// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using LibreLine.Protocol;
using LibreLine.Sessions;
using Microsoft.Extensions.Logging;
using Session = LibreLine.Sessions.Session;

namespace LibreLine.Games;

/// <summary>Finds the player, the game and their seat for a request about a game.</summary>
internal sealed class GameLookup(ISessionRegistry sessions, GameService games)
{
    public Session? Session(RequestContext context) => sessions.Find(context.Connection);

    /// <summary>The game and the sender's seat, or replies <c>UNKNOWN_GAME</c> and returns null.</summary>
    public (Session Session, Game Game, Seat Seat)? Seated(RequestContext context, long gameId, bool reply = true)
    {
        if (Session(context) is { } session && games.Find(gameId) is { } game && game.SeatOf(session.AccountId) is { } seat)
        {
            return (session, game, seat);
        }

        if (reply)
        {
            context.ReplyError(ErrorCode.UnknownGame, gameId: gameId);
        }

        return null;
    }
}

/// <summary><c>WhatsNewPussycat</c> (511) → <c>GameStatusReport</c> (512): the player's games.</summary>
internal sealed class WhatsNewPussycatHandler(GameLookup lookup, GameService games, GameMessages messages)
    : IMessageHandler<WhatsNewPussycatRequest>
{
    public Task HandleAsync(RequestContext context, WhatsNewPussycatRequest message)
    {
        var reply = new GameStatusReportRequest();
        if (lookup.Session(context) is { } session)
        {
            var full = message.HasSummaryDataOnly && !message.SummaryDataOnly;
            var list = message.HasGameId
                ? games.Find(message.GameId) is { } one && one.SeatOf(session.AccountId) is not null ? [one] : []
                : games.GamesOf(session.AccountId);
            foreach (var game in list)
            {
                reply.Reports.Add(messages.Report(game, game.SeatOf(session.AccountId), full));
            }
        }

        context.Reply(reply);
        return Task.CompletedTask;
    }
}

/// <summary><c>SwitchedToGame</c> (534): which game the client is viewing. No reply.</summary>
internal sealed class SwitchedToGameHandler(GameLookup lookup, GameService games) : IMessageHandler<SwitchedToGameRequest>
{
    public Task HandleAsync(RequestContext context, SwitchedToGameRequest message)
    {
        if (lookup.Session(context) is not { } session)
        {
            return Task.CompletedTask;
        }

        var previous = session.CurrentGame is { } old ? games.Find(old) : null;
        session.CurrentGame = message.HasGameId && message.GameId != 0 ? message.GameId : null;
        if (previous is not null)
        {
            games.PushPresence(previous);
        }

        if (session.CurrentGame is { } id && games.Find(id) is { } game)
        {
            if (game != previous)
            {
                games.PushPresence(game);
            }

            if (game.SeatOf(session.AccountId) is { } seat)
            {
                // Any ActionRequired sent while the player was away may have been lost.
                games.ResendTurn(game, seat);
            }
        }
        else
        {
            // Leaving a game screen: wake the robots this player runs.
            games.WakeRobotsControlledBy(session.AccountId);
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>SubscribePresenceService</c> (532). No reply; pushes <c>PlayerPresenceUpdate</c> (506).</summary>
internal sealed class SubscribePresenceHandler(GameLookup lookup, GameService games)
    : IMessageHandler<SubscribePresenceServiceRequest>
{
    public Task HandleAsync(RequestContext context, SubscribePresenceServiceRequest message)
    {
        if (lookup.Session(context) is { } session)
        {
            session.PresenceSubscribed = true;
            if (session.CurrentGame is { } id && games.Find(id) is { } game)
            {
                games.PushPresence(game);
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>UnsubscribePresenceService</c> (533). No reply.</summary>
internal sealed class UnsubscribePresenceHandler(GameLookup lookup) : IMessageHandler<UnsubscribePresenceServiceRequest>
{
    public Task HandleAsync(RequestContext context, UnsubscribePresenceServiceRequest message)
    {
        if (lookup.Session(context) is { } session)
        {
            session.PresenceSubscribed = false;
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>UpdateUserData</c> (566) → <c>UserDataUpdated</c> (567): a private opening choice.</summary>
internal sealed class UpdateUserDataHandler(GameLookup lookup, GameService games) : IMessageHandler<UpdateUserDataRequest>
{
    public Task HandleAsync(RequestContext context, UpdateUserDataRequest message)
    {
        if (lookup.Seated(context, message.GameId) is not (_, var game, var seat))
        {
            return Task.CompletedTask;
        }

        var target = message.HasPlayerId ? message.PlayerId : seat.Number;
        var data = message.UserData.ToByteArray();
        if (games.UpdateUserData(game, seat, target, data, message.ExpectMoreData) is { } error)
        {
            context.ReplyError(error, gameId: game.Id, turnIndex: game.TurnIndex, playerId: target);
            return Task.CompletedTask;
        }

        context.Reply(new UserDataUpdatedRequest { GameId = game.Id, PlayerId = target, UserData = message.UserData });
        return Task.CompletedTask;
    }
}

/// <summary><c>CommitAction</c> (501) → <c>ActionCommited</c> (523), then the next <c>ActionRequired</c>.</summary>
internal sealed class CommitActionHandler(GameLookup lookup, GameService games, ILogger<CommitActionHandler> logger)
    : IMessageHandler<CommitActionRequest>
{
    public async Task HandleAsync(RequestContext context, CommitActionRequest message)
    {
        if (lookup.Seated(context, message.GameId) is not (_, var game, var seat))
        {
            return;
        }

        var target = message.HasPlayerId ? message.PlayerId : seat.Number;
        var result = games.Commit(game, seat, target, message.NextState.ToByteArray(), message.NextPlayerIds,
            message.HasNextSummaryData ? message.NextSummaryData.ToByteArray() : null);
        if (result.Error is { } error)
        {
            logger.LogInformation("Game {Game}: commit by seat {Seat} for {Target} refused: {Error} {Reason}", game.Id,
                seat.Number, target, error, result.Reason);
            context.ReplyError(error, result.Reason, game.Id, game.TurnIndex, target);
            return;
        }

        context.Reply(new ActionCommitedRequest { GameId = game.Id, TurnIndex = game.TurnIndex });
        await games.AfterCommit(game, game.SeatAt(target)!, result.PreviousHead);
    }
}

/// <summary><c>MulticastData</c> (502): relayed to the recipients as <c>ClientData</c> (503). No reply.</summary>
internal sealed class MulticastDataHandler(GameLookup lookup, ISessionRegistry sessions, GameMessages messages)
    : IMessageHandler<MulticastDataRequest>
{
    public Task HandleAsync(RequestContext context, MulticastDataRequest message)
    {
        if (lookup.Seated(context, message.GameId, reply: false) is not (_, var game, var seat))
        {
            return Task.CompletedTask;
        }

        // The sender is the acting seat, which for a robot isn't the connection's own.
        var multicast = Payloads.ParseMulticast(message.Data);
        var acting = multicast?.Action is { HasPlayer: true } action ? action.Player
            : multicast is { HasPlayer: true } ? multicast.Player + 1
            : seat.Number;
        var actingSeat = game.SeatAt(acting) ?? seat;

        var data = new ClientDataRequest
        {
            GameId = game.Id,
            Data = message.Data,
            Sender = messages.Player(game, actingSeat),
        };
        if (message.HasPresentPlayersOnly)
        {
            data.PresentPlayersOnly = message.PresentPlayersOnly;
        }

        if (message.Notification is not null)
        {
            data.Notification = message.Notification;
        }

        var recipients = message.RecipientIds.Count > 0
            ? message.RecipientIds.Select(game.SeatAt).OfType<Seat>()
            : game.Seats.Where(s => s != seat);
        var targets = recipients.Where(s => !s.IsBot).Select(s => s.AccountId).ToHashSet();
        if (actingSeat.IsBot)
        {
            // The controller's game screen learns robot moves only from this echo.
            targets.Add(seat.AccountId);
        }

        foreach (var account in targets)
        {
            sessions.SendTo(account, data);
        }

        foreach (var observer in game.Observers)
        {
            observer.Send(data);
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>GameOver</c> (507): the final state and scores, from the client that made the last move.</summary>
internal sealed class GameOverHandler(GameLookup lookup, GameService games) : IMessageHandler<GameOverRequest>
{
    public async Task HandleAsync(RequestContext context, GameOverRequest message)
    {
        if (lookup.Seated(context, message.GameId) is not (_, var game, _))
        {
            return;
        }

        if (game.Phase != GamePhase.InProgress)
        {
            context.ReplyError(ErrorCode.WrongState, gameId: game.Id);
            return;
        }

        var reported = message.FinalScores
            .Select(s => (Seat: s.PlayerId, Score: (int)Math.Round(s.GameScore), Rank: s.GameRank))
            .ToList();
        await games.EndGame(game, message.Data.ToByteArray(), reported, context);
    }
}

/// <summary><c>GameOutcomeConfirmation</c> (522): the player has seen the results. No reply.</summary>
internal sealed class GameOutcomeConfirmationHandler(GameLookup lookup, GameService games)
    : IMessageHandler<GameOutcomeConfirmationRequest>
{
    public Task HandleAsync(RequestContext context, GameOutcomeConfirmationRequest message)
    {
        if (lookup.Seated(context, message.GameId, reply: false) is (_, var game, var seat))
        {
            games.ConfirmOutcome(game, seat);
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>GameAbortedConfirmation</c> (531): the player has seen that the game was aborted. No reply.</summary>
internal sealed class GameAbortedConfirmationHandler(GameLookup lookup, GameService games)
    : IMessageHandler<GameAbortedConfirmationRequest>
{
    public Task HandleAsync(RequestContext context, GameAbortedConfirmationRequest message)
    {
        if (lookup.Seated(context, message.GameId, reply: false) is (_, var game, var seat))
        {
            games.ConfirmOutcome(game, seat);
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>GetClocks</c> (546) → <c>ClocksStatus</c> (547). Every poll is answered, observers' too.</summary>
internal sealed class GetClocksHandler(GameLookup lookup, GameService games, GameMessages messages)
    : IMessageHandler<GetClocksRequest>
{
    public Task HandleAsync(RequestContext context, GetClocksRequest message)
    {
        var reply = new ClocksStatusRequest { GameId = message.GameId };
        if (lookup.Session(context) is not null && games.Find(message.GameId) is { } game)
        {
            reply.Clock.AddRange(messages.Clocks(game));
        }

        context.Reply(reply);
        return Task.CompletedTask;
    }
}

/// <summary><c>PauseClock</c> (542) → <c>ClockPaused</c> (543). Clocks don't pause; the reply keeps the client going.</summary>
internal sealed class PauseClockHandler(GameService games, GameMessages messages) : IMessageHandler<PauseClockRequest>
{
    public Task HandleAsync(RequestContext context, PauseClockRequest message)
    {
        var reply = new ClockPausedRequest { GameId = message.GameId };
        if (message.NeedGameClocks && games.Find(message.GameId) is { } game)
        {
            reply.Clock.AddRange(messages.Clocks(game));
        }

        context.Reply(reply);
        return Task.CompletedTask;
    }
}

/// <summary><c>ResumeClock</c> (544) → <c>ClockResumed</c> (545).</summary>
internal sealed class ResumeClockHandler(GameService games, GameMessages messages) : IMessageHandler<ResumeClockRequest>
{
    public Task HandleAsync(RequestContext context, ResumeClockRequest message)
    {
        var reply = new ClockResumedRequest { GameId = message.GameId };
        if (message.NeedGameClocks && games.Find(message.GameId) is { } game)
        {
            reply.Clock.AddRange(messages.Clocks(game));
        }

        context.Reply(reply);
        return Task.CompletedTask;
    }
}

/// <summary><c>GameForfeit</c> (513) → <c>GameForfeited</c> (514), also pushed to the other players.</summary>
internal sealed class GameForfeitHandler(GameLookup lookup, GameService games, ISessionRegistry sessions)
    : IMessageHandler<GameForfeitRequest>
{
    public async Task HandleAsync(RequestContext context, GameForfeitRequest message)
    {
        if (lookup.Seated(context, message.GameId) is not (_, var game, var seat))
        {
            return;
        }

        if (game.IsOver || seat.Forfeited)
        {
            context.ReplyError(ErrorCode.WrongState, gameId: game.Id);
            return;
        }

        var aborted = await games.Forfeit(game, seat);
        var notice = new GameForfeitedRequest { GameId = game.Id, PlayerId = seat.Number, Aborted = aborted };
        context.Reply(notice);
        foreach (var other in game.Seats.Where(s => s != seat && !s.IsBot))
        {
            sessions.SendTo(other.AccountId, notice);
        }
    }
}

/// <summary><c>GameAbort</c> (529) → <c>GameAborted</c> (530), also pushed to the other players.</summary>
internal sealed class GameAbortHandler(GameLookup lookup, GameService games, ISessionRegistry sessions)
    : IMessageHandler<GameAbortRequest>
{
    public Task HandleAsync(RequestContext context, GameAbortRequest message)
    {
        if (lookup.Seated(context, message.GameId) is not (_, var game, var seat))
        {
            return Task.CompletedTask;
        }

        if (!game.IsOver)
        {
            games.Abort(game);
        }

        seat.OutcomeSeen = true;
        var notice = new GameAbortedRequest { GameId = game.Id };
        notice.AbortingPlayerIds.Add(seat.Number);
        context.Reply(notice);
        foreach (var other in game.Seats.Where(s => s != seat && !s.IsBot))
        {
            sessions.SendTo(other.AccountId, notice);
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>LeaveSyncGame</c> (538). No reply.</summary>
internal sealed class LeaveSyncGameHandler(GameLookup lookup, GameService games) : IMessageHandler<LeaveSyncGameRequest>
{
    public Task HandleAsync(RequestContext context, LeaveSyncGameRequest message)
    {
        if (lookup.Seated(context, message.GameId, reply: false) is (_, var game, var seat))
        {
            games.LeaveSync(game, seat);
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>ResumeSyncGame</c> (539). No reply.</summary>
internal sealed class ResumeSyncGameHandler(GameLookup lookup, GameService games) : IMessageHandler<ResumeSyncGameRequest>
{
    public Task HandleAsync(RequestContext context, ResumeSyncGameRequest message)
    {
        if (lookup.Seated(context, message.GameId, reply: false) is (_, var game, var seat))
        {
            games.ResumeSync(game, seat);
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>EngageGame</c> (402): automatch without a lobby. Not used by this client; refused cleanly.</summary>
internal sealed class EngageGameHandler : IMessageHandler<EngageGameRequest>
{
    public Task HandleAsync(RequestContext context, EngageGameRequest message)
    {
        context.ReplyError(ErrorCode.BadRequest, "automatch goes through the lobby on this server");
        return Task.CompletedTask;
    }
}

/// <summary><c>DisengageGame</c> (410) → <c>GameDisengaged</c> (411).</summary>
internal sealed class DisengageGameHandler : IMessageHandler<DisengageGameRequest>
{
    public Task HandleAsync(RequestContext context, DisengageGameRequest message)
    {
        context.Reply(new GameDisengagedRequest { OfferId = message.OfferId });
        return Task.CompletedTask;
    }
}

