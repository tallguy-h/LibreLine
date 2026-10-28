// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using Google.Protobuf;
using LibreLine.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibreLine.Sessions;

/// <summary>The online registry. Every method runs under the server lock.</summary>
public sealed class SessionRegistry(IServiceProvider services, ILogger<SessionRegistry> logger)
    : ISessionRegistry, IPresence, IConnectionAuthorizer
{
    private readonly Dictionary<int, Session> byAccount = [];
    private readonly Dictionary<long, Session> byConnection = [];
    private IReadOnlyList<ISessionListener>? listeners;

    // Read without the server lock by the dispatcher, so writes swap a fresh set in.
    private volatile HashSet<long> authenticated = [];

    private IReadOnlyList<ISessionListener> Listeners =>
        listeners ??= services.GetServices<ISessionListener>().ToList();

    public IReadOnlyCollection<Session> All => byAccount.Values;

    public Session? Find(IClientConnection connection) => byConnection.GetValueOrDefault(connection.Id);

    public Session? FindByAccount(int accountId) => byAccount.GetValueOrDefault(accountId);

    public bool IsOnline(int accountId) => byAccount.ContainsKey(accountId);

    public bool IsAuthenticated(IClientConnection connection) => authenticated.Contains(connection.Id);

    public bool SendTo(int accountId, IMessage message)
    {
        if (!byAccount.TryGetValue(accountId, out var session))
        {
            return false;
        }

        session.Send(message);
        return true;
    }

    /// <summary>Adds a session. A new login replaces the player's old connection, which is closed.</summary>
    public void Add(Session session)
    {
        var replaced = byAccount.GetValueOrDefault(session.AccountId);
        byAccount[session.AccountId] = session;
        byConnection[session.Connection.Id] = session;
        if (replaced is not null)
        {
            byConnection.Remove(replaced.Connection.Id);
            replaced.Connection.Close();
            logger.LogInformation("Player {Account} logged in again; the old connection {Connection} is closed",
                session.AccountId, replaced.Connection.Id);
        }

        authenticated = byConnection.Keys.ToHashSet();
        if (replaced is null)
        {
            NotifyWatchers(session.AccountId, online: true);
        }

        foreach (var listener in Listeners)
        {
            listener.SessionStarted(session);
        }
    }

    /// <summary>Removes the connection's session, if it is still the player's current one.</summary>
    public void Remove(IClientConnection connection)
    {
        if (!byConnection.Remove(connection.Id, out var session))
        {
            return;
        }

        authenticated = byConnection.Keys.ToHashSet();
        if (byAccount.TryGetValue(session.AccountId, out var current) && current == session)
        {
            byAccount.Remove(session.AccountId);
            logger.LogInformation("Player {Account} went offline", session.AccountId);
            NotifyWatchers(session.AccountId, online: false);
            foreach (var listener in Listeners)
            {
                listener.SessionEnded(session);
            }
        }
    }

    public (IReadOnlyList<int> Present, IReadOnlyList<int> Absent) Register(Session session, IEnumerable<int> accountIds)
    {
        var ids = accountIds.Distinct().ToList();
        session.WatchedAccounts.UnionWith(ids);
        return (ids.Where(IsOnline).ToList(), ids.Where(id => !IsOnline(id)).ToList());
    }

    public void Unregister(Session session, IEnumerable<int> accountIds)
    {
        var ids = accountIds.ToList();
        if (ids.Count == 0)
        {
            session.WatchedAccounts.Clear();
        }
        else
        {
            session.WatchedAccounts.ExceptWith(ids);
        }
    }

    private void NotifyWatchers(int accountId, bool online)
    {
        var update = new AsyncBuddyPresencePartialUpdateRequest();
        (online ? update.Present : update.Absent).Add(accountId);
        foreach (var watcher in byAccount.Values.Where(s => s.AccountId != accountId && s.WatchedAccounts.Contains(accountId)))
        {
            watcher.Send(update);
        }
    }
}
