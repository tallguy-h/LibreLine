# United Kingdom

One board for two games. UK Advanced adds five technologies; everything else is the same.

| Map enum | Name | Short name |
|---:|---|---|
| 13 | United Kingdom | uk |
| 15 | United Kingdom Advanced | ua |

## Rules

| Rule | UK and UK Advanced |
|---|---|
| Players | 2-4 |
| Trains each | 35 |
| Opening hand | 1 locomotive + 4 cards |
| Opening tickets | 5, keep 3+ |
| Ticket draw | 3, keep 1+ |
| Unkept opening tickets | leave the game |
| Returned tickets | under the deck |
| Double routes | one side only with 2 players |
| Locomotives | wild on any route; any 4 cards (3 with Booster) also count as one locomotive |
| Face-up locomotive | whole turn |
| Face-up row redealt at | 4 locomotives (3 don't trigger it) |
| Technologies | see below |
| Longest path bonus | none |
| Most tickets bonus | none |
| Rules engine version | 0 |

**Opening hand.** Each player's locomotive is dealt by card id, not at random: player by player, the lowest
locomotive id still in the deck goes to the player, then that player's 4 random cards are drawn.

**Technologies.** At the start a player can only claim 1- and 2-space routes with both ends in England. Each
technology unlocks more or changes scoring:

- a concession for any route with an end in that region (France counts with Ireland);
- Mechanical Stoker for 3-space routes;
- Superheated Steam Boiler for 4- to 6-space routes (it doesn't unlock 3-space routes);
- Propellers for ferries;
- Southampton-New York (route 61) needs no technology, can be claimed at any time, and scores 40.

`PICK_TECHNOLOGY{technology}` buys one, paid with cards. The price is a number of locomotives. Each can be paid with
a real locomotive or with 4 cards of any colour (3 with Booster). A player buys at most one technology per turn, and
buying one doesn't end the turn. Every turn ends with the player's own `REMOVE_SINGLE_USE_TECHNOLOGIES` move, which
returns used single-use technologies to the stock.

| Id | Technology | Cost | Copies | Effect |
|---:|---|---:|---:|---|
| 0 | Ireland/France Concession | 1 | 4 | routes into Ireland and France |
| 1 | Scotland Concession | 1 | 4 | routes into Scotland |
| 2 | Wales Concession | 1 | 4 | routes into Wales |
| 3 | Mechanical Stoker | 1 | 4 | 3-space routes |
| 4 | Boiler Lagging | 2 | 4 | +1 per route claimed after buying it |
| 5 | Booster | 2 | 4 | any 3 cards count as a locomotive |
| 6 | Propellers | 2 | 4 | ferries |
| 7 | Superheated Steam Boiler | 2 | 4 | 4- to 6-space routes |
| 8 | Steam Turbine | 2 | 4 | +2 per ferry claimed after buying it |
| 9 | Double Heading | 4 | 4 | +2 per completed ticket at the end |
| 10 | Right of Way | 4 | 1 | claim a route another player owns, then return the card |
| 11 | Thermocompressor | 1 | 1 | UK Advanced: claim 2 routes this turn, then return the card |
| 12 | Equalising Beam | 2 | 1 | UK Advanced: +15 for the longest path, -15 otherwise |
| 13 | Risky Contracts | 2 | 1 | UK Advanced: +20 for the most completed tickets, -20 otherwise |
| 14 | Water Tenders | 2 | 2 | UK Advanced: draw 3 cards from the deck instead of 2 |
| 15 | Diesel Power | 3 | 1 | UK Advanced: claims cost 1 card less (at least 1; a ferry's locomotives still apply) |

Equalising Beam and Risky Contracts can't be bought once the train deck has been reshuffled. The basic stock counts
come from the UK panel; that UK Advanced has the same basic stock is assumed.

**Right of Way.** Both players keep the route: the earlier owner loses nothing and can still use it, and each scores
their own claim.

**Paying for a claim.** The cards handed in are: real locomotives, plus groups of 4 cards (3 with Booster) that each
stand in for a locomotive, plus cards all of one colour (the route's, or any one colour on grey). Together they must
cover the route's length, with at least the ferry's number of locomotives. `CLAIM_ROUTE.card` lists every card.

**Still unseen:** Risky Contracts' ±20, and how ties count for Risky Contracts and Equalising Beam.

## Train cards

| Card ids | Colour |
|---|---|
| 0-11 | purple |
| 12-23 | white |
| 24-35 | blue |
| 36-47 | yellow |
| 48-59 | orange |
| 60-71 | black |
| 72-83 | red |
| 84-95 | green |
| 96-115 | locomotive |

## Cities

| Id | City | Region |
|---:|---|---|
| 0 | Carlisle | England |
| 1 | Newcastle | England |
| 2 | Barrow | England |
| 3 | Liverpool | England |
| 4 | Leeds | England |
| 5 | Manchester | England |
| 6 | Hull | England |
| 7 | Nottingham | England |
| 8 | Birmingham | England |
| 9 | Northampton | England |
| 10 | Norwich | England |
| 11 | Cambridge | England |
| 12 | Bristol | England |
| 13 | Penzance | England |
| 14 | Reading | England |
| 15 | Plymouth | England |
| 16 | Ipswich | England |
| 17 | London | England |
| 18 | Southampton | England |
| 19 | Brighton | England |
| 20 | Dover | England |
| 21 | France | |
| 22 | Londonderry | Ireland |
| 23 | Sligo | Ireland |
| 24 | Belfast | Ireland |
| 25 | Galway | Ireland |
| 26 | Dundalk | Ireland |
| 27 | Tullamore | Ireland |
| 28 | Limerick | Ireland |
| 29 | Dublin | Ireland |
| 30 | Cork | Ireland |
| 31 | Rosslare | Ireland |
| 32 | Stornoway | Scotland |
| 33 | Ullapool | Scotland |
| 34 | Wick | Scotland |
| 35 | Inverness | Scotland |
| 36 | Fort William | Scotland |
| 37 | Aberdeen | Scotland |
| 38 | Dundee | Scotland |
| 39 | Glasgow | Scotland |
| 40 | Edinburgh | Scotland |
| 41 | Stranraer | Scotland |
| 42 | Holyhead | Wales |
| 43 | Aberystwyth | Wales |
| 44 | Llandrindod Wells | Wales |
| 45 | Carmarthen | Wales |
| 46 | Cardiff | Wales |
| 47 | New York | |

## Routes

| Id | From | To | Length | Colour | Notes |
|---:|---|---|---:|---|---|
| 0 | Ullapool | Inverness | 1 | orange | |
| 1 | Stornoway | Ullapool | 1 | grey | ferry 1 |
| 2 | Ullapool | Wick | 3 | yellow | |
| 3 | Stornoway | Wick | 5 | grey | ferry 2 |
| 4 | Ullapool | Fort William | 2 | purple | |
| 5 | Stornoway | Fort William | 5 | grey | ferry 1 |
| 6 | Inverness | Fort William | 2 | black | |
| 7 | Wick | Inverness | 2 | red | |
| 8 | Wick | Aberdeen | 3 | grey | ferry 1 |
| 9 | Inverness | Aberdeen | 3 | purple | |
| 10 | Aberdeen | Dundee | 1 | white | |
| 11 | Inverness | Dundee | 3 | blue | |
| 12 | Fort William | Dundee | 3 | green | |
| 13 | Dundee | Edinburgh | 1 | red | |
| 14 | Dundee | Edinburgh | 1 | yellow | |
| 15 | Aberdeen | Edinburgh | 4 | grey | ferry 1 |
| 16 | Glasgow | Edinburgh | 1 | black | |
| 17 | Glasgow | Edinburgh | 1 | blue | |
| 18 | Fort William | Glasgow | 2 | orange | |
| 19 | Glasgow | Stranraer | 2 | red | |
| 20 | Edinburgh | Stranraer | 3 | white | |
| 21 | Londonderry | Fort William | 5 | grey | ferry 1 |
| 22 | Londonderry | Glasgow | 4 | grey | ferry 1 |
| 23 | Londonderry | Stranraer | 3 | grey | ferry 1 |
| 24 | Belfast | Stranraer | 1 | grey | ferry 1 |
| 25 | Belfast | Dundalk | 1 | white | |
| 26 | Belfast | Dundalk | 1 | red | |
| 27 | Londonderry | Dundalk | 3 | purple | |
| 28 | Londonderry | Sligo | 2 | green | |
| 29 | Sligo | Dundalk | 3 | black | |
| 30 | Dundalk | Dublin | 1 | yellow | |
| 31 | Dundalk | Dublin | 1 | blue | |
| 32 | Tullamore | Dublin | 1 | green | |
| 33 | Tullamore | Dublin | 1 | orange | |
| 34 | Sligo | Tullamore | 3 | blue | |
| 35 | Sligo | Galway | 2 | orange | |
| 36 | Galway | Tullamore | 2 | grey | |
| 37 | Galway | Tullamore | 2 | grey | |
| 38 | Galway | Limerick | 1 | yellow | |
| 39 | Tullamore | Limerick | 1 | grey | |
| 40 | Limerick | Cork | 1 | purple | |
| 41 | Tullamore | Cork | 3 | yellow | |
| 42 | Cork | Rosslare | 2 | blue | |
| 43 | Dublin | Rosslare | 2 | white | |
| 44 | Dublin | Rosslare | 2 | black | |
| 45 | Londonderry | Belfast | 2 | orange | |
| 46 | Tullamore | Rosslare | 2 | red | |
| 47 | Rosslare | Carmarthen | 4 | grey | ferry 1 |
| 48 | Rosslare | Aberystwyth | 3 | grey | ferry 1 |
| 49 | Rosslare | Holyhead | 3 | grey | ferry 1 |
| 50 | Dublin | Holyhead | 2 | grey | ferry 1 |
| 51 | Dundalk | Holyhead | 3 | grey | ferry 1 |
| 52 | Holyhead | Aberystwyth | 2 | grey | ferry 1 |
| 53 | Holyhead | Llandrindod Wells | 3 | blue | |
| 54 | Aberystwyth | Llandrindod Wells | 1 | white | |
| 55 | Llandrindod Wells | Cardiff | 1 | purple | |
| 56 | Carmarthen | Cardiff | 1 | red | |
| 57 | Aberystwyth | Carmarthen | 1 | yellow | |
| 58 | Dover | France | 2 | grey | ferry 1 |
| 59 | Southampton | France | 3 | grey | ferry 1 |
| 60 | Southampton | France | 3 | grey | ferry 1 |
| 61 | Southampton | New York | 10 | grey | ferry 3 |
| 62 | Penzance | Cork | 6 | grey | ferry 2 |
| 63 | Barrow | Belfast | 4 | grey | ferry 1 |
| 64 | Penzance | Cardiff | 5 | grey | ferry 1 |
| 65 | Liverpool | Holyhead | 2 | grey | ferry 1 |
| 66 | Birmingham | Cardiff | 3 | blue | |
| 67 | Birmingham | Cardiff | 3 | orange | |
| 68 | Birmingham | Llandrindod Wells | 2 | red | |
| 69 | Manchester | Llandrindod Wells | 3 | green | |
| 70 | Carlisle | Stranraer | 3 | grey | ferry 1 |
| 71 | Carlisle | Edinburgh | 2 | orange | |
| 72 | Newcastle | Edinburgh | 3 | purple | |
| 73 | Newcastle | Edinburgh | 3 | green | |
| 74 | Newcastle | Aberdeen | 6 | grey | ferry 1 |
| 75 | Carlisle | Barrow | 1 | red | |
| 76 | Carlisle | Newcastle | 1 | yellow | |
| 77 | Barrow | Liverpool | 1 | grey | ferry 1 |
| 78 | Barrow | Leeds | 2 | green | |
| 79 | Liverpool | Leeds | 2 | black | |
| 80 | Liverpool | Manchester | 1 | orange | |
| 81 | Liverpool | Manchester | 1 | purple | |
| 82 | Leeds | Manchester | 1 | blue | |
| 83 | Leeds | Manchester | 1 | red | |
| 84 | Newcastle | Leeds | 2 | orange | |
| 85 | Newcastle | Leeds | 2 | white | |
| 86 | Leeds | Hull | 1 | yellow | |
| 87 | Leeds | Nottingham | 2 | purple | |
| 88 | Newcastle | Hull | 5 | grey | ferry 1 |
| 89 | Manchester | Birmingham | 2 | black | |
| 90 | Manchester | Birmingham | 2 | yellow | |
| 91 | Hull | Nottingham | 2 | black | |
| 92 | Hull | Norwich | 4 | grey | |
| 93 | Nottingham | Norwich | 4 | white | |
| 94 | Nottingham | Birmingham | 1 | grey | |
| 95 | Nottingham | Northampton | 1 | orange | |
| 96 | Nottingham | Cambridge | 2 | grey | |
| 97 | Birmingham | Northampton | 1 | green | |
| 98 | Birmingham | Northampton | 1 | white | |
| 99 | Birmingham | Reading | 2 | grey | |
| 100 | Northampton | Cambridge | 1 | grey | |
| 101 | Northampton | Reading | 1 | red | |
| 102 | Northampton | London | 1 | blue | |
| 103 | Northampton | London | 1 | purple | |
| 104 | Norwich | Cambridge | 2 | red | |
| 105 | Cambridge | Ipswich | 1 | black | |
| 106 | Norwich | Ipswich | 1 | green | |
| 107 | Ipswich | London | 1 | white | |
| 108 | Cambridge | London | 1 | orange | |
| 109 | Cambridge | London | 1 | yellow | |
| 110 | Reading | London | 1 | green | |
| 111 | London | Dover | 2 | grey | |
| 112 | London | Brighton | 1 | grey | |
| 113 | London | Southampton | 2 | black | |
| 114 | London | Southampton | 2 | red | |
| 115 | Brighton | Dover | 2 | purple | |
| 116 | Southampton | Brighton | 1 | blue | |
| 117 | Reading | Southampton | 1 | orange | |
| 118 | Bristol | Reading | 2 | white | |
| 119 | Bristol | Southampton | 2 | green | |
| 120 | Bristol | Plymouth | 3 | yellow | |
| 121 | Plymouth | Southampton | 5 | grey | ferry 1 |
| 122 | Penzance | Plymouth | 2 | black | |
| 123 | Penzance | Plymouth | 3 | grey | ferry 1 |
| 124 | Bristol | Cardiff | 1 | grey | ferry 1 |

## Tickets (enums 13, 15)

| Id | From | To | Points |
|---:|---|---|---:|
| 0 | Newcastle | Hull | 3 |
| 1 | Holyhead | Cardiff | 4 |
| 2 | Penzance | London | 10 |
| 3 | Leeds | France | 10 |
| 4 | Londonderry | Birmingham | 15 |
| 5 | Limerick | Cardiff | 12 |
| 6 | Aberdeen | Glasgow | 5 |
| 7 | Glasgow | France | 19 |
| 8 | Aberystwyth | Cardiff | 2 |
| 9 | London | France | 7 |
| 10 | Belfast | Manchester | 9 |
| 11 | Londonderry | Stranraer | 4 |
| 12 | Ullapool | Dundee | 4 |
| 13 | Manchester | London | 6 |
| 14 | Liverpool | Southampton | 6 |
| 15 | Liverpool | Hull | 3 |
| 16 | Sligo | Holyhead | 9 |
| 17 | Rosslare | Carmarthen | 6 |
| 18 | Glasgow | Manchester | 11 |
| 19 | Dundalk | Carlisle | 7 |
| 20 | Nottingham | Ipswich | 3 |
| 21 | Dublin | London | 15 |
| 22 | Northampton | Dover | 3 |
| 23 | Cork | Leeds | 13 |
| 24 | Cardiff | Reading | 4 |
| 25 | Londonderry | Dublin | 6 |
| 26 | Dublin | Cork | 6 |
| 27 | Liverpool | Llandrindod Wells | 6 |
| 28 | Edinburgh | Birmingham | 12 |
| 29 | Cambridge | London | 3 |
| 30 | Cardiff | London | 8 |
| 31 | Stranraer | Tullamore | 6 |
| 32 | Bristol | Southampton | 2 |
| 33 | Manchester | Plymouth | 8 |
| 34 | Newcastle | Southampton | 7 |
| 35 | Manchester | Norwich | 6 |
| 36 | Fort William | Edinburgh | 3 |
| 37 | Wick | Edinburgh | 5 |
| 38 | Wick | Dundee | 4 |
| 39 | Inverness | Leeds | 13 |
| 40 | Galway | Barrow | 12 |
| 41 | Inverness | Belfast | 10 |
| 42 | Southampton | London | 4 |
| 43 | Rosslare | Aberystwyth | 4 |
| 44 | London | Brighton | 3 |
| 45 | Birmingham | London | 4 |
| 46 | Birmingham | Cambridge | 2 |
| 47 | Norwich | Ipswich | 1 |
| 48 | Glasgow | Dublin | 9 |
| 49 | Edinburgh | London | 15 |
| 50 | Stornoway | Aberdeen | 5 |
| 51 | Leeds | Manchester | 1 |
| 52 | Plymouth | Reading | 5 |
| 53 | Leeds | London | 6 |
| 54 | Stornoway | Glasgow | 7 |
| 55 | Belfast | Dublin | 4 |
| 56 | Galway | Dublin | 5 |
