# Lobby, buddies, invitations and observing

All player ids in these messages follow the rule in the [overview](README.md#the-envelope): `Player.id` is a
position in the list being sent, and the account id is `w_w_w_id`.

## The lobby

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Game server
    participant O as Other online clients

    C->>S: EnterLobby (600)
    S-->>C: LobbyEntered (601)
    S--)C: LobbyPlayerList (604): everyone online
    S--)C: LobbyGameList (609): open games
    S--)O: LobbyPlayerList (604): now including this player
    C->>S: AskPlayerInfo (605) {player}
    S-->>C: LobbyPlayerInfo (606)
    C->>S: ExitLobby (602) (e.g. on opening a game)
    S-->>C: LobbyExited (603)
```

- **`LobbyPlayerList` (604)** is the only way the lobby learns who is online. There are no add or remove messages:
  send the whole list (zlib-compressed `PlayerList`) to everyone whenever someone connects or drops.
- **`LobbyGameList` (609)** works the same way for open games (zlib-compressed `GameList` of `GameDetails`).
- **`AskAnyPlayerInfo` (630) → `PlayerInfo` (631)** looks up any player, online or not.

## Presence

The client shows each player as offline, online, or in this game. It asks for presence in two places.

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Game server

    rect rgba(128, 128, 128, 0.1)
    Note over C,S: Buddy and invite panels
    C->>S: RegisterPresence (527) {w_w_w_ids}
    S-->>C: AsyncBuddyPresencePartialUpdate (517) {present, absent}
    Note over S: later, when one of them connects or drops
    S--)C: AsyncBuddyPresencePartialUpdate (517)
    C->>S: UnregisterPresence (528) (no reply)
    end

    rect rgba(128, 128, 128, 0.1)
    Note over C,S: Game screen and online screen
    C->>S: SwitchedToGame (534) {game_id}
    C->>S: SubscribePresenceService (532) (no reply)
    S--)C: PlayerPresenceUpdate (506) {game_id, present, absent, in_game} (seats)
    Note over S: whenever a player in that game connects, drops, opens or leaves it
    S--)C: PlayerPresenceUpdate (506)
    C->>S: UnsubscribePresenceService (533) (no reply)
    end
```

`PlayerPresenceUpdate` lists **seat numbers**, not account ids. Account ids there crash the client once the game
screen is built. When the player to move drops from "in game" back to online or offline, the client shows "current
player absent".

## Buddies

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Game server

    C->>S: AsyncBuddyList (515)
    S-->>C: AsyncBuddyListContent (516) {player[], present[]}
    C->>S: AsyncBuddyManagement (518) {operation: ADD, buddy {player_id}}
    S-->>C: AsyncBuddyAdded (519) {added_buddy: Player}
    C->>S: AsyncBuddyManagement (518) {operation: REMOVE, buddy {player_id}}
    S-->>C: AsyncBuddyRemoved (520) {removed_buddy}
```

- **Positions.** Each buddy's `Player.id` is its 1-based position in the list. A newly added buddy gets the next
  position.
- **Who can be invited.** The Buddies tab only offers an invite to players who own Ticket to Ride: those whose
  `Player.rights` contains a Days of Wonder product code for the base game:
  - `OLFT-TTOL-USAM-STEA` (Steam);
  - `-ALON` (standalone);
  - `-GOOG`, `-KNDL`, `-IPAD`, `-POCK`.

  The server builds them from each buddy's Asmodee `features`, as cached at that buddy's last login
  ([DESIGN.md](../DESIGN.md#entitlements)). It translates:
  - `tickettorideUSA_Steam` → `OLFT-TTOL-USAM-STEA`;
  - `_Standalone` → `-ALON`.

  The maps have codes too (`OLFT-TTOL-1910`, `-EURO`, `-SWIS`, `-ASIA`, `-INDI`, `-NORD`, `-PENN`, `-UKUK`, `-FRAN`,
  `-GERM`, ...), but they don't decide who can be invited. Without `rights`, nobody can be invited from that tab.
- **Offline buddies can be invited.** Async games don't need the invitee online (see Invitations below).
- **Changes go to the game server.** The client never tells Asmodee about buddy changes, so the server owns the list.

**Ignore list:** `AsyncIgnoreList` (560) → `AsyncIgnoreListContent` (561), and `AsyncIgnoreManagement` (562) →
`AsyncIgnoreAdded` (563) / `AsyncIgnoreRemoved` (564). Same shapes as buddies.

## Chat

```mermaid
sequenceDiagram
    participant A as Sender
    participant S as Game server
    participant B as Other players

    A->>S: MulticastChat (524) {game_id, text} (recipient_ids empty)
    S--)A: ClientChat (525) {game_id, text, sender}
    S--)B: ClientChat (525) {game_id, text, sender}
    A->>S: GetChatHistory (535) {game_id}
    S-->>A: ClientChatHistory (536) {entries: from, text, player_id, timestamp (ms)}
```

- **Recipients.** An empty `recipient_ids` means everyone in the game, the sender included.
- **Lobby chat** has no `game_id`. Leave the field **absent** in `ClientChat` and `ClientChatHistory`: these are
  proto2 messages, and an explicit `game_id: 0` makes the client ignore the line.
- **Emotes** are ordinary text such as `<emoticon_7>`.
- **Blocked chat:** `ClientChatBlocked` (526) exists for that case.

## Invitations (friends games)

The Create Game screen's *Invitation* mode creates a game with chosen players.

```mermaid
sequenceDiagram
    autonumber
    participant A as Creator
    participant S as Game server
    participant B as Invitee (online)
    participant D as Invitee (offline)

    A->>S: EngageGameWithFriends (405)<br/>{friends: [w_w_w_id], configuration, initial_state}
    S-->>A: GameCreated (404)<br/>{local_player_id: 1, invited_by: 1, details}
    S--)B: GameCreated (404)<br/>{local_player_id: own seat, invited_by: 1, details}
    Note over B: accept / decline dialog
    B->>S: AnswerInvitation (540) {game_id, accept: true, user_data}
    S-->>B: InvitationAnswered (541) {game_id, accept, invitee}
    S--)A: InvitationAnswered (541)
    Note over D: logs in later
    D->>S: WhatsNewPussycat (511)
    S-->>D: GameStatusReport (512)<br/>game in WAITING_INVITATION, invitation {invited_by: 1}
    D->>S: AnswerInvitation (540) {accept: true}
    S-->>D: InvitationAnswered (541)
    S--)A: InvitationAnswered (541)
    Note over S: all accepted: the game starts (game.md)
```

- **Configuration.** `configuration` holds the name, `rated`, `lurkable`, `game_mode` (ASYNCHRONOUS), `timeout` (each
  player's time bank, in seconds; 2700 shows as "45 minutes"), `require_first_user_data_round` and `data` (with
  `pb.Settings`).
- **`initial_state`** is a `pb.T2RSummary`, not a game state. Don't send it back as `state`.
- **Seats, not account ids.** `local_player_id` and `invited_by` are seat numbers. The client shows the invitation
  dialog when they differ, so the creator gets `invited_by = local_player_id`.
- **Replying to the creator.** Without a reply to 405 the client gives up after about 30 seconds.
- **Offline invitees** get no push. The stored game shows up in their next status report.
- **A decline** aborts the game.
- **Refusing a game** (for example an unsupported map): reply `Error` (505) `{BAD_REQUEST}`. The client shows "Bad
  request" and recovers.

## Open games and automatch (lobby games)

The lobby's open games and the *Automatch* button both use lobby games. Automatch first searches the open-game list
and joins a match, and only creates a game when it finds none.

```mermaid
sequenceDiagram
    autonumber
    participant A as Creator
    participant S as Game server
    participant B as Joiner
    participant L as Everyone online

    A->>S: LobbyCreateGame (607)<br/>{configuration {private: false, min/max_players, game_mode, timeout, data}, initial_state, initial_summary_data}
    S-->>A: LobbyGameCreated (608) {GameDetails}
    S--)L: LobbyGameList (609): includes the new game
    B->>S: LobbyJoinGame (610) {game_id}
    alt can join
        S-->>B: LobbyNewPlayer (611) {game, joining_player}
        S--)A: LobbyNewPlayer (611)
    else unknown game, already in it, full, karma or rating too low
        S-->>B: LobbyJoinDenied (612) {JoinError}
    end
    Note over S: game full
    S--)A: GameCreated (404) {local_player_id, details} (no invited_by)
    S--)B: GameCreated (404) {local_player_id, details} (no invited_by)
    S--)L: LobbyGameList (609): without the game (not sent to its own players)
```

- **No `invited_by`.** `GameCreated` for a lobby game must not have `invited_by` set, or the waiting screen never
  starts the game.
- **Don't send its players the new list.** Players of the game that just started must not get the updated
  `LobbyGameList`. A list arriving right after `GameCreated` is handled instead of it, and the game never opens.
- **Leaving.** `LobbyLeaveGame` (613) → `LobbyPlayerLeftGame` (614), pushed to the game's other players. If the
  creator leaves, the game is dropped.
- **Lobby games are rated.** Karma and rating change only for lobby games, never for invitation games. `min_karma`
  and `min_rankscore` in the configuration let a creator restrict who can join (`NOT_ENOUGH_KARMA`,
  `NOT_ENOUGH_RANKSCORE`).

**Single-session games.** The Advanced tab's "absent player is immediately replaced by a robot" option creates a
lobby game with `game_mode: SYNCHRONOUS`. It starts the same way. For leaving and resuming, see
[game.md](game.md#leaving-a-single-session-game).

## Observing

Games created with `observable_by` set can be watched from the lobby's Observe tab.

```mermaid
sequenceDiagram
    participant C as Observer
    participant S as Game server

    C->>S: SubscribeToObservableGameList (620) {subscribe: true}
    S-->>C: SubscribedToObservableGameList (621)
    S--)C: ObservableGameList (622) {game_list: zlib GameList}
    Note over C: picks a game
    C->>S: SubscribeToObservableGameList (620) {subscribe: false}
    S-->>C: SubscribedToObservableGameList (621) {subscribed: false}
    C->>S: StartObserveGame (650) {game_id}
    S-->>C: GameObserved (652) {status: OK, game_status: StatusReport}
    loop while watching
        S--)C: ClientData (503): every move, as players receive it
        C->>S: GetClocks (546)
        S-->>C: ClocksStatus (547)
    end
    C->>S: StopObserveGame (651) {game_id}
    S-->>C: GameObserved (652) {game_id, status: OK}
```

- **The reply to `StopObserveGame` is required.** Without it every later request from that client stalls.
- **No cards.** Observers see the board from seat 1 with card counts but no cards. Whether `lurkable` games show
  hands is untested.
- **Untested:** how buddies-only observing is checked.
