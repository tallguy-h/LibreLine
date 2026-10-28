# France

France is not in the client's text list of maps, but it is drawn on the map screen and can be picked.

| Map enum | Name | Short name |
|---:|---|---|
| 16 | France | fr |

## Rules

| Rule | France |
|---|---|
| Players | 2-5 |
| Trains each | 40 |
| Opening hand | 8 cards |
| Opening tickets | 5, keep 3+ |
| Ticket draw | 4, keep 1+ |
| Unkept opening tickets | under the deck |
| Returned tickets | under the deck |
| Double routes | one side only with 2-3 players |
| Locomotives | wild on any route |
| Face-up locomotive | whole turn |
| Face-up row redealt at | 3 locomotives |
| Track pieces | see below |
| Longest path bonus | 10 |
| Most tickets bonus | 15 |
| Rules engine version | 1 |

**Track pieces.** Most routes are blank: they can't be claimed until someone lays a coloured track piece of the same
length on them, which gives the route that colour. A turn spent drawing train cards stays open after the draws.
It ends with `PUT_TRACK_PIECE_ON_ROUTE{route, track_piece}`, a commit of its own with its own `action_index`.
`PASS_TURN_WITHOUT_PUTTING_TRACK_PIECE_ON_ROUTE` presumably ends it when no piece fits (not yet seen). Pieces run out
by colour and length. Claiming a painted route returns its piece to the supply.

**Crossing routes.** Some routes are drawn across others. Once one of them is painted, the routes it crosses can no
longer be painted or claimed. A claimed route still blocks them after its piece has gone back to the supply. That
painting blocks is confirmed; that claiming blocks is inferred.

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
| 96-109 | locomotive |

## Track pieces

`PUT_TRACK_PIECE_ON_ROUTE.track_piece` uses these ids.

| Ids | Colour | Length |
|---|---|---:|
| 0 | red | 5 |
| 1 | red | 4 |
| 2-4 | red | 3 |
| 5-7 | red | 2 |
| 8 | green | 5 |
| 9 | green | 4 |
| 10-12 | green | 3 |
| 13-15 | green | 2 |
| 16 | blue | 5 |
| 17 | blue | 4 |
| 18-20 | blue | 3 |
| 21-23 | blue | 2 |
| 24 | white | 5 |
| 25 | white | 4 |
| 26-28 | white | 3 |
| 29-31 | white | 2 |
| 32 | black | 5 |
| 33 | black | 4 |
| 34-36 | black | 3 |
| 37-39 | black | 2 |
| 40 | orange | 5 |
| 41 | orange | 4 |
| 42-44 | orange | 3 |
| 45-47 | orange | 2 |
| 48 | purple | 5 |
| 49 | purple | 4 |
| 50-52 | purple | 3 |
| 53-55 | purple | 2 |
| 56 | yellow | 5 |
| 57 | yellow | 4 |
| 58-60 | yellow | 3 |
| 61-63 | yellow | 2 |

## Cities

The first six are places off the map, each a single place on the board.

| Id | City |
|---:|---|
| 0 | Corsica |
| 1 | Italy |
| 2 | Spain |
| 3 | Belgium |
| 4 | Germany |
| 5 | Switzerland |
| 6 | Calais |
| 7 | Cherbourg |
| 8 | Lille |
| 9 | Brest |
| 10 | Le Havre |
| 11 | Amiens |
| 12 | Saint-Malo |
| 13 | Rouen |
| 14 | Rennes |
| 15 | Lorient |
| 16 | Reims |
| 17 | Paris |
| 18 | Le Mans |
| 19 | Metz |
| 20 | Nantes |
| 21 | Angers |
| 22 | Orléans |
| 23 | Nancy |
| 24 | Tours |
| 25 | Strasbourg |
| 26 | La Rochelle |
| 27 | Bourges |
| 28 | Poitiers |
| 29 | Mulhouse |
| 30 | Dijon |
| 31 | Besançon |
| 32 | Limoges |
| 33 | Clermont-Ferrand |
| 34 | Bordeaux |
| 35 | Brive-la-Gaillarde |
| 36 | Lyon |
| 37 | Bayonne |
| 38 | Grenoble |
| 39 | Rodez |
| 40 | Pau |
| 41 | Briançon |
| 42 | Toulouse |
| 43 | Avignon |
| 44 | Montpellier |
| 45 | Perpignan |
| 46 | Nice |
| 47 | Marseille |

## Routes

Colour "blank": the route needs a track piece first. "Crosses": the routes drawn across this one.

| Id | From | To | Length | Colour | Notes |
|---:|---|---|---:|---|---|
| 0 | Bourges | Poitiers | 3 | blank | |
| 1 | Bourges | Orléans | 2 | blank | |
| 2 | Orléans | Tours | 1 | yellow | |
| 3 | Orléans | Tours | 1 | black | |
| 4 | Orléans | Tours | 1 | white | |
| 5 | Orléans | Paris | 1 | orange | |
| 6 | Orléans | Paris | 1 | blue | |
| 7 | Angers | Le Mans | 1 | blue | |
| 8 | Brive-la-Gaillarde | Limoges | 1 | blue | |
| 9 | Le Mans | Tours | 1 | orange | |
| 10 | Angers | Nantes | 1 | orange | |
| 11 | Mulhouse | Strasbourg | 1 | orange | |
| 12 | Belgium | Lille | 1 | green | |
| 13 | Metz | Nancy | 1 | green | |
| 14 | Angers | Tours | 1 | green | |
| 15 | Rennes | Saint-Malo | 1 | yellow | |
| 16 | Germany | Metz | 1 | yellow | |
| 17 | Briançon | Grenoble | 1 | yellow | |
| 18 | Le Havre | Rouen | 1 | purple | |
| 19 | Orléans | Paris | 1 | purple | |
| 20 | Angers | Tours | 1 | purple | |
| 21 | Germany | Strasbourg | 1 | purple | |
| 22 | Metz | Nancy | 1 | black | |
| 23 | Bayonne | Pau | 1 | black | |
| 24 | Amiens | Rouen | 1 | white | |
| 25 | Besançon | Dijon | 1 | white | |
| 26 | Paris | Rouen | 1 | red | |
| 27 | Angers | Nantes | 1 | red | |
| 28 | Germany | Strasbourg | 1 | red | |
| 29 | Calais | Cherbourg | 6 | grey | ferry 1 |
| 30 | Brest | Cherbourg | 6 | grey | ferry 1 |
| 31 | Corsica | Perpignan | 5 | grey | ferry 1 |
| 32 | Corsica | Marseille | 3 | grey | ferry 1 |
| 33 | Corsica | Nice | 2 | grey | ferry 1 |
| 34 | Avignon | Toulouse | 6 | grey | crosses 124 |
| 35 | Belgium | Calais | 2 | blank | |
| 36 | Calais | Lille | 2 | blank | |
| 37 | Calais | Le Havre | 4 | blank | |
| 38 | Calais | Rouen | 3 | blank | |
| 39 | Amiens | Calais | 2 | blank | |
| 40 | Cherbourg | Le Havre | 2 | blank | |
| 41 | Cherbourg | Saint-Malo | 2 | blank | |
| 42 | Brest | Saint-Malo | 3 | blank | |
| 43 | Amiens | Lille | 2 | blank | |
| 44 | Amiens | Lille | 2 | blank | |
| 45 | Belgium | Reims | 4 | blank | |
| 46 | Lille | Reims | 3 | blank | |
| 47 | Brest | Lorient | 2 | blank | |
| 48 | Brest | Lorient | 2 | blank | |
| 49 | Rouen | Saint-Malo | 5 | blank | crosses 147, 149 |
| 50 | Lorient | Saint-Malo | 2 | blank | crosses 148 |
| 51 | Le Mans | Rouen | 4 | blank | |
| 52 | Amiens | Reims | 2 | blank | |
| 53 | Amiens | Paris | 2 | blank | |
| 54 | Amiens | Paris | 2 | blank | |
| 55 | Lorient | Rennes | 2 | blank | |
| 56 | Le Mans | Rennes | 2 | blank | |
| 57 | Metz | Reims | 3 | blank | |
| 58 | Paris | Reims | 2 | blank | |
| 59 | Paris | Reims | 2 | blank | |
| 60 | Lorient | Nantes | 2 | blank | |
| 61 | Lorient | Nantes | 2 | blank | |
| 62 | Le Mans | Lorient | 5 | blank | crosses 150, 151 |
| 63 | Le Mans | Paris | 3 | blank | |
| 64 | Nancy | Reims | 3 | blank | |
| 65 | Nancy | Reims | 3 | blank | |
| 66 | Dijon | Paris | 5 | blank | |
| 67 | Dijon | Paris | 5 | blank | |
| 68 | Dijon | Paris | 5 | blank | |
| 69 | Le Mans | Orléans | 3 | blank | |
| 70 | Metz | Strasbourg | 2 | blank | |
| 71 | Nancy | Strasbourg | 2 | blank | |
| 72 | La Rochelle | Nantes | 2 | blank | |
| 73 | La Rochelle | Nantes | 2 | blank | |
| 74 | Angers | Poitiers | 2 | blank | |
| 75 | Dijon | Nancy | 4 | blank | |
| 76 | Dijon | Nancy | 4 | blank | |
| 77 | Poitiers | Tours | 2 | blank | |
| 78 | Poitiers | Tours | 2 | blank | |
| 79 | Poitiers | Tours | 2 | blank | |
| 80 | Nancy | Strasbourg | 2 | blank | |
| 81 | Bourges | Tours | 2 | blank | |
| 82 | La Rochelle | Poitiers | 2 | blank | |
| 83 | Germany | Mulhouse | 2 | blank | |
| 84 | Bourges | Dijon | 4 | blank | |
| 85 | Dijon | Mulhouse | 4 | blank | crosses 152 |
| 86 | Bordeaux | Poitiers | 4 | blank | |
| 87 | Bordeaux | Poitiers | 4 | blank | |
| 88 | Bordeaux | Poitiers | 4 | blank | |
| 89 | Besançon | Mulhouse | 2 | blank | |
| 90 | Limoges | Poitiers | 2 | blank | |
| 91 | Bourges | Clermont-Ferrand | 3 | blank | |
| 92 | Bordeaux | La Rochelle | 3 | blank | |
| 93 | Bordeaux | La Rochelle | 3 | blank | |
| 94 | Dijon | Lyon | 3 | blank | |
| 95 | Mulhouse | Switzerland | 2 | blank | |
| 96 | Dijon | Lyon | 3 | blank | |
| 97 | Dijon | Lyon | 3 | blank | |
| 98 | Besançon | Lyon | 4 | blank | |
| 99 | Besançon | Switzerland | 2 | blank | |
| 100 | Clermont-Ferrand | Limoges | 3 | blank | crosses 153 |
| 101 | Clermont-Ferrand | Lyon | 3 | blank | |
| 102 | Bordeaux | Brive-la-Gaillarde | 3 | blank | |
| 103 | Brive-la-Gaillarde | Clermont-Ferrand | 3 | blank | |
| 104 | Bayonne | Bordeaux | 3 | blank | |
| 105 | Bayonne | Bordeaux | 3 | blank | |
| 106 | Lyon | Switzerland | 3 | blank | |
| 107 | Bordeaux | Toulouse | 4 | blank | |
| 108 | Bordeaux | Toulouse | 4 | blank | |
| 109 | Clermont-Ferrand | Rodez | 3 | blank | |
| 110 | Lyon | Switzerland | 3 | blank | |
| 111 | Bordeaux | Pau | 3 | blank | |
| 112 | Grenoble | Lyon | 2 | blank | |
| 113 | Brive-la-Gaillarde | Toulouse | 3 | blank | |
| 114 | Brive-la-Gaillarde | Rodez | 2 | blank | |
| 115 | Lyon | Rodez | 5 | blank | |
| 116 | Avignon | Lyon | 4 | blank | |
| 117 | Avignon | Lyon | 4 | blank | |
| 118 | Avignon | Lyon | 4 | blank | |
| 119 | Grenoble | Italy | 4 | blank | |
| 120 | Bayonne | Spain | 3 | blank | |
| 121 | Rodez | Toulouse | 2 | blank | |
| 122 | Pau | Toulouse | 3 | blank | |
| 123 | Briançon | Italy | 3 | blank | |
| 124 | Montpellier | Rodez | 2 | blank | crosses 34 |
| 125 | Avignon | Briançon | 3 | blank | crosses 154 |
| 126 | Pau | Spain | 2 | blank | |
| 127 | Montpellier | Toulouse | 4 | blank | |
| 128 | Briançon | Nice | 3 | blank | |
| 129 | Montpellier | Toulouse | 4 | blank | |
| 130 | Spain | Toulouse | 4 | blank | |
| 131 | Spain | Toulouse | 4 | blank | |
| 132 | Avignon | Montpellier | 2 | blank | |
| 133 | Perpignan | Toulouse | 3 | blank | |
| 134 | Avignon | Montpellier | 2 | blank | |
| 135 | Italy | Nice | 3 | blank | |
| 136 | Italy | Nice | 3 | blank | |
| 137 | Montpellier | Perpignan | 2 | blank | |
| 138 | Marseille | Montpellier | 3 | blank | |
| 139 | Marseille | Montpellier | 3 | blank | |
| 140 | Avignon | Marseille | 2 | blank | |
| 141 | Avignon | Marseille | 2 | blank | |
| 142 | Avignon | Marseille | 2 | blank | |
| 143 | Avignon | Nice | 4 | blank | crosses 154, 155 |
| 144 | Marseille | Nice | 2 | blank | |
| 145 | Perpignan | Spain | 2 | blank | |
| 146 | Marseille | Nice | 2 | blank | |
| 147 | Cherbourg | Le Mans | 5 | blank | crosses 49 |
| 148 | Brest | Rennes | 4 | blank | crosses 50 |
| 149 | Le Havre | Le Mans | 4 | blank | crosses 49 |
| 150 | Nantes | Rennes | 2 | blank | crosses 62 |
| 151 | Angers | Rennes | 2 | blank | crosses 62 |
| 152 | Besançon | Nancy | 3 | blank | crosses 85 |
| 153 | Bourges | Brive-la-Gaillarde | 5 | blank | crosses 100 |
| 154 | Grenoble | Marseille | 5 | blank | crosses 125, 143 |
| 155 | Briançon | Marseille | 4 | blank | crosses 143 |
| 156 | Germany | Metz | 1 | blue | |

## Tickets (enum 16)

| Id | From | To | Points |
|---:|---|---|---:|
| 0 | Germany | Avignon | 12 |
| 1 | Germany | Spain | 18 |
| 2 | Amiens | Montpellier | 13 |
| 3 | Angers | Briançon | 12 |
| 4 | Belgium | Brive-la-Gaillarde | 12 |
| 5 | Belgium | Switzerland | 12 |
| 6 | Besançon | Bordeaux | 12 |
| 7 | Bordeaux | Clermont-Ferrand | 6 |
| 8 | Bordeaux | Marseille | 10 |
| 9 | Brest | Belgium | 12 |
| 10 | Brest | Clermont-Ferrand | 11 |
| 11 | Brest | Paris | 9 |
| 12 | Calais | Germany | 9 |
| 13 | Calais | Nantes | 9 |
| 14 | Cherbourg | Limoges | 9 |
| 15 | Cherbourg | Paris | 4 |
| 16 | Dijon | Grenoble | 5 |
| 17 | Dijon | Montpellier | 9 |
| 18 | Spain | Italy | 12 |
| 19 | La Rochelle | Montpellier | 11 |
| 20 | La Rochelle | Toulouse | 7 |
| 21 | Le Havre | Bourges | 5 |
| 22 | Le Havre | Marseille | 16 |
| 23 | Le Mans | Bordeaux | 7 |
| 24 | Le Mans | Switzerland | 10 |
| 25 | Lille | Dijon | 9 |
| 26 | Lille | Poitiers | 8 |
| 27 | Lille | Saint-Malo | 8 |
| 28 | Lorient | Spain | 12 |
| 29 | Lorient | Rodez | 10 |
| 30 | Lyon | Marseille | 6 |
| 31 | Lyon | Toulouse | 7 |
| 32 | Metz | Angers | 8 |
| 33 | Metz | Perpignan | 16 |
| 34 | Mulhouse | Lyon | 6 |
| 35 | Nancy | Marseille | 13 |
| 36 | Nantes | Nice | 15 |
| 37 | Nantes | Pau | 8 |
| 38 | Paris | Bayonne | 11 |
| 39 | Paris | Brive-la-Gaillarde | 7 |
| 40 | Paris | Lyon | 8 |
| 41 | Paris | Marseille | 14 |
| 42 | Paris | Nice | 14 |
| 43 | Paris | Strasbourg | 7 |
| 44 | Reims | Orléans | 3 |
| 45 | Reims | Toulouse | 13 |
| 46 | Rennes | Bayonne | 10 |
| 47 | Rennes | Lyon | 11 |
| 48 | Rennes | Nancy | 10 |
| 49 | Rouen | Grenoble | 11 |
| 50 | Strasbourg | Avignon | 11 |
| 51 | Strasbourg | Corsica | 15 |
| 52 | Strasbourg | Italy | 13 |
| 53 | Strasbourg | Limoges | 13 |
| 54 | Switzerland | Marseille | 9 |
| 55 | Toulouse | Corsica | 8 |
| 56 | Toulouse | Nice | 9 |
| 57 | Tours | Dijon | 6 |
