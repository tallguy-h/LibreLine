// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using LibreLine.Games;
using LibreLine.Players;
using LibreLine.Protocol;
using LibreLine.Sessions;
using Microsoft.Extensions.Logging;
using JoinError = Com.Daysofwonder.Async.LobbyJoinDeniedRequest.Types.JoinError;
using ObservedStatus = Com.Daysofwonder.Async.GameObservedRequest.Types.Status;

namespace LibreLine.Lobby;

/// <summary>
/// <c>EngageGameWithFriends</c> (405) → <c>GameCreated</c> (404) to the creator, and pushed to invitees who are
/// online. Offline invitees find the game in their next status report.
/// </summary>
internal sealed class EngageGameWithFriendsHandler(ISessionRegistry sessions, IGameService games, GameMessages messages,
    ILogger<EngageGameWithFriendsHandler> logger) : IMessageHandler<EngageGameWithFriendsRequest>
{
    public Task HandleAsync(RequestContext context, EngageGameWithFriendsRequest message)
    {
        if (sessions.Find(context.Connection) is not { } session)
        {
            return Task.CompletedTask;
        }

        var error = "no configuration";
        var options = message.Configuration is { } configuration ? games.ReadConfiguration(configuration, out error) : null;
        var friends = message.Friends.Where(f => f != session.AccountId).Distinct().ToList();
        if (options is null || friends.Count == 0)
        {
            var why = options is null ? error : "no friends invited";
            logger.LogInformation("Friends game from {Account} refused: {Why}", session.AccountId, why);
            context.ReplyError(ErrorCode.BadRequest, why);
            return Task.CompletedTask;
        }

        // initial_state is a T2RSummary, not a game state.
        var summary = message.HasInitialSummaryData ? message.InitialSummaryData : message.InitialState;
        var game = games.Create(GameKind.Friends, session.AccountId, options with { MaxPlayers = friends.Count + 1 }, friends,
            message.UserData.ToByteArray(), summary.ToByteArray());

        // local_player_id and invited_by are seats; they are equal for the creator, so no invitation dialog shows.
        var details = messages.Details(game);
        context.Reply(new GameCreatedRequest { LocalPlayerId = 1, InvitedBy = 1, Details = details });
        foreach (var seat in game.Seats.Skip(1))
        {
            sessions.SendTo(seat.AccountId, new GameCreatedRequest { LocalPlayerId = seat.Number, InvitedBy = 1, Details = details });
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>AnswerInvitation</c> (540) → <c>InvitationAnswered</c> (541), also pushed to the other players.</summary>
internal sealed class AnswerInvitationHandler(ISessionRegistry sessions, IGameService games, GameMessages messages)
    : IMessageHandler<AnswerInvitationRequest>
{
    public Task HandleAsync(RequestContext context, AnswerInvitationRequest message)
    {
        if (sessions.Find(context.Connection) is not { } session || games.Find(message.GameId) is not { } game)
        {
            context.ReplyError(ErrorCode.UnknownGame, gameId: message.GameId);
            return Task.CompletedTask;
        }

        if (game.SeatOf(session.AccountId) is not { } seat || seat.Number == 1)
        {
            context.ReplyError(ErrorCode.NotYourInvitation, gameId: game.Id);
            return Task.CompletedTask;
        }

        if (game.Phase != GamePhase.WaitingInvitation)
        {
            context.ReplyError(ErrorCode.WrongState, gameId: game.Id);
            return Task.CompletedTask;
        }

        var everyoneIn = false;
        if (message.Accept)
        {
            everyoneIn = games.Accept(game, seat, message.UserData.ToByteArray());
        }
        else
        {
            games.Decline(game, seat);
        }

        var answer = new InvitationAnsweredRequest { GameId = game.Id, Accept = message.Accept, Invitee = messages.Player(game, seat) };
        context.Reply(answer);
        foreach (var other in game.Seats.Where(s => s != seat))
        {
            sessions.SendTo(other.AccountId, answer);
        }

        if (everyoneIn)
        {
            games.Start(game);
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>LobbyCreateGame</c> (607) → <c>LobbyGameCreated</c> (608), and the new game list to the lobby.</summary>
internal sealed class LobbyCreateGameHandler(ISessionRegistry sessions, IGameService games, GameMessages messages,
    LobbyState lobby, ILogger<LobbyCreateGameHandler> logger) : IMessageHandler<LobbyCreateGameRequest>
{
    public Task HandleAsync(RequestContext context, LobbyCreateGameRequest message)
    {
        if (sessions.Find(context.Connection) is not { } session)
        {
            return Task.CompletedTask;
        }

        var error = "no configuration";
        var options = message.Configuration is { } configuration ? games.ReadConfiguration(configuration, out error) : null;
        if (options is null)
        {
            var why = error;
            logger.LogInformation("Lobby game from {Account} refused: {Why}", session.AccountId, why);
            context.ReplyError(ErrorCode.BadRequest, why);
            return Task.CompletedTask;
        }

        if (message.HasPassword && message.Password.Length > 0)
        {
            options = options with { Password = message.Password };
        }

        var summary = message.HasInitialSummaryData ? message.InitialSummaryData : message.InitialState;
        var game = games.Create(GameKind.Lobby, session.AccountId, options, [], message.UserData.ToByteArray(),
            summary.ToByteArray());
        context.Reply(new LobbyGameCreatedRequest { Game = messages.Details(game) });
        lobby.BroadcastGameList();
        return Task.CompletedTask;
    }
}

/// <summary>
/// <c>LobbyJoinGame</c> (610) → <c>LobbyNewPlayer</c> (611) to everyone in the game, or <c>LobbyJoinDenied</c>
/// (612). A full game starts: <c>GameCreated</c> (without <c>invited_by</c>) to each player.
/// </summary>
internal sealed class LobbyJoinGameHandler(ISessionRegistry sessions, IGameService games, IPlayerService players,
    GameMessages messages, LobbyState lobby) : IMessageHandler<LobbyJoinGameRequest>
{
    public Task HandleAsync(RequestContext context, LobbyJoinGameRequest message)
    {
        if (sessions.Find(context.Connection) is not { } session)
        {
            return Task.CompletedTask;
        }

        var game = games.Find(message.GameId);
        if (Denied(game, session.AccountId, message) is { } cause)
        {
            context.Reply(new LobbyJoinDeniedRequest { GameId = message.GameId, Cause = cause });
            return Task.CompletedTask;
        }

        var seat = games.AddSeat(game!, session.AccountId, message.UserData.ToByteArray());
        var joined = new LobbyNewPlayerRequest { Game = messages.Details(game!), JoiningPlayer = seat.Number };
        context.Reply(joined);
        foreach (var other in game!.Seats.Where(s => s != seat))
        {
            sessions.SendTo(other.AccountId, joined);
        }

        if (!game.IsFull)
        {
            lobby.BroadcastGameList();
            return Task.CompletedTask;
        }

        var details = messages.Details(game);
        foreach (var player in game.Seats)
        {
            sessions.SendTo(player.AccountId, new GameCreatedRequest { LocalPlayerId = player.Number, Details = details });
        }

        games.Start(game);
        lobby.BroadcastGameList(justStarted: game);
        return Task.CompletedTask;
    }

    private JoinError? Denied(Game? game, int accountId, LobbyJoinGameRequest message)
    {
        if (game is null || game.Kind != GameKind.Lobby)
        {
            return JoinError.UnknownGame;
        }

        if (game.SeatOf(accountId) is not null)
        {
            return JoinError.PlayerDuplicate;
        }

        if (game.Phase != GamePhase.NotStarted || game.IsFull)
        {
            return JoinError.GameFull;
        }

        if (game.Options.Password is { } password && password != message.Password)
        {
            return JoinError.PasswordMismatch;
        }

        if (game.Seats.Any(s => players.IsIgnoring(s.AccountId, accountId)))
        {
            return JoinError.Ignored;
        }

        var player = players.Find(accountId);
        if ((player?.Karma ?? PlayerRecord.StartingKarma) < game.Options.MinKarma)
        {
            return JoinError.NotEnoughKarma;
        }

        if ((player?.Rating ?? PlayerRecord.StartingRating) < game.Options.MinRankScore)
        {
            return JoinError.NotEnoughRankscore;
        }

        return null;
    }
}

/// <summary><c>LobbyLeaveGame</c> (613) → <c>LobbyPlayerLeftGame</c> (614), pushed to the game's other players.
/// If the creator leaves, the game is dropped.</summary>
internal sealed class LobbyLeaveGameHandler(ISessionRegistry sessions, IGameService games, GameMessages messages,
    LobbyState lobby) : IMessageHandler<LobbyLeaveGameRequest>
{
    public Task HandleAsync(RequestContext context, LobbyLeaveGameRequest message)
    {
        if (sessions.Find(context.Connection) is not { } session || games.Find(message.GameId) is not { } game
            || game.SeatOf(session.AccountId) is not { } seat)
        {
            context.ReplyError(ErrorCode.UnknownGame, gameId: message.GameId);
            return Task.CompletedTask;
        }

        if (game.Phase != GamePhase.NotStarted)
        {
            context.ReplyError(ErrorCode.WrongState, gameId: game.Id);
            return Task.CompletedTask;
        }

        var leaving = messages.Player(game, seat);
        var others = game.Seats.Where(s => s != seat).Select(s => s.AccountId).ToList();
        var creatorLeft = seat.Number == 1;
        if (!creatorLeft)
        {
            games.RemoveSeat(game, session.AccountId);
        }

        var left = new LobbyPlayerLeftGameRequest { Game = messages.Details(game), LeavingPlayer = leaving };
        context.Reply(left);
        foreach (var account in others)
        {
            sessions.SendTo(account, left);
        }

        if (creatorLeft)
        {
            games.Drop(game);
        }
        else
        {
            lobby.BroadcastGameList();
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>SubscribeToObservableGameList</c> (620) → <c>SubscribedToObservableGameList</c> (621), then pushes.</summary>
internal sealed class SubscribeObservableListHandler(ISessionRegistry sessions, LobbyState lobby)
    : IMessageHandler<SubscribeToObservableGameListRequest>
{
    public Task HandleAsync(RequestContext context, SubscribeToObservableGameListRequest message)
    {
        var session = sessions.Find(context.Connection);
        if (session is not null)
        {
            session.ObservableListSubscribed = message.Subscribe;
        }

        context.Reply(new SubscribedToObservableGameListRequest { Subscribed = message.Subscribe });
        if (message.Subscribe)
        {
            session?.Send(lobby.ObservableList());
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>StartObserveGame</c> (650) → <c>GameObserved</c> (652) with the game's state.</summary>
internal sealed class StartObserveGameHandler(ISessionRegistry sessions, IGameService games, IPlayerService players,
    GameMessages messages) : IMessageHandler<StartObserveGameRequest>
{
    public Task HandleAsync(RequestContext context, StartObserveGameRequest message)
    {
        var reply = new GameObservedRequest { GameId = message.GameId };
        var session = sessions.Find(context.Connection);
        var game = games.Find(message.GameId);
        reply.Status = session is null || game is null ? ObservedStatus.NoSuchGameError
            : game.SeatOf(session.AccountId) is not null ? ObservedStatus.InGameError
            : game.IsOver ? ObservedStatus.GameOver
            : game.Phase != GamePhase.InProgress ? ObservedStatus.GameNotStarted
            : !MayObserve(game, session.AccountId) ? ObservedStatus.ForbiddenError
            : ObservedStatus.Ok;

        if (reply.Status == ObservedStatus.Ok)
        {
            games.AddObserver(game!, session!);
            reply.GameStatus = messages.Report(game!, null, full: true);
        }

        context.Reply(reply);
        return Task.CompletedTask;
    }

    private bool MayObserve(Game game, int accountId) => game.Options.ObservableBy switch
    {
        Observers.All => true,
        Observers.Buddies => game.Seats.Any(s => players.Find(s.AccountId)?.Buddies.Any(b => b.AccountId == accountId) == true),
        _ => false,
    };
}

/// <summary><c>StopObserveGame</c> (651) → <c>GameObserved</c> (652). The reply is required: the client waits for it.</summary>
internal sealed class StopObserveGameHandler(ISessionRegistry sessions, IGameService games)
    : IMessageHandler<StopObserveGameRequest>
{
    public Task HandleAsync(RequestContext context, StopObserveGameRequest message)
    {
        if (sessions.Find(context.Connection) is { } session && games.Find(message.GameId) is { } game)
        {
            games.RemoveObserver(game, session);
        }

        context.Reply(new GameObservedRequest { GameId = message.GameId, Status = ObservedStatus.Ok });
        return Task.CompletedTask;
    }
}
