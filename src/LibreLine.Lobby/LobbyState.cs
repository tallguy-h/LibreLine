// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using LibreLine.Games;
using LibreLine.Protocol;
using LibreLine.Sessions;
using Session = LibreLine.Sessions.Session;

namespace LibreLine.Lobby;

/// <summary>The lobby's own state (its chat) and the whole-list pushes it sends whenever something changes.</summary>
public sealed class LobbyState(ISessionRegistry sessions, IGameService games, GameMessages gameMessages,
    PlayerMessages playerMessages, TimeProvider time) : ISessionListener, IGameListener
{
    public const int ChatHistoryLength = 100;
    public const int MaxChatLength = 500;

    private readonly List<ChatLine> chat = [];

    public IReadOnlyList<ChatLine> Chat => chat;

    public void AddChat(ChatLine line)
    {
        chat.Add(line);
        if (chat.Count > ChatHistoryLength)
        {
            chat.RemoveRange(0, chat.Count - ChatHistoryLength);
        }
    }

    public long Now => time.GetUtcNow().ToUnixTimeMilliseconds();

    public IEnumerable<Session> InLobby => sessions.All.Where(s => s.InLobby);

    /// <summary><c>LobbyPlayerList</c> (604): everyone online, zlib-compressed.</summary>
    public LobbyPlayerListRequest PlayerList()
    {
        var list = new LobbyPlayerListRequest.Types.PlayerList();
        list.Players.AddRange(sessions.All.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => playerMessages.BuildSmall(s.AccountId)));
        return new LobbyPlayerListRequest { PlayerList = Zlib.Compress(list) };
    }

    /// <summary><c>LobbyGameList</c> (609): the open lobby games, zlib-compressed.</summary>
    public LobbyGameListRequest GameList()
    {
        var list = new GameList();
        list.Games.AddRange(games.All
            .Where(g => g.Kind == GameKind.Lobby && g.Phase == GamePhase.NotStarted)
            .OrderBy(g => g.CreatedAt)
            .Select(gameMessages.Details));
        return new LobbyGameListRequest { GameList = Zlib.Compress(list) };
    }

    /// <summary><c>ObservableGameList</c> (622): games in progress that can be watched.</summary>
    public ObservableGameListRequest ObservableList()
    {
        var list = new GameList();
        list.Games.AddRange(games.All
            .Where(g => g.Phase == GamePhase.InProgress && g.Options.ObservableBy != Observers.None)
            .OrderBy(g => g.CreatedAt)
            .Select(gameMessages.Details));
        return new ObservableGameListRequest { GameList = Zlib.Compress(list) };
    }

    public void BroadcastPlayerList()
    {
        var message = PlayerList();
        foreach (var session in InLobby)
        {
            session.Send(message);
        }
    }

    /// <summary>Sends the open-game list to the lobby, except to the players of a game that has just started:
    /// a list arriving right after <c>GameCreated</c> would be handled instead of it.</summary>
    public void BroadcastGameList(Game? justStarted = null)
    {
        var message = GameList();
        foreach (var session in InLobby.Where(s => justStarted?.SeatOf(s.AccountId) is null))
        {
            session.Send(message);
        }
    }

    public void BroadcastObservableList()
    {
        var message = ObservableList();
        foreach (var session in sessions.All.Where(s => s.ObservableListSubscribed))
        {
            session.Send(message);
        }
    }

    public void SessionStarted(Session session) => BroadcastPlayerList();

    public void SessionEnded(Session session) => BroadcastPlayerList();

    public void GameChanged(Game game)
    {
        if (game.Options.ObservableBy != Observers.None)
        {
            BroadcastObservableList();
        }

        if (game.Kind == GameKind.Lobby && game.Phase is GamePhase.NotStarted)
        {
            BroadcastGameList();
        }
    }
}
