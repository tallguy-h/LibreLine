# Rules engine

The client runs the game itself. The server only stores and relays its moves (see [protocol/game.md](protocol/game.md)).
Even so, the server keeps its own model of every game, for three reasons:

- to check each move before relaying it;
- because only the client that makes the last move reports scores, the server has to score everyone else itself;
- to end games the client never reports, such as one finished by a robot.

This page covers the rules shared by every map. Each map's own rules and data are in [maps/](maps/README.md). The
model below has been checked against the real client on every multiplayer map: each game replayed with no rule
violations and the client's final scores matched.

## Numbering

| Thing | On the wire | Base |
|---|---|---|
| Map | `pb.Settings.map_enum_index` | see [maps/](maps/README.md) |
| Route, ticket, city, track piece | index into the map's lists | 0 |
| Train card | index into the deck, colour by colour (USA: 0-11 purple ... 96-109 locomotive) | 0 |
| Face-up slot | `PICK_OPEN_TRAIN_CARD.slot` | 1 (slots 1-5) |
| Player in an event | `pb.Event.player` | 1 |
| Player in `T2RMulticast.player`, `T2RCommit.PlayerState.player_id`, `pb.Settings.first_player` | | 0 |

## Random numbers

Everything random in a game comes from `pb.Settings.seed`, so every client, and the server, can rebuild every deck and
hand.

- **Generator:** `minstd_rand` (multiplier 48271, modulus 2^31 − 1). The state starts at `seed mod 0x7fffffff` (as
  an unsigned 32-bit value), with 0 replaced by 1.
- **A number in `[lo, hi]`:** let `r = hi − lo` and `bucket = 0x7ffffffe / (r + 1)` (integer division). Repeat
  `v = (next() − 1) / bucket` until `v ≤ r`, then return `lo + v`. When `r = 0` nothing is drawn from the generator.

## Setting up

Done in this order:

1. **Short ticket deck.** Repeatedly pick `uniform(0, n − 1)` from the tickets not yet placed (in map order), take
   that ticket and append it to the deck. Tickets are drawn from the front.
2. **Long ticket deck** (maps with long tickets): built the same way from the long tickets, right after the short
   deck.
3. **Train deck.** It starts in id order and is never shuffled. Every draw takes the card at `uniform(0, count − 1)`.
   Before drawing `n` cards, if `n` is at least the number of cards left and the discard pile isn't empty, the
   discard pile is appended to the deck (no random numbers used).
4. **Face-up row.** Draw 5 cards (after the reshuffle check), then apply the locomotive check below.
5. **Opening hands**, player by player in seat order. Each player first gets any special cards (UK: the lowest
   locomotive id still in the deck), then the reshuffle check, then the map's number of random cards.
6. **Opening tickets**, player by player: the map's short tickets, then its long ones.

**Germany's opening is different.** Nothing is dealt in step 6. Each player first states how many short and long
tickets they want (4 in total). The tickets are then dealt from the tops of the two decks in seat order, whatever
order the players asked in. So a player can't know their tickets until every earlier seat has asked.

**Locomotive check.** While the face-up row holds the map's threshold of locomotives or more (3 on most maps),
discard all five and draw five more.

## A turn

On their turn a player does one of the following.

### Draw train cards

Two cards, each either face-up or from the deck.

- **Taking a face-up card:** the reshuffle check for 1 card, draw the replacement, give the chosen card, then the
  locomotive check.
- **Face-up locomotives:** on most maps a face-up locomotive is the whole turn and can't be the second card. On
  Switzerland and Nordic Countries it is an ordinary card.
- **Drawing from an empty deck** is illegal.
- **Empty face-up slots** are refilled at the end of every turn, reshuffling the discard pile in first.

### Claim a route

The cards paid must cover the route's length:

- all one colour: the route's colour, or any single colour on a grey route;
- locomotives are wild unless the map restricts them (Switzerland: only on tunnels; Nordic: only on tunnels and
  ferries);
- a ferry needs at least its number of locomotives;
- the claim uses length + mountains train pieces (Asia).

A player can't claim a route if:

- they don't have the trains;
- they already own the other half of a double route;
- the game has no more players than the map's double-route threshold and the other half is taken.

**Tunnels.**

1. At claim time three cards are revealed from the train deck. If the deck holds fewer, only what it holds is
   revealed (after the reshuffle check); with none, nothing is revealed.
2. Each revealed card that matches the claim's colour, or is a locomotive, costs one more card of that kind. A claim
   paid only with locomotives costs extra only for revealed locomotives.
3. If there is no extra cost, the claim is complete.
4. Otherwise the player pays the extra (`DIG_TUNNEL` with the cards) or gives up (`DIG_TUNNEL` with no cards). On
   giving up, the base cards go back to the hand.
5. Discard order: the revealed cards, then the base payment, then the extra payment. If the player gave up, only the
   revealed cards are discarded.

### Claim a station (Europe)

Pay 1, 2, then 3 cards of one colour for a player's first, second and third station (the third hasn't been seen
yet). At the end, each station lets its owner use one route of another player's into that city for their tickets.

### Draw tickets

Draw the map's number, keep at least the map's minimum. Unkept tickets go under their deck, or out of the game on
Switzerland and Nordic Countries. On maps with long tickets, normal draws are short only, except Germany, where the
player chooses the split.

## End of the game

When a player ends a turn with 2 trains or fewer, every player, that one included, has one more turn. The game then
ends.

## Scoring

| Route length | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Points | 1 | 2 | 4 | 7 | 10 | 15 | 18 | 21 | 27 |

The UK's 10-space Southampton-New York route scores 40.

- **Tickets:** a completed ticket adds its points; an uncompleted one subtracts them.
  - A ticket with several destinations scores the best one connected; if none is connected, it subtracts the
    lowest.
  - Tickets may use routes borrowed by stations.
- **Unused stations:** 4 each.
- **Mountains (Asia):** 2 each, on the player's claimed routes.
- **Longest path bonus:** to the player(s) with the longest continuous path. Each route counts once; cities may
  repeat.
- **Most tickets bonus:** to every player tied for the most completed tickets, even when that is zero.
- **Explorer bonus (Asia):** to the player(s) with the most cities in one connected network.
- **Mandala bonus (India):** see [maps/india.md](maps/india.md).
- **Stock shares (Pennsylvania):** see [maps/pennsylvania.md](maps/pennsylvania.md).
- **Technologies (UK):** see [maps/uk.md](maps/uk.md).

Ranks follow the scores. For ties the printed rules are used (most completed tickets, then longest path), which
hasn't been seen on the wire yet.

## Not yet seen in a real game

- double routes in 2-3 player games;
- a third station;
- ties for ranks and for the bonuses;
- Switzerland's "parallel route open" flag;
- France's pass without a track piece;
- Pennsylvania with more than two players (is there still a dummy shareholder?);
- UK Risky Contracts.
