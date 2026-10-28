# Germany

The digital board follows *Zug um Zug: Deutschland*: no passengers, and players choose how many short and long
tickets to draw.

| Map enum | Name | Short name |
|---:|---|---|
| 14 | Germany | de |

## Rules

| Rule | Germany |
|---|---|
| Players | 2-5 |
| Trains each | 45 |
| Opening hand | 4 cards |
| Opening tickets | 4, split between short and long as the player chooses; keep 2+ |
| Ticket draw | 4, split as the player chooses; keep 1+ |
| Unkept opening tickets | under the deck they came from |
| Returned tickets | under the deck they came from |
| Double routes | one side only with 2-3 players |
| Locomotives | wild on any route |
| Face-up locomotive | whole turn |
| Face-up row redealt at | 3 locomotives |
| Longest path bonus | none |
| Most tickets bonus | 15 |
| Rules engine version | 2 |

There is no limit on how many long tickets a player keeps. The opening is described in [RULES.md](../RULES.md)
(Germany's opening). That returned tickets go under their own deck is inferred. A mid-game draw of 0 short + 4 long
has been seen.

The board has triple routes (Dortmund-Düsseldorf, Düsseldorf-Köln). How many of the three are open in a 2-3
player game hasn't been seen.

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

## Cities

The first five are the neighbouring countries, each a single place on the board.

| Id | City |
|---:|---|
| 0 | Dänemark |
| 1 | Niederlande |
| 2 | Frankreich |
| 3 | Schweiz |
| 4 | Österreich |
| 5 | Kiel |
| 6 | Rostock |
| 7 | Schwerin |
| 8 | Bremerhaven |
| 9 | Hamburg |
| 10 | Emden |
| 11 | Bremen |
| 12 | Berlin |
| 13 | Hannover |
| 14 | Münster |
| 15 | Magdeburg |
| 16 | Dortmund |
| 17 | Leipzig |
| 18 | Düsseldorf |
| 19 | Kassel |
| 20 | Dresden |
| 21 | Erfurt |
| 22 | Köln |
| 23 | Chemnitz |
| 24 | Koblenz |
| 25 | Frankfurt |
| 26 | Würzburg |
| 27 | Mainz |
| 28 | Nürnberg |
| 29 | Mannheim |
| 30 | Saarbrücken |
| 31 | Regensburg |
| 32 | Karlsruhe |
| 33 | Stuttgart |
| 34 | Ulm |
| 35 | Augsburg |
| 36 | München |
| 37 | Freiburg |
| 38 | Konstanz |
| 39 | Lindau |

## Routes

| Id | From | To | Length | Colour | Notes |
|---:|---|---|---:|---|---|
| 0 | Dänemark | Bremerhaven | 5 | green | |
| 1 | Dänemark | Kiel | 2 | grey | |
| 2 | Kiel | Rostock | 4 | orange | |
| 3 | Kiel | Schwerin | 3 | yellow | |
| 4 | Kiel | Hamburg | 2 | purple | |
| 5 | Kiel | Hamburg | 2 | black | |
| 6 | Kiel | Bremerhaven | 3 | grey | |
| 7 | Bremerhaven | Emden | 3 | grey | |
| 8 | Bremerhaven | Hamburg | 3 | grey | |
| 9 | Bremerhaven | Bremen | 1 | white | |
| 10 | Rostock | Schwerin | 2 | red | |
| 11 | Rostock | Berlin | 6 | purple | |
| 12 | Niederlande | Emden | 2 | white | |
| 13 | Emden | Münster | 4 | red | |
| 14 | Emden | Bremen | 3 | blue | |
| 15 | Bremen | Münster | 3 | black | |
| 16 | Bremen | Hannover | 3 | yellow | |
| 17 | Hamburg | Hannover | 4 | red | |
| 18 | Hamburg | Hannover | 4 | white | |
| 19 | Hamburg | Berlin | 7 | yellow | |
| 20 | Hamburg | Berlin | 7 | blue | |
| 21 | Schwerin | Hamburg | 2 | green | |
| 22 | Schwerin | Berlin | 5 | white | |
| 23 | Hamburg | Bremen | 3 | orange | |
| 24 | Niederlande | Münster | 2 | orange | |
| 25 | Niederlande | Düsseldorf | 3 | purple | |
| 26 | Münster | Dortmund | 1 | grey | |
| 27 | Münster | Dortmund | 1 | grey | |
| 28 | Hannover | Münster | 4 | purple | |
| 29 | Hannover | Kassel | 3 | grey | |
| 30 | Hannover | Kassel | 3 | grey | |
| 31 | Hannover | Erfurt | 5 | orange | |
| 32 | Hannover | Erfurt | 5 | green | |
| 33 | Hannover | Magdeburg | 4 | blue | |
| 34 | Magdeburg | Leipzig | 2 | yellow | |
| 35 | Berlin | Magdeburg | 3 | red | |
| 36 | Berlin | Hannover | 7 | black | |
| 37 | Berlin | Leipzig | 4 | orange | |
| 38 | Berlin | Dresden | 5 | green | |
| 39 | Dortmund | Düsseldorf | 1 | grey | |
| 40 | Dortmund | Düsseldorf | 1 | grey | |
| 41 | Dortmund | Düsseldorf | 1 | grey | |
| 42 | Dortmund | Kassel | 4 | green | |
| 43 | Düsseldorf | Köln | 1 | grey | |
| 44 | Düsseldorf | Köln | 1 | grey | |
| 45 | Düsseldorf | Köln | 1 | grey | |
| 46 | Kassel | Frankfurt | 4 | blue | |
| 47 | Kassel | Frankfurt | 4 | white | |
| 48 | Kassel | Erfurt | 3 | grey | |
| 49 | Erfurt | Nürnberg | 4 | yellow | |
| 50 | Erfurt | Nürnberg | 4 | purple | |
| 51 | Erfurt | Regensburg | 7 | white | |
| 52 | Erfurt | Chemnitz | 4 | black | |
| 53 | Leipzig | Erfurt | 3 | red | |
| 54 | Leipzig | Chemnitz | 2 | blue | |
| 55 | Leipzig | Dresden | 3 | black | |
| 56 | Dresden | Chemnitz | 1 | yellow | |
| 57 | Dresden | Regensburg | 7 | red | |
| 58 | Chemnitz | Regensburg | 6 | purple | |
| 59 | Köln | Koblenz | 1 | grey | |
| 60 | Köln | Koblenz | 1 | grey | |
| 61 | Köln | Frankfurt | 4 | grey | |
| 62 | Köln | Frankfurt | 4 | grey | |
| 63 | Frankfurt | Mainz | 1 | grey | |
| 64 | Frankfurt | Mainz | 1 | grey | |
| 65 | Frankfurt | Mannheim | 2 | grey | |
| 66 | Frankfurt | Mannheim | 2 | grey | |
| 67 | Frankfurt | Würzburg | 2 | grey | |
| 68 | Frankfurt | Würzburg | 2 | grey | |
| 69 | Koblenz | Saarbrücken | 3 | grey | |
| 70 | Koblenz | Mainz | 2 | grey | |
| 71 | Koblenz | Mainz | 2 | grey | |
| 72 | Mainz | Saarbrücken | 3 | grey | |
| 73 | Mainz | Mannheim | 1 | grey | |
| 74 | Mainz | Mannheim | 1 | grey | |
| 75 | Mannheim | Saarbrücken | 3 | grey | |
| 76 | Mannheim | Karlsruhe | 1 | blue | |
| 77 | Mannheim | Stuttgart | 2 | grey | |
| 78 | Mannheim | Stuttgart | 2 | grey | |
| 79 | Würzburg | Nürnberg | 2 | grey | |
| 80 | Würzburg | Nürnberg | 2 | grey | |
| 81 | Nürnberg | Augsburg | 4 | orange | |
| 82 | Nürnberg | München | 5 | blue | |
| 83 | Nürnberg | München | 5 | black | |
| 84 | Nürnberg | Regensburg | 3 | green | |
| 85 | Regensburg | München | 3 | orange | |
| 86 | Regensburg | Österreich | 4 | yellow | |
| 87 | Frankreich | Saarbrücken | 1 | green | |
| 88 | Saarbrücken | Karlsruhe | 3 | grey | |
| 89 | Frankreich | Karlsruhe | 2 | black | |
| 90 | Karlsruhe | Freiburg | 3 | white | |
| 91 | Karlsruhe | Stuttgart | 1 | purple | |
| 92 | Stuttgart | Freiburg | 3 | grey | |
| 93 | Stuttgart | Konstanz | 3 | green | |
| 94 | Stuttgart | Ulm | 2 | grey | |
| 95 | Stuttgart | Ulm | 2 | grey | |
| 96 | Ulm | Lindau | 2 | red | |
| 97 | Ulm | Augsburg | 1 | grey | |
| 98 | Ulm | Augsburg | 1 | grey | |
| 99 | Augsburg | München | 2 | grey | |
| 100 | Augsburg | München | 2 | grey | |
| 101 | Österreich | München | 3 | red | |
| 102 | München | Lindau | 5 | grey | |
| 103 | Frankreich | Freiburg | 2 | yellow | |
| 104 | Schweiz | Freiburg | 2 | orange | |
| 105 | Freiburg | Konstanz | 2 | black | |
| 106 | Schweiz | Konstanz | 1 | white | |
| 107 | Konstanz | Lindau | 1 | yellow | |
| 108 | Schweiz | Lindau | 2 | blue | |
| 109 | Österreich | Lindau | 2 | purple | |

## Tickets (enum 14)

| Id | From | To | Points | Notes |
|---:|---|---|---:|---|
| 0 | Bremen | Berlin | 10 | |
| 1 | Dänemark | Niederlande | 10 | |
| 2 | Berlin | Chemnitz | 6 | |
| 3 | Bremen | Düsseldorf | 5 | |
| 4 | Bremen | Kassel | 6 | |
| 5 | Dortmund | Erfurt | 7 | |
| 6 | Dortmund | Mannheim | 6 | |
| 7 | Erfurt | Würzburg | 6 | |
| 8 | Hamburg | Rostock | 4 | |
| 9 | Hannover | Leipzig | 6 | |
| 10 | Karlsruhe | Augsburg | 4 | |
| 11 | Köln | Stuttgart | 6 | |
| 12 | Mainz | Stuttgart | 3 | |
| 13 | Mannheim | Würzburg | 4 | |
| 14 | Dortmund | Magdeburg | 9 | |
| 15 | Düsseldorf | Konstanz | 10 | |
| 16 | Frankfurt | Lindau | 8 | |
| 17 | Frankfurt | München | 9 | |
| 18 | Frankfurt | Österreich | 10 | |
| 19 | Frankreich | München | 8 | |
| 20 | Hamburg | Frankfurt | 11 | |
| 21 | Hamburg | Köln | 9 | |
| 22 | Hannover | Saarbrücken | 11 | |
| 23 | Karlsruhe | Regensburg | 9 | |
| 24 | Kassel | Freiburg | 10 | |
| 25 | Koblenz | Ulm | 7 | |
| 26 | Köln | Nürnberg | 8 | |
| 27 | Köln | Schweiz | 10 | |
| 28 | Berlin | Düsseldorf | 13 | long |
| 29 | Berlin | Frankfurt | 14 | long |
| 30 | Berlin | Mainz | 15 | long |
| 31 | Berlin | München | 15 | long |
| 32 | Berlin | Schweiz | 20 | long |
| 33 | Bremerhaven | Frankreich | 12 | long |
| 34 | Bremerhaven | Regensburg | 16 | long |
| 35 | Dänemark | Lindau | 22 | long |
| 36 | Dänemark | Regensburg | 20 | long |
| 37 | Dortmund | München | 13 | long |
| 38 | Dresden | Augsburg | 12 | long |
| 39 | Dresden | Saarbrücken | 16 | long |
| 40 | Emden | Freiburg | 15 | long |
| 41 | Emden | Österreich | 19 | long |
| 42 | Frankreich | Dänemark | 17 | long |
| 43 | Frankreich | Leipzig | 15 | long |
| 44 | Hamburg | Dresden | 12 | long |
| 45 | Hamburg | München | 18 | long |
| 46 | Hamburg | Stuttgart | 15 | long |
| 47 | Kiel | Nürnberg | 15 | long |
| 48 | Kiel | Schweiz | 20 | long |
| 49 | Köln | Leipzig | 12 | long |
| 50 | Leipzig | Ulm | 12 | long |
| 51 | Münster | München | 14 | long |
| 52 | Münster | Österreich | 15 | long |
| 53 | Niederlande | Berlin | 13 | long |
| 54 | Rostock | Konstanz | 22 | long |
| 55 | Rostock | Österreich | 22 | long |
| 56 | Schwerin | Koblenz | 12 | long |
| 57 | Leipzig | Nürnberg | 7 | |
| 58 | Münster | Stuttgart | 9 | |
| 59 | Niederlande | Frankfurt | 8 | |
| 60 | Niederlande | Karlsruhe | 9 | |
| 61 | Nürnberg | Stuttgart | 7 | |
