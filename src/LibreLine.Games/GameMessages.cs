// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder;
using Com.Daysofwonder.Async;
using Google.Protobuf;
using LibreLine.Sessions;

namespace LibreLine.Games;

/// <summary>Builds the protocol messages that describe a game. Shared with the lobby module.</summary>
public sealed class GameMessages(PlayerMessages players, TimeProvider time)
{
    public static GameStatus StatusOf(GamePhase phase) => phase switch
    {
        GamePhase.WaitingInvitation => GameStatus.WaitingInvitation,
        GamePhase.NotStarted => GameStatus.NotStarted,
        GamePhase.OpeningRound => GameStatus.FirstUserDataRound,
        GamePhase.InProgress => GameStatus.InProgress,
        GamePhase.Outcome => GameStatus.Outcome,
        GamePhase.Aborted => GameStatus.Aborted,
        GamePhase.PlayerTimeout => GameStatus.PlayerTimeout,
        _ => GameStatus.InProgress,
    };

    public static GameConfiguration Configuration(Game game) => GameConfiguration.Parser.ParseFrom(game.Options.RawConfiguration);

    public Player Player(Game game, Seat seat) => players.Build(seat.Number, seat.AccountId, isBot: seat.IsBot);

    public IEnumerable<Player> Players(Game game) => game.Seats.Select(s => Player(game, s));

    public GameDetails Details(Game game)
    {
        var details = new GameDetails { GameId = game.Id, Configuration = Configuration(game) };
        details.Players.AddRange(Players(game));
        foreach (var seat in game.Seats.Where(s => s.PregameData is { Length: > 0 }))
        {
            details.UserData.Add(new PlayerPregameData { PlayerId = seat.Number, Data = ByteString.CopyFrom(seat.PregameData) });
        }

        return details;
    }

    public GameClock Clock(Game game, Seat seat)
    {
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        var running = seat.ClockStartedAt is { } since;
        var remaining = seat.ClockRemainingMs - (running ? now - seat.ClockStartedAt!.Value : 0);
        return new GameClock
        {
            StartedAt = game.CreatedAt,
            LastStartedAt = seat.ClockStartedAt ?? game.CreatedAt,
            Duration = game.Options.TimeoutSeconds * 1000L,
            Remaining = Math.Max(0, remaining),
            Armed = game.Phase == GamePhase.InProgress && game.Head == seat.Number,
            Paused = false,
            Timestamp = now,
        };
    }

    public IEnumerable<StatusReport.Types.PlayerClock> Clocks(Game game) =>
        game.Seats.Select(s => new StatusReport.Types.PlayerClock { PlayerId = s.Number, Clock = Clock(game, s) });

    /// <summary>
    /// A game's <c>StatusReport</c> for one viewer (null for an observer). Opening choices are included only before
    /// the first commit, and only the viewer's own and those of robots it controls.
    /// </summary>
    public StatusReport Report(Game game, Seat? viewer, bool full)
    {
        var report = new StatusReport
        {
            GameId = game.Id,
            Status = StatusOf(game.Phase),
            TurnIndex = game.TurnIndex,
            Configuration = Configuration(game),
            GameCreatedAt = game.CreatedAt,
        };

        if (full && game.State.Length > 0)
        {
            report.Data = ByteString.CopyFrom(game.State);
        }

        if (game.Summary.Length > 0)
        {
            report.SummaryData = ByteString.CopyFrom(game.Summary);
        }

        report.Players.AddRange(Players(game));
        report.NextPlayerIds.AddRange(game.Queue);
        if (game.Head is { } head)
        {
            report.TurnId = head;
        }

        if (game.ActivePlayer is { } active)
        {
            report.ActivePlayer = active;
        }

        report.Clock.AddRange(Clocks(game));
        report.Forfeiters.AddRange(game.Seats.Where(s => s.Forfeited).Select(s => s.Number));
        report.TimedOutPlayers.AddRange(game.Seats.Where(s => s.TimedOut).Select(s => s.Number));
        report.LeftPlayers.AddRange(game.Seats.Where(s => s.Left).Select(s => s.Number));

        if (game.Phase is GamePhase.OpeningRound || (game.Phase == GamePhase.InProgress && game.TurnIndex == 0))
        {
            report.PlayerUserDataUpdated.AddRange(game.Seats.Where(s => s.OpeningData is not null).Select(s => s.Number));
            if (viewer is not null)
            {
                report.UserData.AddRange(PrivateOpenings(game, viewer));
            }
        }

        if (game.Phase == GamePhase.WaitingInvitation)
        {
            var invitation = new InvitationDetails { InvitedBy = 1 };
            invitation.Confirmation.AddRange(game.Seats.Select(s =>
                new InvitationDetails.Types.Confirmation { PlayerId = s.Number, Accepted = s.Accepted }));
            report.Invitation = invitation;
        }

        if (game.IsOver)
        {
            report.OutcomeNotSeen.AddRange(game.Seats.Where(s => !s.OutcomeSeen && !s.IsBot).Select(s => s.Number));
        }

        if (game.Outcome is { } outcome)
        {
            report.RankingUpdate.AddRange(RankingUpdates(game, outcome));
            report.KarmaUpdate.AddRange(KarmaUpdates(outcome));
        }

        return report;
    }

    /// <summary>The opening choices a viewer may see: its own, and those of the robots it controls.</summary>
    public IEnumerable<PlayerPregameData> PrivateOpenings(Game game, Seat viewer)
    {
        var controls = game.Controller == viewer;
        return game.Seats
            .Where(s => s.OpeningData is not null && (s == viewer || (controls && s.IsBot)))
            .Select(s => new PlayerPregameData { PlayerId = s.Number, Data = ByteString.CopyFrom(s.OpeningData) });
    }

    /// <summary>
    /// <c>ActionRequired</c> for the seat at the head of the queue, sent to <paramref name="recipient"/>: the head
    /// itself, or the controller of a robot.
    /// </summary>
    public ActionRequiredRequest ActionRequired(Game game, Seat recipient)
    {
        var head = game.Head ?? recipient.Number;
        var message = new ActionRequiredRequest
        {
            GameId = game.Id,
            TurnIndex = game.TurnIndex,
            State = ByteString.CopyFrom(game.State),
            DifferentPlayer = head != recipient.Number,
        };
        message.Players.AddRange(Players(game));
        message.NextPlayerIds.AddRange(game.Queue);
        if (game.SeatAt(head) is { } headSeat)
        {
            message.PlayerClock = Clock(game, headSeat);
        }

        if (game.TurnIndex == 0)
        {
            message.UserData.AddRange(PrivateOpenings(game, recipient));
        }

        return message;
    }

    public IEnumerable<PlayerRankingUpdate> RankingUpdates(Game game, GameOutcomeRecord outcome) =>
        outcome.Rankings.Select(r => new PlayerRankingUpdate
        {
            PlayerId = r.Seat,
            Delta = (float)(r.NewRating - r.OldRating),
            OldRankScore = r.OldRating,
            NewRankScore = r.NewRating,
            OldNbGames = r.OldGames,
            NewNbGames = r.NewGames,
        });

    public static IEnumerable<KarmaUpdate> KarmaUpdates(GameOutcomeRecord outcome) =>
        outcome.Karma.Select(k => new KarmaUpdate { PlayerId = k.Seat, OldKarma = k.OldKarma, NewKarma = k.NewKarma });

    public PlayerPresenceUpdateRequest Presence(Game game, ISessionRegistry sessions)
    {
        var update = new PlayerPresenceUpdateRequest { GameId = game.Id };
        foreach (var seat in game.Seats)
        {
            var session = sessions.FindByAccount(seat.AccountId);
            (session is null ? update.Absent : update.Present).Add(seat.Number);
            if (session?.CurrentGame == game.Id)
            {
                update.InGame.Add(seat.Number);
            }
        }

        return update;
    }
}
