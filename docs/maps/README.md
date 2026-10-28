# Maps

One file per board. Boards that share routes and cities but have different ticket sets (USA and USA 1910, Europe and
Europa 1912, UK and UK Advanced) share a file.

| Map enum | Short name | Map | File |
|---:|---|---|---|
| 0 | as | Asia | [asia.md](asia.md) |
| 1 | ch | Switzerland | [switzerland.md](switzerland.md) |
| 2 | eu | Europe | [europe.md](europe.md) |
| 3 | in | India | [india.md](india.md) |
| 4 | us | USA | [usa.md](usa.md) |
| 5 | u1 | USA 1910 | [usa.md](usa.md) |
| 6 | u2 | USA 1910 Mega Game | [usa.md](usa.md) |
| 7 | u3 | USA 1910 Big Cities | [usa.md](usa.md) |
| 8 | nc | Nordic Countries | [nordic.md](nordic.md) |
| 9 | pa | Pennsylvania | [pennsylvania.md](pennsylvania.md) |
| 10 | e1 | Europa 1912 Expanded | [europe.md](europe.md) |
| 11 | e2 | Europa 1912 Mega Game | [europe.md](europe.md) |
| 12 | e3 | Europa 1912 Big Cities | [europe.md](europe.md) |
| 13 | uk | United Kingdom | [uk.md](uk.md) |
| 14 | de | Germany | [germany.md](germany.md) |
| 15 | ua | United Kingdom Advanced | [uk.md](uk.md) |
| 16 | fr | France | [france.md](france.md) |
| 17 | ow | Old West | not playable (the client has no data for it) |
| 18 | mu | Mini USA | single-player tutorial only; not documented |
| 19 | po | Polska | not offered by the client's map screen; not documented |

The map enum is `pb.Settings.map_enum_index`. The short name is the last entry of `GameOver.variants`.

Every multiplayer map (0-16) has been played to the end against the real client, and the final scores matched the
rules described here.

## Reading a map file

Each file has the same sections, so the server can load them as its map data.

- **Rules:** a table of the rules that vary between maps. The rules common to every map are in
  [RULES.md](../RULES.md).
- **Train cards:** card ids by colour. Train cards are numbered colour by colour in this order.
- **Track pieces** (France only): track piece ids.
- **Cities:** city ids, used on the wire only by `CLAIM_STATION.city`. The id order also fixes the order of the
  routes and tickets below. A third column gives the UK's regions.
- **Routes:** one row per route, in id order. The id is what `CLAIM_ROUTE.route` and the other route fields carry.
  - *From* and *To* are city names exactly as in the cities table.
  - *Colour* is one of purple, white, blue, yellow, orange, black, red, green, grey (any one colour), or blank (France).
  - *Notes* holds a comma-separated list of:
    - `ferry N`: N of the cards must be locomotives;
    - `tunnel`;
    - `N mountain(s)`;
    - `crosses A, B`: the routes, by id, drawn across this one;
    - `parallel route open`.
  - Pennsylvania adds a *Shares* column.
- **Tickets:** one table per ticket set, in id order. The id is what ticket fields on the wire carry.
  - A ticket with several possible destinations lists them in *To* separated by ` / `, with the points in the same
    order.
  - *Notes* says `long` for a ticket from the long deck.