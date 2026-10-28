# Server design decisions

## Ground rules

- **The client is never modified.** Players change one setting, `customAsyncServerName` in `UserDefault.xml`
  ([protocol overview](protocol/README.md#pointing-the-client-at-a-server)). Nothing else on their machine changes.
- **Steam and Asmodee stay in charge of who owns what.** Players keep signing in through Steam and Asmodee, and the
  server never grants a map or anything else a player hasn't bought.
- **The server doesn't enforce entitlements.** It accepts a game on any map a client asks for: the client's own map
  screen enforces ownership. Entitlements are only passed on where the protocol needs them (see below).
- **Every login is checked with Asmodee** ([asmodee.md](protocol/asmodee.md)). There is no other way in.

## What the server keeps

| Data | Kept | Starting value |
|---|---|---|
| Games (lobby, in progress, awaiting result confirmation) and their chat | in memory only; lost on restart, which is acceptable | |
| Lobby chat | in memory only | |
| Karma | persisted | 50 |
| Rating (Elo) and games played | persisted | 1500, 0 |
| Buddy list | persisted, owned by the server | filled from the player's Asmodee buddies on their first login |
| Ignore list | persisted | empty |
| Entitlements (Asmodee `features`) | persisted, refreshed at every login | |
| SteamID (from `AsyncAuth.device`) | persisted, refreshed at every login | |

- **Persisted data** goes through the player store interface. The shipped implementation writes JSON files to the
  data directory ([ARCHITECTURE.md](ARCHITECTURE.md#storage)).
- **Karma and rating belong to this server.** Everyone starts at the defaults above, including players whose buddy
  lists come from Asmodee. Asmodee's old karma and ranking values are never imported.

## Entitlements

At every login the server reads the player's `features` from Asmodee (`user/me`, which the token check calls anyway)
and caches them with the player.

The only place the protocol needs them is `Player.rights` on other players' records. In particular, the buddy list
uses them to decide who can be invited ([social.md](protocol/social.md#buddies)). The server fills `rights` from its
cache and never uses entitlements to allow or refuse anything.

A buddy who has never logged in to this server is treated as having no entitlements, so the Buddies tab won't offer
to invite them. That's intended: someone who never logs in isn't playing on this server.

## Asmodee

The server depends on Asmodee for:

- validating login tokens, and the account id;
- entitlements (`features`), cached at login;
- the buddy list, read once on a player's first login.

Login can't do without Asmodee: the client fetches its token from Asmodee itself before contacting the game server.
Everything else then runs on the server's own data.

The server never writes to Asmodee. Buddy changes stay on the server, even though the original server presumably
updated Asmodee's friend list.

## Open questions

- **Steam invitations.** Filling buddies' `Player.partner_user` with their SteamIDs showed no visible effect in the
  client. Steam invites probably don't need the server; untested.
- **If Asmodee's sign-in ever goes away.** Not planned for. The only route that keeps the client unmodified is
  untested: send `api.asmodee.net` to a stand-in through each player's hosts file. The client checks HTTPS
  certificates there, so every player would also have to trust a certificate.
