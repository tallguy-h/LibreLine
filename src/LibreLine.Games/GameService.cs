// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using Google.Protobuf;
using LibreLine.Players;
using LibreLine.Protocol;
using LibreLine.Rules;
using LibreLine.Rules.Engine;
using LibreLine.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pb;
using Session = LibreLine.Sessions.Session;
using PlayerStatus = Com.Daysofwonder.Async.PlayerTimeoutRequest.Types.PlayerStatus;

namespace LibreLine.Games;

public enum RulesMode
{
    /// <summary>Refuse illegal moves.</summary>
    Enforce,

    /// <summary>Log illegal moves, but accept them.</summary>
    Audit,

    /// <summary>Don't check moves (the model is still used to score and end games when it can be rebuilt).</summary>
    Off,
}

public sealed class GamesOptions
{
    public RulesMode Rules { get; set; } = RulesMode.Enforce;

    /// <summary>The map enums games may be created on.</summary>
    public ISet<int> Maps { get; set; } = Enumerable.Range(0, 17).ToHashSet();

    /// <summary>How long to wait after <c>PlayerReplaced</c> before waking the robot, so its dialog can close.</summary>
    public TimeSpan RobotWakeDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Finished games are dropped this long after their last activity, even if not every result was seen.</summary>
    public TimeSpan FinishedGameLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Lobby games that never filled are dropped after this long.</summary>
    public TimeSpan OpenGameLifetime { get; set; } = TimeSpan.FromHours(12);
}

/// <summary>Told when a game starts, ends or goes away (the lobby keeps its lists from this).</summary>
public interface IGameListener
{
    void GameChanged(Game game);
}

/// <summary>The games domain as the lobby module uses it. Every method runs under the server lock.</summary>
public interface IGameService
{
    IReadOnlyCollection<Game> All { get; }

    Game? Find(long id);

    /// <summary>The games to report to a player: those they sit at, except lobby games not yet started and
    /// finished games whose results they have seen.</summary>
    IEnumerable<Game> GamesOf(int accountId);

    /// <summary>Reads and checks a configuration. Returns null with a reason if the server won't host it.</summary>
    GameOptions? ReadConfiguration(GameConfiguration configuration, out string error);

    Game Create(GameKind kind, int creator, GameOptions options, IReadOnlyList<int> friends, byte[]? creatorPregame,
        byte[]? summary);

    /// <summary>Lobby games: seats a joining player in the next seat.</summary>
    Seat AddSeat(Game game, int accountId, byte[]? pregame);

    /// <summary>Lobby games: removes a player who hasn't started playing; later seats move up.</summary>
    void RemoveSeat(Game game, int accountId);

    void Drop(Game game);

    /// <summary>Friends games: records an acceptance. Returns true when everyone has accepted.</summary>
    bool Accept(Game game, Seat seat, byte[]? pregame);

    /// <summary>Friends games: a decline aborts the game.</summary>
    void Decline(Game game, Seat seat);

    /// <summary>Starts a full game: the opening round, or straight to play.</summary>
    void Start(Game game);

    void AddObserver(Game game, Session session);

    void RemoveObserver(Game game, Session session);
}

public sealed partial class GameService(
    IGameStore store,
    IRulesEngine rules,
    IPlayerService players,
    ISessionRegistry sessions,
    GameMessages messages,
    IOptions<GamesOptions> options,
    TimeProvider time,
    IServiceProvider services,
    ILogger<GameService> logger) : IGameService, IHostedGameCounter, ISessionListener
{
    private long lastId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private IReadOnlyList<IGameListener>? listeners;

    private GamesOptions Settings => options.Value;

    private IReadOnlyList<IGameListener> Listeners => listeners ??= services.GetServices<IGameListener>().ToList();

    private long Now => time.GetUtcNow().ToUnixTimeMilliseconds();

    public IReadOnlyCollection<Game> All => store.All;

    public int HostedGames => store.All.Count(g => !g.IsOver);

    public Game? Find(long id) => store.Find(id);

    public IEnumerable<Game> GamesOf(int accountId) =>
        store.All.Where(g => g.SeatOf(accountId) is { } seat
            && g.Phase != GamePhase.NotStarted
            && !(g.IsOver && seat.OutcomeSeen));

    // ----- Creating ----------------------------------------------------------------------------------------------

    public GameOptions? ReadConfiguration(GameConfiguration configuration, out string error)
    {
        var settings = Payloads.ReadConfigurationSettings(configuration.Data);
        if (settings is null)
        {
            error = "the configuration has no game settings";
            return null;
        }

        if (!Settings.Maps.Contains(settings.MapEnumIndex) || !rules.Supports(settings.MapEnumIndex, settings.RulesEngineVersion))
        {
            error = $"map {settings.MapEnumIndex} with rules engine version {settings.RulesEngineVersion} isn't hosted here";
            return null;
        }

        error = "";
        return Payloads.ToOptions(configuration, settings);
    }

    public Game Create(GameKind kind, int creator, GameOptions options, IReadOnlyList<int> friends, byte[]? creatorPregame,
        byte[]? summary)
    {
        var game = new Game
        {
            Id = NextId(),
            Kind = kind,
            Options = options,
            Phase = kind == GameKind.Friends ? GamePhase.WaitingInvitation : GamePhase.NotStarted,
            CreatedAt = Now,
            Summary = summary ?? [],
        };

        foreach (var account in friends.Prepend(creator))
        {
            game.Seats.Add(NewSeat(game, account));
        }

        game.Seats[0].Accepted = true;
        game.Seats[0].PregameData = creatorPregame is { Length: > 0 } ? creatorPregame : null;
        store.Add(game);
        logger.LogInformation("Game {Game} created by {Account}: {Kind}, map {Map}, {Seats} seats", game.Id, creator, kind,
            options.Settings.MapEnum, kind == GameKind.Friends ? game.Seats.Count : options.MaxPlayers);
        return game;
    }

    private Seat NewSeat(Game game, int account) => new()
    {
        Number = game.Seats.Count + 1,
        AccountId = account,
        Name = players.NameOf(account),
        ClockRemainingMs = game.Options.TimeoutSeconds * 1000L,
    };

    private long NextId()
    {
        // Ids come from the clock so they don't repeat across restarts: a client may still list an old one.
        var candidate = Math.Max(Now, lastId + 1);
        lastId = candidate;
        return candidate;
    }

    public Seat AddSeat(Game game, int accountId, byte[]? pregame)
    {
        var seat = NewSeat(game, accountId);
        seat.Accepted = true;
        seat.PregameData = pregame is { Length: > 0 } ? pregame : null;
        game.Seats.Add(seat);
        game.LastActivity = time.GetUtcNow();
        return seat;
    }

    public void RemoveSeat(Game game, int accountId)
    {
        game.Seats.RemoveAll(s => s.AccountId == accountId);
        for (var i = 0; i < game.Seats.Count; i++)
        {
            game.Seats[i].Number = i + 1;
        }

        game.LastActivity = time.GetUtcNow();
    }

    public void Drop(Game game)
    {
        store.Remove(game.Id);
        logger.LogInformation("Game {Game} dropped", game.Id);
        Changed(game);
    }

    public bool Accept(Game game, Seat seat, byte[]? pregame)
    {
        seat.Accepted = true;
        if (pregame is { Length: > 0 })
        {
            seat.PregameData = pregame;
        }

        game.LastActivity = time.GetUtcNow();
        return game.Seats.All(s => s.Accepted);
    }

    public void Decline(Game game, Seat seat)
    {
        logger.LogInformation("Game {Game}: seat {Seat} declined", game.Id, seat.Number);
        Abort(game);
    }

    public void Start(Game game)
    {
        game.LastActivity = time.GetUtcNow();
        if (!game.Options.RequireOpeningRound)
        {
            BeginPlay(game);
            return;
        }

        game.Phase = GamePhase.OpeningRound;
        game.Queue = game.Seats.Select(s => s.Number).ToList();
        logger.LogInformation("Game {Game}: opening round", game.Id);
        foreach (var seat in game.Seats.Where(s => !s.IsBot))
        {
            sessions.SendTo(seat.AccountId, new UserDataUpdateRequiredRequest { GameId = game.Id });
        }

        Changed(game);
    }

    public void AddObserver(Game game, Session session) => game.Observers.Add(session);

    public void RemoveObserver(Game game, Session session) => game.Observers.Remove(session);

    private void Changed(Game game)
    {
        foreach (var listener in Listeners)
        {
            listener.GameChanged(game);
        }
    }

    // ----- The opening round -------------------------------------------------------------------------------------

    /// <summary><c>UpdateUserData</c>: a seat's private opening choice. Returns the error to reply with, if any.</summary>
    internal ErrorCode? UpdateUserData(Game game, Seat sender, int seatNumber, byte[] data, bool expectMore)
    {
        if (game.Phase != GamePhase.OpeningRound)
        {
            return ErrorCode.WrongState;
        }

        var seat = game.SeatAt(seatNumber);
        if (seat is null || !(seat == sender || (seat.IsBot && game.Controller == sender)))
        {
            return ErrorCode.NotYourTurn;
        }

        if (seat.OpeningDone)
        {
            return ErrorCode.WrongState;
        }

        if (!CheckOpening(game, seat, data))
        {
            return ErrorCode.WrongState;
        }

        seat.OpeningData = data;
        game.LastActivity = time.GetUtcNow();
        if (!expectMore)
        {
            seat.OpeningDone = true;
            game.Queue.Remove(seat.Number);
            if (game.Queue.Count == 0)
            {
                BeginPlay(game);
            }
        }

        return null;
    }

    private bool CheckOpening(Game game, Seat seat, byte[] data)
    {
        if (Settings.Rules == RulesMode.Off || game.Options.Settings.Players != game.Seats.Count)
        {
            return true;
        }

        var choice = Payloads.ParsePlayerState(data);
        if (choice is null)
        {
            return Refuse(game, seat.Number, "unreadable opening choice");
        }

        // Only this seat's choice is checked; the other seats' requests matter because Germany deals in seat order.
        var openings = Payloads.OpeningsFrom(game)
            .Where(o => o.Seat != seat.Number)
            .Select(o => o with { Choose = null })
            .Append(new OpeningChoice(seat.Number,
                choice.AskForFirstTickets is { } ask ? Payloads.ToMove(ask) : null,
                choice.ChooseFirstTickets is { } choose ? Payloads.ToMove(choose) : null))
            .ToList();

        var result = rules.Replay(game.Options.Settings, openings, []);
        if (result.Violation is not { } violation)
        {
            return true;
        }

        // A choice made before every earlier seat has asked can't be checked yet.
        if (result.Game is { } model && !model.CanCheckOpeningChoice(seat.Number))
        {
            return true;
        }

        return Refuse(game, seat.Number, violation.ToString());
    }

    private bool Refuse(Game game, int seat, string why)
    {
        logger.LogWarning("Game {Game}: illegal step by seat {Seat}: {Why}{Mode}", game.Id, seat, why,
            Settings.Rules == RulesMode.Audit ? " (audit only)" : "");
        return Settings.Rules != RulesMode.Enforce;
    }

    private void BeginPlay(Game game)
    {
        game.Phase = GamePhase.InProgress;
        game.TurnIndex = 0;
        var first = game.Options.FirstPlayer ?? (game.Options.Settings.FirstPlayer + 1);
        if (first < 1 || first > game.Seats.Count)
        {
            first = 1;
        }

        game.Queue = [first];
        game.LastActivity = time.GetUtcNow();
        logger.LogInformation("Game {Game}: play begins with seat {Seat}", game.Id, first);
        StartClock(game);
        SendTurn(game, robotDelay: game.Seats.Any(s => s.IsBot && s.ReplacedSentTo is null));
        Changed(game);
    }

    // ----- Turns -------------------------------------------------------------------------------------------------

    /// <summary>Tells whoever plays the head of the queue that it's their move.</summary>
    private void SendTurn(Game game, bool robotDelay = false)
    {
        if (game.Phase != GamePhase.InProgress || game.Head is not { } head || game.SeatAt(head) is not { } seat)
        {
            return;
        }

        if (seat.IsBot)
        {
            game.WakeRobotAt = robotDelay ? time.GetUtcNow() + Settings.RobotWakeDelay : null;
            if (!robotDelay)
            {
                WakeRobot(game);
            }

            return;
        }

        game.WakeRobotAt = null;
        sessions.SendTo(seat.AccountId, messages.ActionRequired(game, seat));
    }

    /// <summary>Resends <c>ActionRequired</c> to a player who has come back to a game where it is their move.</summary>
    internal void ResendTurn(Game game, Seat seat)
    {
        if (game.Phase == GamePhase.InProgress && game.Head == seat.Number && !seat.IsBot)
        {
            sessions.SendTo(seat.AccountId, messages.ActionRequired(game, seat));
        }
    }

    /// <summary>Wakes the robot engine in the controller's client, if the robot is to move and the controller online.</summary>
    internal void WakeRobot(Game game)
    {
        game.WakeRobotAt = null;
        if (game.Phase != GamePhase.InProgress || game.Head is not { } head || game.SeatAt(head) is not { IsBot: true } robot)
        {
            return;
        }

        if (game.Controller is not { } controller || !sessions.IsOnline(controller.AccountId))
        {
            // The engine runs only while the controller is connected: wait for them.
            return;
        }

        if (robot.ReplacedSentTo != controller.AccountId)
        {
            // PlayerReplaced assigns the seat to the robot. Waking it while that dialog is open doesn't work, so
            // wake it a little later.
            SendReplaced(game, robot, controller, becomeRobot: true);
            game.WakeRobotAt = time.GetUtcNow() + Settings.RobotWakeDelay;
            return;
        }

        sessions.SendTo(controller.AccountId, messages.ActionRequired(game, controller));
        var wake = new PlayerTimeoutRequest
        {
            GameId = game.Id,
            OffenderId = robot.Number,
            Status = PlayerStatus.Robot,
            TurnIndex = game.TurnIndex,
            GameStatus = GameMessages.StatusOf(game.Phase),
        };
        wake.Players.AddRange(messages.Players(game));
        wake.NextPlayerIds.AddRange(game.Queue);
        sessions.SendTo(controller.AccountId, wake);
    }

    /// <summary>Wakes every robot this player controls that is to move (they left a game screen, or reconnected).</summary>
    internal void WakeRobotsControlledBy(int accountId)
    {
        foreach (var game in store.All.Where(g => g.Phase == GamePhase.InProgress && g.Controller?.AccountId == accountId))
        {
            if (game.Head is { } head && game.SeatAt(head) is { IsBot: true } && game.WakeRobotAt is null)
            {
                WakeRobot(game);
            }
        }
    }

    private void SendReplaced(Game game, Seat seat, Seat recipient, bool becomeRobot)
    {
        sessions.SendTo(recipient.AccountId, new PlayerReplacedRequest
        {
            GameId = game.Id,
            PlayerId = seat.Number,
            BecomeRobot = becomeRobot,
            Status = (PlayerStatus)(int)seat.ReplacedReason,
        });

        if (becomeRobot && recipient == game.Controller)
        {
            seat.ReplacedSentTo = recipient.AccountId;
        }
    }

    /// <summary>A seat becomes a robot: told to every other human; the controller's copy assigns it.</summary>
    private void Replace(Game game, Seat seat, ReplacementReason reason)
    {
        seat.IsBot = true;
        seat.ReplacedReason = reason;
        seat.ReplacedSentTo = null;
        StopClock(seat);
        foreach (var human in game.Seats.Where(s => s.IsActiveHuman && s != seat))
        {
            SendReplaced(game, seat, human, becomeRobot: true);
        }

        if (game.Head == seat.Number)
        {
            game.WakeRobotAt = time.GetUtcNow() + Settings.RobotWakeDelay;
        }

        logger.LogInformation("Game {Game}: seat {Seat} is now a robot ({Reason})", game.Id, seat.Number, reason);
    }

    // ----- Commits -----------------------------------------------------------------------------------------------

    internal sealed record CommitResult(ErrorCode? Error, string? Reason = null, int? PreviousHead = null);

    /// <summary><c>CommitAction</c>: checks and stores a new state.</summary>
    internal CommitResult Commit(Game game, Seat sender, int seatNumber, byte[] nextState, IReadOnlyList<int> nextQueue,
        byte[]? nextSummary)
    {
        if (sender.TimedOut && seatNumber == sender.Number)
        {
            return new CommitResult(ErrorCode.YouRanOutOfTime);
        }

        if (sender.Forfeited && seatNumber == sender.Number)
        {
            return new CommitResult(ErrorCode.YouForfeited);
        }

        if (game.Phase != GamePhase.InProgress)
        {
            return new CommitResult(ErrorCode.WrongState, "the game isn't in progress");
        }

        var seat = game.SeatAt(seatNumber);
        if (seat is null || !(seat == sender && !seat.IsBot || seat.IsBot && game.Controller == sender))
        {
            return new CommitResult(ErrorCode.NotYourTurn, "the sender doesn't play that seat");
        }

        var commit = Payloads.ParseCommit(nextState);
        if (commit is null)
        {
            return new CommitResult(ErrorCode.BadRequest, "unreadable state");
        }

        var stored = Payloads.ParseCommit(game.State);
        var storedActions = stored?.Action ?? [];
        if (commit.Action.Count < storedActions.Count
            || !storedActions.Select((a, i) => a.Equals(commit.Action[i])).All(same => same))
        {
            return new CommitResult(ErrorCode.NotYourTurn, "the state doesn't continue the stored one");
        }

        var added = commit.Action.Skip(storedActions.Count).ToList();
        if (added.Any(a => a.Player != seatNumber))
        {
            return new CommitResult(ErrorCode.NotYourTurn, "the new moves aren't all by that seat");
        }

        if (!IsAtHead(game, seatNumber))
        {
            return new CommitResult(ErrorCode.NotYourTurn, $"seat {game.Head} is to move");
        }

        var result = rules.Replay(Payloads.ToSettings(commit.Settings), Payloads.OpeningsFrom(commit),
            commit.Action.Select(Payloads.ToMove).ToList());
        if (result.Violation is { } violation && Settings.Rules != RulesMode.Off)
        {
            if (violation.MoveIndex >= storedActions.Count && !Refuse(game, seatNumber, violation.ToString()))
            {
                return new CommitResult(ErrorCode.WrongState, violation.Reason);
            }

            if (violation.MoveIndex < storedActions.Count)
            {
                logger.LogWarning("Game {Game}: the stored history no longer replays: {Violation}", game.Id, violation);
            }
        }

        if (stored is null && game.Seats.Any(s => s.OpeningData is not null)
            && commit.PlayerState.Count < game.Seats.Count(s => s.OpeningData is not null))
        {
            logger.LogWarning("Game {Game}: the first commit lacks opening choices the server holds", game.Id);
        }

        var previousHead = game.Head;
        game.State = nextState;
        if (nextSummary is { Length: > 0 })
        {
            game.Summary = nextSummary;
        }

        game.TurnIndex++;
        game.ActionCount = commit.Action.Count;
        if (added.Count > 0)
        {
            game.LastActor = seatNumber;
        }

        game.Model = result.IsLegal ? result.Game : null;

        // A robot's queue can be stale: work out the turn from the model instead.
        var queue = seat.IsBot || nextQueue.Count == 0 ? ModelQueue(game) ?? nextQueue.ToList() : nextQueue.ToList();
        game.Queue = queue.Where(n => n >= 1 && n <= game.Seats.Count).ToList();
        game.LastActivity = time.GetUtcNow();

        if (game.Head != previousHead)
        {
            StopClock(game.SeatAt(previousHead ?? 0));
            StartClock(game);
        }

        return new CommitResult(null, PreviousHead: previousHead);
    }

    /// <summary>After a commit has been acknowledged: end a game a robot finished, or pass the turn on.</summary>
    internal async Task AfterCommit(Game game, Seat committed, int? previousHead)
    {
        if (committed.IsBot && game.Model is { } model)
        {
            model.CloseOpenTurn();
            if (model.IsOver)
            {
                // The robot engine sends no GameOver: the server ends the game itself.
                await EndGame(game, game.State, null, null);
                return;
            }
        }

        if (game.Head != previousHead || (game.Head is { } head && game.SeatAt(head)?.IsBot == true))
        {
            SendTurn(game);
        }
    }

    private List<int>? ModelQueue(Game game) =>
        game.Model is { IsOver: false } model ? [model.CurrentPlayer, .. Rotation(game, model.CurrentPlayer)] : null;

    private static IEnumerable<int> Rotation(Game game, int from) =>
        Enumerable.Range(1, game.Seats.Count - 1).Select(i => ((from - 1 + i) % game.Seats.Count) + 1);

    private static bool IsAtHead(Game game, int seat)
    {
        if (game.Head == seat)
        {
            return true;
        }

        // A robot can commit with itself still at the head; the next seat may then commit too.
        return game.Head is { } head && game.SeatAt(head) is { IsBot: true } && game.LastActor == head
            && game.Queue.Count > 1 && game.Queue[1] == seat;
    }

    // ----- Clocks ------------------------------------------------------------------------------------------------

    private void StartClock(Game game)
    {
        if (game.Phase == GamePhase.InProgress && game.Head is { } head && game.SeatAt(head) is { IsBot: false } seat
            && game.Options.TimeoutSeconds > 0)
        {
            seat.ClockStartedAt = Now;
        }
    }

    private void StopClock(Seat? seat)
    {
        if (seat?.ClockStartedAt is { } since)
        {
            seat.ClockRemainingMs = Math.Max(0, seat.ClockRemainingMs - (Now - since));
            seat.ClockStartedAt = null;
        }
    }

    /// <summary>Runs every second: wakes robots that are due, and times out seats whose bank has run out.</summary>
    internal async Task Tick()
    {
        var now = time.GetUtcNow();
        foreach (var game in store.All.ToList())
        {
            if (game.WakeRobotAt is { } due && due <= now)
            {
                WakeRobot(game);
            }

            if (game.Phase == GamePhase.InProgress && game.Head is { } head && game.SeatAt(head) is { } seat
                && seat.ClockStartedAt is { } since && seat.ClockRemainingMs - (Now - since) <= 0)
            {
                await TimeOut(game, seat);
            }

            var finishedTooLong = game.IsOver && (game.Seats.All(s => s.OutcomeSeen || s.IsBot)
                || now - game.LastActivity > Settings.FinishedGameLifetime);
            var openTooLong = game.Phase is GamePhase.NotStarted or GamePhase.WaitingInvitation
                && now - game.LastActivity > Settings.OpenGameLifetime;
            if (finishedTooLong || openTooLong)
            {
                Drop(game);
            }
        }
    }

    private async Task TimeOut(Game game, Seat seat)
    {
        StopClock(seat);
        seat.ClockRemainingMs = 0;
        seat.TimedOut = true;
        seat.OutcomeSeen = true;
        logger.LogInformation("Game {Game}: seat {Seat} ran out of time", game.Id, seat.Number);

        var robotPossible = game.Seats.Any(s => s.IsActiveHuman);
        if (!robotPossible)
        {
            game.Phase = GamePhase.PlayerTimeout;
        }

        var notice = new PlayerTimeoutRequest
        {
            GameId = game.Id,
            OffenderId = seat.Number,
            Status = PlayerStatus.Timeout,
            TurnIndex = game.TurnIndex,
            GameStatus = GameMessages.StatusOf(game.Phase),
        };
        notice.Players.AddRange(messages.Players(game));
        notice.NextPlayerIds.AddRange(game.Queue);
        sessions.SendTo(seat.AccountId, notice);
        sessions.SendTo(seat.AccountId, Errors.Create(ErrorCode.YouRanOutOfTime, gameId: game.Id, turnIndex: game.TurnIndex,
            playerId: seat.Number));

        if (game.Kind == GameKind.Lobby)
        {
            await Penalise(game, seat);
        }

        if (robotPossible)
        {
            Replace(game, seat, ReplacementReason.Timeout);
        }
        else
        {
            Changed(game);
        }
    }

    // ----- Leaving -----------------------------------------------------------------------------------------------

    /// <summary><c>GameForfeit</c>: the seat withdraws. Returns true if the game was aborted.</summary>
    internal async Task<bool> Forfeit(Game game, Seat seat)
    {
        seat.Forfeited = true;
        seat.OutcomeSeen = true;
        game.LastActivity = time.GetUtcNow();
        logger.LogInformation("Game {Game}: seat {Seat} withdrew", game.Id, seat.Number);
        if (game.Kind == GameKind.Lobby)
        {
            await Penalise(game, seat);
        }

        // Fewer than two players left ends the game; otherwise a robot takes the seat if a human can run it.
        if (game.Seats.Count(s => !s.Forfeited) < 2 || !game.Seats.Any(s => s.IsActiveHuman))
        {
            Abort(game);
            return true;
        }

        Replace(game, seat, ReplacementReason.Forfeit);
        return false;
    }

    internal void Abort(Game game)
    {
        foreach (var seat in game.Seats)
        {
            StopClock(seat);
        }

        game.Phase = GamePhase.Aborted;
        game.WakeRobotAt = null;
        game.LastActivity = time.GetUtcNow();
        logger.LogInformation("Game {Game} aborted", game.Id);
        Changed(game);
    }

    /// <summary><c>LeaveSyncGame</c>: a robot plays the seat until the player resumes.</summary>
    internal void LeaveSync(Game game, Seat seat)
    {
        if (!game.Options.Synchronous || game.Phase != GamePhase.InProgress || seat.Left || !seat.IsActiveHuman)
        {
            return;
        }

        seat.Left = true;
        Replace(game, seat, ReplacementReason.Left);
    }

    /// <summary><c>ResumeSyncGame</c>: the player takes their seat back from the robot.</summary>
    internal void ResumeSync(Game game, Seat seat)
    {
        if (!seat.Left)
        {
            return;
        }

        seat.Left = false;
        seat.IsBot = false;
        seat.ReplacedSentTo = null;
        foreach (var human in game.Seats.Where(s => s.IsActiveHuman && s != seat))
        {
            SendReplaced(game, seat, human, becomeRobot: false);
        }

        if (game.Head == seat.Number)
        {
            game.WakeRobotAt = null;
            StartClock(game);
            ResendTurn(game, seat);
        }
    }

    // ----- The end -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Ends a game: from <c>GameOver</c> (with the reporting client's scores), or from the model when a robot made
    /// the last move. Sends <c>GameOutcome</c> to every player; <paramref name="replyTo"/> gets it as its reply.
    /// </summary>
    internal async Task EndGame(Game game, byte[] finalState, IReadOnlyList<(int Seat, int Score, int Rank)>? reported,
        RequestContext? replyTo)
    {
        if (finalState.Length > 0)
        {
            game.State = finalState;
        }

        var scores = ModelScores(game, finalState) ?? reported ?? [];
        if (reported is not null)
        {
            foreach (var (seat, score, _) in reported)
            {
                var model = scores.FirstOrDefault(s => s.Seat == seat);
                if (model.Seat == seat && model.Score != score)
                {
                    logger.LogWarning("Game {Game}: seat {Seat} reported {Reported} points, the model scores {Model}",
                        game.Id, seat, score, model.Score);
                }
            }
        }

        foreach (var seat in game.Seats)
        {
            StopClock(seat);
        }

        game.Phase = GamePhase.Outcome;
        game.Queue = [];
        game.WakeRobotAt = null;
        game.LastActivity = time.GetUtcNow();

        var rankings = new List<SeatRanking>();
        var karma = new List<SeatKarma>();
        if (game.Kind == GameKind.Lobby && scores.Count > 0)
        {
            var results = game.Seats
                .Select(s => new GameResult(s.AccountId,
                    scores.FirstOrDefault(x => x.Seat == s.Number) is { Seat: > 0 } r ? r.Rank : game.Seats.Count,
                    s.Forfeited || s.TimedOut))
                .ToList();
            foreach (var change in await players.RecordRatedGameAsync(results))
            {
                var seat = game.SeatOf(change.AccountId)!;
                rankings.Add(new SeatRanking(seat.Number, change.OldRating, change.NewRating, change.OldGames, change.NewGames));
                if (change.OldKarma != change.NewKarma)
                {
                    karma.Add(new SeatKarma(seat.Number, change.OldKarma, change.NewKarma));
                }
            }
        }

        game.Outcome = new GameOutcomeRecord(rankings, karma, scores);
        logger.LogInformation("Game {Game} over: {Scores}", game.Id,
            string.Join(", ", scores.Select(s => $"seat {s.Seat}: {s.Score} (#{s.Rank})")));

        var outcome = new GameOutcomeRequest { GameId = game.Id, Data = ByteString.CopyFrom(game.State) };
        outcome.RankingUpdate.AddRange(messages.RankingUpdates(game, game.Outcome));
        outcome.KarmaUpdate.AddRange(GameMessages.KarmaUpdates(game.Outcome));
        var reporter = replyTo is null ? null : sessions.All.FirstOrDefault(s => s.Connection == replyTo.Connection);
        foreach (var seat in game.Seats.Where(s => !s.Forfeited && !s.TimedOut))
        {
            if (reporter is not null && seat.AccountId == reporter.AccountId)
            {
                replyTo!.Reply(outcome);
            }
            else
            {
                sessions.SendTo(seat.AccountId, outcome);
            }
        }

        foreach (var observer in game.Observers)
        {
            observer.Send(outcome);
        }

        Changed(game);
    }

    /// <summary>Scores and places from the model, when the final state replays.</summary>
    private IReadOnlyList<(int Seat, int Score, int Rank)>? ModelScores(Game game, byte[] finalState)
    {
        var commit = Payloads.ParseCommit(finalState.Length > 0 ? finalState : game.State);
        if (commit is null)
        {
            return null;
        }

        var result = rules.Replay(Payloads.ToSettings(commit.Settings), Payloads.OpeningsFrom(commit),
            commit.Action.Select(Payloads.ToMove).ToList());
        if (!result.IsLegal)
        {
            if (result.Violation is { } violation)
            {
                logger.LogWarning("Game {Game}: the final state doesn't replay ({Violation}); using the reported scores",
                    game.Id, violation);
            }

            return null;
        }

        result.Game!.CloseOpenTurn();
        return result.Game.Score().Select(s => (s.Seat, s.Total, s.Rank)).ToList();
    }

    private async Task Penalise(Game game, Seat seat)
    {
        if (await players.PenaliseAsync(seat.AccountId) is { } change)
        {
            logger.LogInformation("Game {Game}: karma of {Account} {Old} -> {New}", game.Id, seat.AccountId,
                change.OldKarma, change.NewKarma);
        }
    }

    internal void ConfirmOutcome(Game game, Seat seat)
    {
        seat.OutcomeSeen = true;
        if (game.IsOver && game.Seats.All(s => s.OutcomeSeen || s.IsBot))
        {
            Drop(game);
        }
    }

    // ----- Presence ----------------------------------------------------------------------------------------------

    /// <summary>Pushes <c>PlayerPresenceUpdate</c> to everyone subscribed and viewing the game.</summary>
    internal void PushPresence(Game game)
    {
        var update = messages.Presence(game, sessions);
        foreach (var session in sessions.All.Where(s => s.PresenceSubscribed && s.CurrentGame == game.Id))
        {
            session.Send(update);
        }
    }

    public void SessionStarted(Session session)
    {
        foreach (var game in GamesOf(session.AccountId).ToList())
        {
            PushPresence(game);
        }
    }

    public void SessionEnded(Session session)
    {
        foreach (var game in store.All)
        {
            game.Observers.Remove(session);
        }

        foreach (var game in GamesOf(session.AccountId).ToList())
        {
            PushPresence(game);
        }
    }
}
