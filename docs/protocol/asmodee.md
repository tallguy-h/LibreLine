# Asmodee calls made by the server

The game server calls Asmodee's public API (`https://api.asmodee.net/`) for three things:

- checking login tokens;
- reading a player's entitlements at login;
- reading their buddy list on their first login. Every call uses the player's own access token, the one that
arrived in `AsyncAuth`: `Authorization: Bearer <token>`, `Accept: application/json`.

Responses are wrapped as `{"data": {...}, "error": false, "status": 200}`. An invalid or expired token gives `401`.

## Who is this? `GET main/v1/user/me`

Query `extras=features` (a comma list; `onlinegames`, `boardgames`, `partners`, `userapps` also work).

| `data.user` field | Use |
|---|---|
| `user_id` | the account id: `Player.w_w_w_id` |
| `login_name` | the player's name; should match `AsyncAuth.name` |
| `features` | the player's purchases, e.g. `["tickettorideUSA_Steam", "tickettorideEuro", ...]` |
| `partners` | (with `extras=partners`) linked accounts, e.g. `{partner_id: 12, partner_user_id: <SteamID64>}`; 12 is Steam. Only returned for the caller's own account. |

The response also holds email and other personal fields. The server has no use for them and shouldn't keep them.

## Someone else's profile: `GET main/v1/user/{id}`

Same query and fields, for any account's public profile. Linked accounts (`partners`) are not returned for other
users. The server doesn't currently need it: buddies' entitlements come from what they cached at their own login.

## Buddy list: `GET main/v1/user/me/buddies`

Query `offset`, `limit` (e.g. `offset=0&limit=20`). Response: `data.total`, `data.buddies[] {id, login_name}`. Page
through until `total` is reached. The client itself never calls this. The server calls it once, on a player's first
login, to fill their buddy list. After that the list belongs to the server.

## From features to `Player.rights`

The client decides who owns Ticket to Ride (and so who can be invited from the Buddies tab) from `Player.rights`.
Each entry is `{name: code, feature: code}` with the client's own product codes. Features carry a platform after an
underscore (`tickettorideEuro_Standalone`); for maps the suffix doesn't matter.

| Asmodee feature | Product code |
|---|---|
| `tickettorideUSA_Steam` | `OLFT-TTOL-USAM-STEA` |
| `tickettorideUSA_Standalone` (and unknown platforms) | `OLFT-TTOL-USAM-ALON` |
| `tickettorideUSA_Google` | `OLFT-TTOL-USAM-GOOG` |
| `tickettorideUSA_Kindle` | `OLFT-TTOL-USAM-KNDL` |
| `tickettorideUSA_Pocket` | `OLFT-TTOL-USAM-POCK` |
| `tickettorideUSA_iPad` | `OLFT-TTOL-USAM-IPAD` |
| `tickettoride1910` | `OLFT-TTOL-1910` |
| `tickettorideEuro` | `OLFT-TTOL-EURO` |
| `tickettorideSwiss` | `OLFT-TTOL-SWIS` |
| `tickettorideAsia` | `OLFT-TTOL-ASIA` |
| `tickettorideIndia` | `OLFT-TTOL-INDI` |
| `tickettorideNordic` | `OLFT-TTOL-NORD` |
| `tickettoridePennsylvania` | `OLFT-TTOL-PENN` |
| `tickettorideUK` | `OLFT-TTOL-UKUK` |
| `tickettorideFrance` | `OLFT-TTOL-FRAN` |
| `tickettorideGermany` | `OLFT-TTOL-GERM` |
| `tickettorideOW` | `OLFT-TTOL-OWOW` |
| `tickettorideMiniUSA` | `OLFT-TTOL-MIUS` |
| `tickettoridePolska` | `OLFT-TTOL-POLS` |

Match feature names case-insensitively. The base-game and Steam names are confirmed. The map feature names are
inferred from the client and were confirmed only for Euro and Asia.

The server caches each player's features at login and only passes them on as `rights`. It never adds an
entitlement a player doesn't have, and never uses entitlements to allow or refuse anything. The client's map screen
already checks the player's own entitlements.
