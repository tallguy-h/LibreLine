// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder;
using Com.Daysofwonder.Async;
using LibreLine.Games;
using LibreLine.Players;
using LibreLine.Protocol;
using LibreLine.Sessions;


namespace LibreLine.Lobby;

/// <summary><c>EnterLobby</c> (600) → <c>LobbyEntered</c> (601), then the player and game lists.</summary>
internal sealed class EnterLobbyHandler(ISessionRegistry sessions, LobbyState lobby) : IMessageHandler<EnterLobbyRequest>
{
    public Task HandleAsync(RequestContext context, EnterLobbyRequest message)
    {
        context.Reply(new LobbyEnteredRequest());
        if (sessions.Find(context.Connection) is { } session)
        {
            session.InLobby = true;
            lobby.BroadcastPlayerList();
            session.Send(lobby.GameList());
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>ExitLobby</c> (602) → <c>LobbyExited</c> (603).</summary>
internal sealed class ExitLobbyHandler(ISessionRegistry sessions) : IMessageHandler<ExitLobbyRequest>
{
    public Task HandleAsync(RequestContext context, ExitLobbyRequest message)
    {
        if (sessions.Find(context.Connection) is { } session)
        {
            session.InLobby = false;
        }

        context.Reply(new LobbyExitedRequest());
        return Task.CompletedTask;
    }
}

/// <summary><c>AskPlayerInfo</c> (605) → <c>LobbyPlayerInfo</c> (606).</summary>
internal sealed class AskPlayerInfoHandler(PlayerMessages messages) : IMessageHandler<AskPlayerInfoRequest>
{
    public Task HandleAsync(RequestContext context, AskPlayerInfoRequest message)
    {
        context.Reply(new LobbyPlayerInfoRequest { Player = messages.Build(1, message.PlayerId, withRights: true) });
        return Task.CompletedTask;
    }
}

/// <summary><c>AskAnyPlayerInfo</c> (630) → <c>PlayerInfo</c> (631): any player, online or not.</summary>
internal sealed class AskAnyPlayerInfoHandler(PlayerMessages messages) : IMessageHandler<AskAnyPlayerInfoRequest>
{
    public Task HandleAsync(RequestContext context, AskAnyPlayerInfoRequest message)
    {
        context.Reply(new PlayerInfoRequest { Player = messages.Build(1, message.PlayerId, withRights: true) });
        return Task.CompletedTask;
    }
}

/// <summary><c>AsyncBuddyList</c> (515) → <c>AsyncBuddyListContent</c> (516).</summary>
internal sealed class AsyncBuddyListHandler(ISessionRegistry sessions, IPlayerService players, PlayerMessages messages)
    : IMessageHandler<AsyncBuddyListRequest>
{
    public Task HandleAsync(RequestContext context, AsyncBuddyListRequest message)
    {
        var list = new AsyncBuddyList();
        if (sessions.Find(context.Connection) is { } session && players.Find(session.AccountId) is { } player)
        {
            // Each buddy's Player.id is its position; rights decide whom the Buddies tab offers to invite.
            for (var i = 0; i < player.Buddies.Count; i++)
            {
                var buddy = player.Buddies[i];
                list.Player.Add(messages.Build(i + 1, buddy.AccountId, withRights: true));
                if (sessions.IsOnline(buddy.AccountId))
                {
                    list.Present.Add(buddy.AccountId);
                }
            }
        }

        context.Reply(new AsyncBuddyListContentRequest { Buddies = list });
        return Task.CompletedTask;
    }
}

/// <summary>Finds a contact from a management request: an account id, or failing that a list position.</summary>
internal static class Contacts
{
    public static int? Resolve(IReadOnlyList<ContactEntry> list, int playerId)
    {
        if (list.Any(c => c.AccountId == playerId))
        {
            return playerId;
        }

        return playerId >= 1 && playerId <= list.Count ? list[playerId - 1].AccountId : null;
    }
}

/// <summary><c>AsyncBuddyManagement</c> (518) → <c>AsyncBuddyAdded</c> (519) or <c>AsyncBuddyRemoved</c> (520).</summary>
internal sealed class AsyncBuddyManagementHandler(ISessionRegistry sessions, IPlayerService players, PlayerMessages messages)
    : IMessageHandler<AsyncBuddyManagementRequest>
{
    public async Task HandleAsync(RequestContext context, AsyncBuddyManagementRequest message)
    {
        if (sessions.Find(context.Connection) is not { } session || players.Find(session.AccountId) is not { } player
            || message.Buddy is not { } buddy)
        {
            context.ReplyError(ErrorCode.BadRequest);
            return;
        }

        if (message.Operation == AsyncBuddyManagementRequest.Types.Operation.Remove)
        {
            var account = Contacts.Resolve(player.Buddies, buddy.PlayerId) ?? buddy.PlayerId;
            await players.RemoveBuddyAsync(session.AccountId, account);
            context.Reply(new AsyncBuddyRemovedRequest { RemovedBuddy = new AsyncBuddy { PlayerId = account } });
            return;
        }

        if (await players.AddBuddyAsync(session.AccountId, buddy.PlayerId) is null)
        {
            context.ReplyError(ErrorCode.UnknownPlayer);
            return;
        }

        var position = player.Buddies.FindIndex(b => b.AccountId == buddy.PlayerId) + 1;
        context.Reply(new AsyncBuddyAddedRequest { AddedBuddy = messages.Build(position, buddy.PlayerId, withRights: true) });
    }
}

/// <summary><c>AsyncIgnoreList</c> (560) → <c>AsyncIgnoreListContent</c> (561).</summary>
internal sealed class AsyncIgnoreListHandler(ISessionRegistry sessions, IPlayerService players, PlayerMessages messages)
    : IMessageHandler<AsyncIgnoreListRequest>
{
    public Task HandleAsync(RequestContext context, AsyncIgnoreListRequest message)
    {
        var list = new AsyncIgnoreList();
        if (sessions.Find(context.Connection) is { } session && players.Find(session.AccountId) is { } player)
        {
            for (var i = 0; i < player.Ignores.Count; i++)
            {
                list.Player.Add(messages.Build(i + 1, player.Ignores[i].AccountId));
            }
        }

        context.Reply(new AsyncIgnoreListContentRequest { Ignores = list });
        return Task.CompletedTask;
    }
}

/// <summary><c>AsyncIgnoreManagement</c> (562) → <c>AsyncIgnoreAdded</c> (563) or <c>AsyncIgnoreRemoved</c> (564).</summary>
internal sealed class AsyncIgnoreManagementHandler(ISessionRegistry sessions, IPlayerService players, PlayerMessages messages)
    : IMessageHandler<AsyncIgnoreManagementRequest>
{
    public async Task HandleAsync(RequestContext context, AsyncIgnoreManagementRequest message)
    {
        if (sessions.Find(context.Connection) is not { } session || players.Find(session.AccountId) is not { } player
            || message.Ignore is not { } ignore)
        {
            context.ReplyError(ErrorCode.BadRequest);
            return;
        }

        if (message.Operation == AsyncIgnoreManagementRequest.Types.Operation.Remove)
        {
            var account = Contacts.Resolve(player.Ignores, ignore.PlayerId) ?? ignore.PlayerId;
            await players.RemoveIgnoreAsync(session.AccountId, account);
            context.Reply(new AsyncIgnoreRemovedRequest { RemovedIgnore = new AsyncIgnore { PlayerId = account } });
            return;
        }

        if (await players.AddIgnoreAsync(session.AccountId, ignore.PlayerId) is null)
        {
            context.ReplyError(ErrorCode.UnknownPlayer);
            return;
        }

        var position = player.Ignores.FindIndex(b => b.AccountId == ignore.PlayerId) + 1;
        context.Reply(new AsyncIgnoreAddedRequest { AddedIgnore = messages.Build(position, ignore.PlayerId) });
    }
}

/// <summary>
/// <c>MulticastChat</c> (524): relayed as <c>ClientChat</c> (525). Lobby chat has no game id, and the field must
/// stay absent in what is sent back.
/// </summary>
internal sealed class MulticastChatHandler(ISessionRegistry sessions, IGameService games, IPlayerService players,
    LobbyState lobby, PlayerMessages playerMessages, GameMessages gameMessages) : IMessageHandler<MulticastChatRequest>
{
    public Task HandleAsync(RequestContext context, MulticastChatRequest message)
    {
        if (sessions.Find(context.Connection) is not { } session || string.IsNullOrEmpty(message.Text))
        {
            return Task.CompletedTask;
        }

        var text = message.Text.Length > LobbyState.MaxChatLength ? message.Text[..LobbyState.MaxChatLength] : message.Text;
        if (!message.HasGameId || message.GameId == 0)
        {
            lobby.AddChat(new ChatLine(session.Name, text, session.AccountId, lobby.Now));
            var chat = new ClientChatRequest { Text = text, Sender = playerMessages.Build(1, session.AccountId) };
            foreach (var recipient in lobby.InLobby.Append(session).Distinct())
            {
                if (!players.IsIgnoring(recipient.AccountId, session.AccountId))
                {
                    recipient.Send(chat);
                }
            }

            return Task.CompletedTask;
        }

        if (games.Find(message.GameId) is not { } game)
        {
            return Task.CompletedTask;
        }

        var seat = game.SeatOf(session.AccountId);
        var sender = seat is not null ? gameMessages.Player(game, seat) : playerMessages.Build(1, session.AccountId);
        game.Chat.Add(new ChatLine(session.Name, text, seat?.Number ?? 0, lobby.Now));

        var gameChat = new ClientChatRequest { GameId = game.Id, Text = text, Sender = sender };
        var everyone = message.RecipientIds.Count == 0;
        var recipients = everyone
            ? game.Seats
            : message.RecipientIds.Select(game.SeatAt).OfType<Seat>().Append(seat).OfType<Seat>().Distinct();
        foreach (var recipient in recipients)
        {
            if (!players.IsIgnoring(recipient.AccountId, session.AccountId))
            {
                sessions.SendTo(recipient.AccountId, gameChat);
            }
        }

        if (everyone)
        {
            foreach (var observer in game.Observers.Where(o => o != session))
            {
                observer.Send(gameChat);
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>GetChatHistory</c> (535) → <c>ClientChatHistory</c> (536).</summary>
internal sealed class GetChatHistoryHandler(IGameService games, LobbyState lobby) : IMessageHandler<GetChatHistoryRequest>
{
    public Task HandleAsync(RequestContext context, GetChatHistoryRequest message)
    {
        var reply = new ClientChatHistoryRequest();
        IEnumerable<ChatLine> lines = lobby.Chat;
        if (message.HasGameId && message.GameId != 0)
        {
            reply.GameId = message.GameId;
            lines = games.Find(message.GameId)?.Chat ?? [];
        }

        reply.Entry.AddRange(lines.Select(l => new ChatHistoryEntry
        {
            From = l.From,
            Text = l.Text,
            PlayerId = l.PlayerId,
            Timestamp = l.Timestamp,
        }));
        context.Reply(reply);
        return Task.CompletedTask;
    }
}
