// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Engine;
using LibreLine.Sessions;

namespace LibreLine.Games;

public enum GameKind
{
    /// <summary>Created with chosen players (<c>EngageGameWithFriends</c>).</summary>
    Friends,

    /// <summary>An open game in the lobby, or an automatch (<c>LobbyCreateGame</c>). Rated.</summary>
    Lobby,
}

/// <summary>Where a game is in its life (the <c>GameStatus</c> the client is sent).</summary>
public enum GamePhase
{
    WaitingInvitation,
    NotStarted,
    OpeningRound,
    InProgress,
    Outcome,
    Aborted,
    PlayerTimeout,
}

/// <summary>Why a seat became a robot (<c>PlayerTimeout.PlayerStatus</c>).</summary>
public enum ReplacementReason
{
    Timeout = 1,
    Forfeit = 2,
    Left = 3,
    Robot = 4,
}

public enum Observers
{
    None,
    All,
    Buddies,
}

/// <summary>A game's configuration, as read from <c>GameConfiguration</c>; the raw message is kept to send back.</summary>
public sealed record GameOptions
{
    public string Name { get; init; } = "";
    public bool Private { get; init; }
    public string? Password { get; init; }
    public bool Lurkable { get; init; }
    public bool Rated { get; init; }
    public int MinPlayers { get; init; } = 2;
    public int MaxPlayers { get; init; } = 2;
    public int MinKarma { get; init; }
    public int MinRankScore { get; init; }

    /// <summary>The first seat to move (1-based), if the configuration says.</summary>
    public int? FirstPlayer { get; init; }

    /// <summary>A single-session game: absent players are replaced by robots.</summary>
    public bool Synchronous { get; init; }

    /// <summary>Each seat's time bank, in seconds; 0 for no limit.</summary>
    public int TimeoutSeconds { get; init; }

    public bool RequireOpeningRound { get; init; }
    public Observers ObservableBy { get; init; }

    /// <summary>The settings in <c>configuration.data</c> (field 13).</summary>
    public required GameSettings Settings { get; init; }

    /// <summary>The <c>GameConfiguration</c> as the creator sent it.</summary>
    public required byte[] RawConfiguration { get; init; }
}

/// <summary>One seat at a game. <see cref="Number"/> is the seat (1-based), which is also <c>Player.id</c>.</summary>
public sealed class Seat
{
    public required int Number { get; set; }
    public required int AccountId { get; init; }
    public required string Name { get; init; }

    /// <summary>Played by a robot in its controller's client.</summary>
    public bool IsBot { get; internal set; }

    /// <summary>Friends games: has accepted the invitation (the creator always has).</summary>
    public bool Accepted { get; internal set; }

    public bool Forfeited { get; internal set; }
    public bool TimedOut { get; internal set; }

    /// <summary>Left a single-session game; a robot plays until they resume.</summary>
    public bool Left { get; internal set; }

    public bool OutcomeSeen { get; internal set; }

    /// <summary>The <c>user_data</c> given when creating, joining or accepting (shown in <c>GameDetails</c>).</summary>
    public byte[]? PregameData { get; internal set; }

    /// <summary>The opening choice (<c>pb.T2RPlayerState</c>), private to the seat.</summary>
    public byte[]? OpeningData { get; internal set; }

    public bool OpeningDone { get; internal set; }

    /// <summary>What is left of the time bank, as of <see cref="ClockStartedAt"/> if the clock is running.</summary>
    public long ClockRemainingMs { get; internal set; }

    public long? ClockStartedAt { get; internal set; }

    /// <summary>Why the seat is a robot.</summary>
    public ReplacementReason ReplacedReason { get; internal set; } = ReplacementReason.Robot;

    /// <summary>The controller that has been told about this robot (<c>PlayerReplaced</c>), by account.</summary>
    internal int? ReplacedSentTo { get; set; }

    /// <summary>A human still in the game, who can play their own seat and control robots.</summary>
    public bool IsActiveHuman => !IsBot && !Forfeited && !TimedOut && !Left;
}

public sealed record ChatLine(string From, string Text, int PlayerId, long Timestamp);

/// <summary>The recorded result of a finished game.</summary>
public sealed record GameOutcomeRecord(
    IReadOnlyList<SeatRanking> Rankings,
    IReadOnlyList<SeatKarma> Karma,
    IReadOnlyList<(int Seat, int Score, int Rank)> Scores);

public sealed record SeatRanking(int Seat, double OldRating, double NewRating, int OldGames, int NewGames);

public sealed record SeatKarma(int Seat, int OldKarma, int NewKarma);

/// <summary>A game the server hosts. Kept in memory only, behind <see cref="IGameStore"/>.</summary>
public sealed class Game
{
    public required long Id { get; init; }
    public required GameKind Kind { get; init; }
    public required GameOptions Options { get; init; }
    public GamePhase Phase { get; internal set; }
    public List<Seat> Seats { get; } = [];
    public long CreatedAt { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public DateTimeOffset LastActivity { get; internal set; } = DateTimeOffset.UtcNow;

    /// <summary>The latest <c>pb.T2RCommit</c>; empty before the first commit.</summary>
    public byte[] State { get; internal set; } = [];

    /// <summary>The latest <c>pb.T2RSummary</c>.</summary>
    public byte[] Summary { get; internal set; } = [];

    /// <summary>The turn queue: only its head may act.</summary>
    public List<int> Queue { get; internal set; } = [];

    /// <summary>The number of commits so far.</summary>
    public int TurnIndex { get; internal set; }

    /// <summary>The number of moves in the stored state.</summary>
    public int ActionCount { get; internal set; }

    /// <summary>The seat that made the last move.</summary>
    public int? LastActor { get; internal set; }

    public List<ChatLine> Chat { get; } = [];

    /// <summary>Sessions watching from the Observe tab.</summary>
    public HashSet<Session> Observers { get; } = [];

    public GameOutcomeRecord? Outcome { get; internal set; }

    /// <summary>The rules model after the latest legal state, when it could be rebuilt.</summary>
    internal RulesGame? Model { get; set; }

    /// <summary>When the robot to move should next be woken.</summary>
    internal DateTimeOffset? WakeRobotAt { get; set; }

    public int? Head => Queue.Count > 0 ? Queue[0] : null;

    public Seat? SeatOf(int accountId) => Seats.FirstOrDefault(s => s.AccountId == accountId);

    public Seat? SeatAt(int number) => number >= 1 && number <= Seats.Count ? Seats[number - 1] : null;

    public bool IsFull => Seats.Count >= Options.MaxPlayers;

    public bool IsOver => Phase is GamePhase.Outcome or GamePhase.Aborted or GamePhase.PlayerTimeout;

    /// <summary>The human who runs the robots: the creator, or else the first remaining human.</summary>
    public Seat? Controller => Seats[0].IsActiveHuman ? Seats[0] : Seats.FirstOrDefault(s => s.IsActiveHuman);

    /// <summary>The seat whose client plays the head of the queue: the head itself, or for a robot its controller.</summary>
    public int? ActivePlayer => Head is { } head && SeatAt(head) is { } seat
        ? seat.IsBot ? Controller?.Number : head
        : null;
}

/// <summary>The storage contract for games. The shipped store keeps them in memory; a fork could persist them.</summary>
public interface IGameStore
{
    IReadOnlyCollection<Game> All { get; }

    Game? Find(long id);

    void Add(Game game);

    void Remove(long id);
}

public sealed class InMemoryGameStore : IGameStore
{
    private readonly Dictionary<long, Game> games = [];

    public IReadOnlyCollection<Game> All => games.Values;

    public Game? Find(long id) => games.GetValueOrDefault(id);

    public void Add(Game game) => games.Add(game.Id, game);

    public void Remove(long id) => games.Remove(id);
}
