// SPDX-License-Identifier: GPL-2.0-only
using Google.Protobuf;
using LibreLine.Protocol;

namespace LibreLine.Sessions;

/// <summary>A logged-in player on one connection, and what that connection is looking at.</summary>
public sealed class Session(long id, int accountId, string name, IClientConnection connection)
{
    public long Id { get; } = id;

    public int AccountId { get; } = accountId;

    public string Name { get; } = name;

    public IClientConnection Connection { get; } = connection;

    public DateTimeOffset Started { get; } = DateTimeOffset.UtcNow;

    /// <summary>Between <c>EnterLobby</c> and <c>ExitLobby</c>.</summary>
    public bool InLobby { get; set; }

    /// <summary>The game the client says it is viewing (<c>SwitchedToGame</c>), if any.</summary>
    public long? CurrentGame { get; set; }

    /// <summary>Wants <c>PlayerPresenceUpdate</c> pushes for its current game.</summary>
    public bool PresenceSubscribed { get; set; }

    /// <summary>Wants <c>ObservableGameList</c> pushes.</summary>
    public bool ObservableListSubscribed { get; set; }

    /// <summary>Accounts whose online state this client registered for (buddy and invite panels).</summary>
    public HashSet<int> WatchedAccounts { get; } = [];

    public void Send(IMessage message) => Connection.Send(message);
}

/// <summary>Something that wants to know when players come and go (lobby lists, game presence).</summary>
public interface ISessionListener
{
    void SessionStarted(Session session);

    void SessionEnded(Session session);
}

/// <summary>Who is online, and how to reach them.</summary>
public interface ISessionRegistry
{
    IReadOnlyCollection<Session> All { get; }

    Session? Find(IClientConnection connection);

    Session? FindByAccount(int accountId);

    bool IsOnline(int accountId);

    /// <summary>Pushes a message to a player if they are online. Returns false if they aren't.</summary>
    bool SendTo(int accountId, IMessage message);
}

/// <summary>Buddy and invite panel presence (<c>RegisterPresence</c> and its updates).</summary>
public interface IPresence
{
    (IReadOnlyList<int> Present, IReadOnlyList<int> Absent) Register(Session session, IEnumerable<int> accountIds);

    void Unregister(Session session, IEnumerable<int> accountIds);
}

/// <summary>How many games the server hosts, for <c>ServerStatistics</c>. Implemented by the games module.</summary>
public interface IHostedGameCounter
{
    int HostedGames { get; }
}
