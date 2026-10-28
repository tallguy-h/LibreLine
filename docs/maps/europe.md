# Europe

One board, four ticket sets. The routes, cities and cards are the same for all four; only the rules and tickets
differ. The client calls the last three "Europa 1912".

| Map enum | Name | Short name |
|---:|---|---|
| 2 | Europe | eu |
| 10 | Europa 1912 Expanded | e1 |
| 11 | Europa 1912 Mega Game | e2 |
| 12 | Europa 1912 Big Cities | e3 |

## Rules

| Rule | Europe | Expanded | Mega Game | Big Cities |
|---|---|---|---|---|
| Players | 2-5 | 2-5 | 2-5 | 2-5 |
| Trains each | 45 | 45 | 45 | 45 |
| Stations each | 3 | 3 | 3 | 3 |
| Opening hand | 4 cards | 4 cards | 4 cards | 4 cards |
| Opening tickets | 1 long + 3 short, keep 2+ | 1 long + 3 short, keep 2+ | 2 long + 5 short, keep 3+, at most 1 long | 5, keep 2+ |
| Ticket draw | 3 short, keep 1+ | 3 short, keep 1+ | 3 short, keep 1+ | 4, keep 1+ |
| Unkept opening tickets | leave the game | leave the game | leave the game | leave the game |
| Returned tickets | under the deck | under the deck | under the deck | under the deck |
| Double routes | one side only with 2-3 players | same | same | same |
| Locomotives | wild on any route | same | same | same |
| Face-up locomotive | whole turn | whole turn | whole turn | whole turn |
| Face-up row redealt at | 3 locomotives | 3 locomotives | 3 locomotives | 3 locomotives |
| Longest path bonus | 10 | 10 | 10 | none |
| Most tickets bonus | none | none | none | none |
| Rules engine version | 0 | 0 | 0 | 0 |

The client enforces the Mega Game's limit of one long ticket at the opening; keeping none is allowed.

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

Station claims (`CLAIM_STATION.city`) use these ids.

| Id | City |
|---:|---|
| 0 | Edinburgh |
| 1 | London |
| 2 | Dieppe |
| 3 | Brest |
| 4 | Pamplona |
| 5 | Madrid |
| 6 | Lisboa |
| 7 | Cádiz |
| 8 | Amsterdam |
| 9 | Bruxelles |
| 10 | Paris |
| 11 | Barcelona |
| 12 | Essen |
| 13 | Frankfurt |
| 14 | München |
| 15 | Zürich |
| 16 | Marseille |
| 17 | København |
| 18 | Berlin |
| 19 | Wien |
| 20 | Zágráb |
| 21 | Venezia |
| 22 | Roma |
| 23 | Brindisi |
| 24 | Palermo |
| 25 | Stockholm |
| 26 | Danzig |
| 27 | Warszawa |
| 28 | Budapest |
| 29 | Sarajevo |
| 30 | Sofia |
| 31 | Athína |
| 32 | Riga |
| 33 | Wilno |
| 34 | Kyïv |
| 35 | Bucureşti |
| 36 | Constantinople |
| 37 | Smyrna |
| 38 | Petrograd |
| 39 | Smolensk |
| 40 | Sevastopol |
| 41 | Angora |
| 42 | Moskva |
| 43 | Kharkov |
| 44 | Rostov |
| 45 | Sochi |
| 46 | Erzurum |

## Routes

| Id | From | To | Length | Colour | Notes |
|---:|---|---|---:|---|---|
| 0 | Edinburgh | London | 4 | black | |
| 1 | Edinburgh | London | 4 | orange | |
| 2 | London | Dieppe | 2 | grey | ferry 1 |
| 3 | London | Dieppe | 2 | grey | ferry 1 |
| 4 | London | Amsterdam | 2 | grey | ferry 2 |
| 5 | Dieppe | Brest | 2 | orange | |
| 6 | Brest | Paris | 3 | black | |
| 7 | Brest | Pamplona | 4 | purple | |
| 8 | Pamplona | Madrid | 3 | black | tunnel |
| 9 | Pamplona | Madrid | 3 | white | tunnel |
| 10 | Madrid | Lisboa | 3 | purple | |
| 11 | Lisboa | Cádiz | 2 | blue | |
| 12 | Madrid | Cádiz | 3 | orange | |
| 13 | Madrid | Barcelona | 2 | yellow | |
| 14 | Amsterdam | Bruxelles | 1 | black | |
| 15 | Bruxelles | Dieppe | 2 | green | |
| 16 | Dieppe | Paris | 1 | purple | |
| 17 | Bruxelles | Paris | 2 | yellow | |
| 18 | Bruxelles | Paris | 2 | red | |
| 19 | Paris | Pamplona | 4 | blue | |
| 20 | Paris | Pamplona | 4 | green | |
| 21 | Paris | Zürich | 3 | grey | tunnel |
| 22 | Paris | Marseille | 4 | grey | |
| 23 | Pamplona | Marseille | 4 | red | |
| 24 | Pamplona | Barcelona | 2 | grey | tunnel |
| 25 | Barcelona | Marseille | 4 | grey | |
| 26 | Essen | Amsterdam | 3 | yellow | |
| 27 | Essen | Frankfurt | 2 | green | |
| 28 | Amsterdam | Frankfurt | 2 | white | |
| 29 | Bruxelles | Frankfurt | 2 | blue | |
| 30 | Frankfurt | Paris | 3 | white | |
| 31 | Frankfurt | Paris | 3 | orange | |
| 32 | Frankfurt | München | 2 | purple | |
| 33 | München | Zürich | 2 | yellow | tunnel |
| 34 | Zürich | Marseille | 2 | purple | tunnel |
| 35 | Zürich | Venezia | 2 | green | tunnel |
| 36 | Marseille | Roma | 4 | grey | tunnel |
| 37 | København | Essen | 3 | grey | ferry 1 |
| 38 | København | Essen | 3 | grey | ferry 1 |
| 39 | Essen | Berlin | 2 | blue | |
| 40 | Berlin | Frankfurt | 3 | black | |
| 41 | Berlin | Frankfurt | 3 | red | |
| 42 | München | Venezia | 2 | blue | tunnel |
| 43 | Venezia | Roma | 2 | black | |
| 44 | Roma | Palermo | 4 | grey | ferry 1 |
| 45 | Roma | Brindisi | 2 | white | |
| 46 | Brindisi | Palermo | 3 | grey | ferry 1 |
| 47 | Stockholm | København | 3 | yellow | |
| 48 | Stockholm | København | 3 | white | |
| 49 | Danzig | Berlin | 4 | grey | |
| 50 | Berlin | Wien | 3 | green | |
| 51 | Wien | München | 3 | orange | |
| 52 | Wien | Zágráb | 2 | grey | |
| 53 | Venezia | Zágráb | 2 | grey | |
| 54 | Zágráb | Sarajevo | 3 | red | |
| 55 | Riga | Danzig | 3 | black | |
| 56 | Riga | Wilno | 4 | green | |
| 57 | Wilno | Warszawa | 3 | red | |
| 58 | Danzig | Warszawa | 2 | grey | |
| 59 | Warszawa | Berlin | 4 | purple | |
| 60 | Warszawa | Berlin | 4 | yellow | |
| 61 | Warszawa | Wien | 4 | blue | |
| 62 | Wien | Budapest | 1 | red | |
| 63 | Wien | Budapest | 1 | white | |
| 64 | Budapest | Zágráb | 2 | orange | |
| 65 | Budapest | Sarajevo | 3 | purple | |
| 66 | Sarajevo | Athína | 4 | green | |
| 67 | Brindisi | Athína | 4 | grey | ferry 1 |
| 68 | Stockholm | Petrograd | 8 | grey | tunnel |
| 69 | Petrograd | Riga | 4 | grey | |
| 70 | Petrograd | Wilno | 4 | blue | |
| 71 | Wilno | Smolensk | 3 | yellow | |
| 72 | Wilno | Kyïv | 2 | grey | |
| 73 | Kyïv | Warszawa | 4 | grey | |
| 74 | Kyïv | Budapest | 6 | grey | tunnel |
| 75 | Kyïv | Bucureşti | 4 | grey | |
| 76 | Budapest | Bucureşti | 4 | grey | tunnel |
| 77 | Bucureşti | Sofia | 2 | grey | tunnel |
| 78 | Bucureşti | Constantinople | 3 | yellow | |
| 79 | Sofia | Sarajevo | 2 | grey | tunnel |
| 80 | Sofia | Athína | 3 | purple | |
| 81 | Sofia | Constantinople | 3 | blue | |
| 82 | Athína | Smyrna | 2 | grey | ferry 1 |
| 83 | Smyrna | Palermo | 6 | grey | ferry 2 |
| 84 | Petrograd | Moskva | 4 | white | |
| 85 | Moskva | Smolensk | 2 | orange | |
| 86 | Smolensk | Kyïv | 3 | red | |
| 87 | Moskva | Kharkov | 4 | grey | |
| 88 | Kharkov | Kyïv | 4 | grey | |
| 89 | Kharkov | Rostov | 2 | green | |
| 90 | Rostov | Sevastopol | 4 | grey | |
| 91 | Sevastopol | Bucureşti | 4 | white | |
| 92 | Sevastopol | Constantinople | 4 | grey | ferry 2 |
| 93 | Constantinople | Smyrna | 2 | grey | tunnel |
| 94 | Constantinople | Angora | 2 | grey | tunnel |
| 95 | Angora | Smyrna | 3 | orange | tunnel |
| 96 | Sevastopol | Sochi | 2 | grey | ferry 1 |
| 97 | Sevastopol | Erzurum | 4 | grey | ferry 2 |
| 98 | Rostov | Sochi | 2 | grey | |
| 99 | Erzurum | Angora | 3 | black | |
| 100 | Sochi | Erzurum | 3 | red | tunnel |

## Tickets: Europe (enum 2)

| Id | From | To | Points | Notes |
|---:|---|---|---:|---|
| 0 | Amsterdam | Pamplona | 7 | |
| 1 | Amsterdam | Wilno | 12 | |
| 2 | Angora | Kharkov | 10 | |
| 3 | Athína | Angora | 5 | |
| 4 | Athína | Wilno | 11 | |
| 5 | Barcelona | Bruxelles | 8 | |
| 6 | Barcelona | München | 8 | |
| 7 | Berlin | Bucureşti | 8 | |
| 8 | Berlin | Moskva | 12 | |
| 9 | Berlin | Roma | 9 | |
| 10 | Brest | Marseille | 7 | |
| 11 | Brest | Petrograd | 20 | long |
| 12 | Brest | Venezia | 8 | |
| 13 | Bruxelles | Danzig | 9 | |
| 14 | Budapest | Sofia | 5 | |
| 15 | Cádiz | Stockholm | 21 | long |
| 16 | Edinburgh | Athína | 21 | long |
| 17 | Edinburgh | Paris | 7 | |
| 18 | Essen | Kyïv | 10 | |
| 19 | Frankfurt | København | 5 | |
| 20 | Frankfurt | Smolensk | 13 | |
| 21 | København | Erzurum | 21 | long |
| 22 | Kyïv | Petrograd | 6 | |
| 23 | Kyïv | Sochi | 8 | |
| 24 | Lisboa | Danzig | 20 | long |
| 25 | London | Berlin | 7 | |
| 26 | London | Wien | 10 | |
| 27 | Madrid | Dieppe | 8 | |
| 28 | Madrid | Zürich | 8 | |
| 29 | Marseille | Essen | 8 | |
| 30 | Palermo | Constantinople | 8 | |
| 31 | Palermo | Moskva | 20 | long |
| 32 | Paris | Wien | 8 | |
| 33 | Paris | Zágráb | 7 | |
| 34 | Riga | Bucureşti | 10 | |
| 35 | Roma | Smyrna | 8 | |
| 36 | Rostov | Erzurum | 5 | |
| 37 | Sarajevo | Sevastopol | 8 | |
| 38 | Smolensk | Rostov | 8 | |
| 39 | Sofia | Smyrna | 5 | |
| 40 | Stockholm | Wien | 11 | |
| 41 | Venezia | Constantinople | 10 | |
| 42 | Warszawa | Smolensk | 6 | |
| 43 | Zágráb | Brindisi | 6 | |
| 44 | Zürich | Brindisi | 6 | |
| 45 | Zürich | Budapest | 6 | |

## Tickets: Europa 1912 Expanded (enum 10)

| Id | From | To | Points | Notes |
|---:|---|---|---:|---|
| 0 | Amsterdam | Pamplona | 7 | |
| 1 | Amsterdam | Wilno | 12 | |
| 2 | Angora | Kharkov | 10 | |
| 3 | Athína | Angora | 5 | |
| 4 | Athína | Wilno | 11 | |
| 5 | Barcelona | Bruxelles | 8 | |
| 6 | Barcelona | München | 8 | |
| 7 | Berlin | Bucureşti | 8 | |
| 8 | Berlin | Moskva | 12 | |
| 9 | Berlin | Roma | 9 | |
| 10 | Brest | Marseille | 7 | |
| 11 | Brest | Petrograd | 20 | long |
| 12 | Brest | Venezia | 8 | |
| 13 | Bruxelles | Danzig | 9 | |
| 14 | Budapest | Sofia | 5 | |
| 15 | Cádiz | Stockholm | 21 | long |
| 16 | Edinburgh | Athína | 21 | long |
| 17 | Paris | Edinburgh | 7 | |
| 18 | Essen | Kyïv | 10 | |
| 19 | Frankfurt | København | 5 | |
| 20 | Frankfurt | Smolensk | 13 | |
| 21 | København | Erzurum | 21 | long |
| 22 | Kyïv | Petrograd | 6 | |
| 23 | Kyïv | Sochi | 8 | |
| 24 | Lisboa | Danzig | 20 | long |
| 25 | London | Berlin | 7 | |
| 26 | London | Wien | 10 | |
| 27 | Madrid | Dieppe | 8 | |
| 28 | Madrid | Zürich | 8 | |
| 29 | Marseille | Essen | 8 | |
| 30 | Palermo | Constantinople | 8 | |
| 31 | Palermo | Moskva | 20 | long |
| 32 | Paris | Wien | 8 | |
| 33 | Paris | Zágráb | 7 | |
| 34 | Riga | Bucureşti | 10 | |
| 35 | Roma | Smyrna | 8 | |
| 36 | Rostov | Erzurum | 5 | |
| 37 | Sarajevo | Sevastopol | 8 | |
| 38 | Smolensk | Rostov | 8 | |
| 39 | Sofia | Smyrna | 5 | |
| 40 | Wien | Stockholm | 11 | |
| 41 | Venezia | Constantinople | 10 | |
| 42 | Warszawa | Smolensk | 6 | |
| 43 | Zágráb | Brindisi | 6 | |
| 44 | Zürich | Brindisi | 6 | |
| 45 | Zürich | Budapest | 6 | |
| 46 | Amsterdam | Venezia | 6 | |
| 47 | Bruxelles | Stockholm | 10 | |
| 48 | Bucureşti | Erzurum | 7 | |
| 49 | Cádiz | Frankfurt | 13 | |
| 50 | Danzig | Budapest | 7 | |
| 51 | Dieppe | København | 9 | |
| 52 | Dieppe | Marseille | 5 | |
| 53 | Edinburgh | Essen | 9 | |
| 54 | Lisboa | Cádiz | 2 | |
| 55 | München | Petrograd | 14 | |
| 56 | München | Sarajevo | 7 | |
| 57 | Pamplona | Palermo | 12 | |
| 58 | Riga | Kharkov | 10 | |
| 59 | Sochi | Smyrna | 9 | |
| 60 | Sofia | Kyïv | 6 | |
| 61 | Stockholm | Wilno | 12 | |
| 62 | Venezia | Warszawa | 8 | |
| 63 | Warszawa | Budapest | 5 | |
| 64 | Warszawa | Sevastopol | 12 | |

## Tickets: Europa 1912 Mega Game (enum 11)

| Id | From | To | Points | Notes |
|---:|---|---|---:|---|
| 0 | Amsterdam | Pamplona | 7 | |
| 1 | Amsterdam | Wilno | 12 | |
| 2 | Angora | Kharkov | 10 | |
| 3 | Athína | Angora | 5 | |
| 4 | Athína | Wilno | 11 | |
| 5 | Barcelona | Bruxelles | 8 | |
| 6 | Barcelona | München | 8 | |
| 7 | Berlin | Bucureşti | 8 | |
| 8 | Berlin | Moskva | 12 | |
| 9 | Berlin | Roma | 9 | |
| 10 | Brest | Marseille | 7 | |
| 11 | Brest | Venezia | 8 | |
| 12 | Bruxelles | Danzig | 9 | |
| 13 | Budapest | Sofia | 5 | |
| 14 | Paris | Edinburgh | 7 | |
| 15 | Essen | Kyïv | 10 | |
| 16 | Frankfurt | København | 5 | |
| 17 | Frankfurt | Smolensk | 13 | |
| 18 | Kyïv | Petrograd | 6 | |
| 19 | Kyïv | Sochi | 8 | |
| 20 | London | Berlin | 7 | |
| 21 | London | Wien | 10 | |
| 22 | Madrid | Dieppe | 8 | |
| 23 | Madrid | Zürich | 8 | |
| 24 | Marseille | Essen | 8 | |
| 25 | Palermo | Constantinople | 8 | |
| 26 | Paris | Wien | 8 | |
| 27 | Paris | Zágráb | 7 | |
| 28 | Riga | Bucureşti | 10 | |
| 29 | Roma | Smyrna | 8 | |
| 30 | Rostov | Erzurum | 5 | |
| 31 | Sarajevo | Sevastopol | 8 | |
| 32 | Smolensk | Rostov | 8 | |
| 33 | Sofia | Smyrna | 5 | |
| 34 | Wien | Stockholm | 11 | |
| 35 | Venezia | Constantinople | 10 | |
| 36 | Warszawa | Smolensk | 6 | |
| 37 | Zágráb | Brindisi | 6 | |
| 38 | Zürich | Brindisi | 6 | |
| 39 | Zürich | Budapest | 6 | |
| 40 | Amsterdam | Venezia | 6 | |
| 41 | Bruxelles | Stockholm | 10 | |
| 42 | Bucureşti | Erzurum | 7 | |
| 43 | Cádiz | Frankfurt | 13 | |
| 44 | Danzig | Budapest | 7 | |
| 45 | Dieppe | København | 9 | |
| 46 | Dieppe | Marseille | 5 | |
| 47 | Edinburgh | Essen | 9 | |
| 48 | Lisboa | Cádiz | 2 | |
| 49 | München | Petrograd | 14 | |
| 50 | München | Sarajevo | 7 | |
| 51 | Pamplona | Palermo | 12 | |
| 52 | Riga | Kharkov | 10 | |
| 53 | Sochi | Smyrna | 9 | |
| 54 | Sofia | Kyïv | 6 | |
| 55 | Stockholm | Wilno | 12 | |
| 56 | Venezia | Warszawa | 8 | |
| 57 | Warszawa | Budapest | 5 | |
| 58 | Warszawa | Sevastopol | 12 | |
| 59 | Berlin | Wien | 3 | |
| 60 | Berlin | Athína | 11 | |
| 61 | Berlin | Angora | 13 | |
| 62 | London | Madrid | 10 | |
| 63 | London | Athína | 16 | |
| 64 | London | Angora | 20 | |
| 65 | London | Moskva | 19 | |
| 66 | London | Paris | 3 | |
| 67 | London | Roma | 10 | |
| 68 | Madrid | Angora | 21 | |
| 69 | Madrid | Athína | 16 | |
| 70 | Madrid | Berlin | 13 | |
| 71 | Madrid | Moskva | 25 | |
| 72 | Madrid | Roma | 10 | |
| 73 | Madrid | Wien | 13 | |
| 74 | Moskva | Angora | 14 | |
| 75 | Moskva | Athína | 14 | |
| 76 | Paris | Angora | 17 | |
| 77 | Paris | Athína | 13 | |
| 78 | Paris | Berlin | 7 | |
| 79 | Paris | Madrid | 7 | |
| 80 | Paris | Moskva | 18 | |
| 81 | Paris | Roma | 10 | |
| 82 | Roma | Angora | 11 | |
| 83 | Roma | Athína | 6 | |
| 84 | Roma | Moskva | 17 | |
| 85 | Wien | Angora | 10 | |
| 86 | Wien | Athína | 8 | |
| 87 | Wien | Moskva | 12 | |
| 88 | Wien | Roma | 6 | |
| 89 | København | Erzurum | 21 | long |
| 90 | Lisboa | Danzig | 20 | long |
| 91 | Cádiz | Stockholm | 21 | long |
| 92 | Edinburgh | Athína | 21 | long |
| 93 | Brest | Petrograd | 20 | long |
| 94 | Palermo | Moskva | 20 | long |
| 95 | Amsterdam | Rostov | 19 | long |
| 96 | Essen | Angora | 16 | long |
| 97 | London | Sochi | 20 | long |
| 98 | Pamplona | Kyïv | 18 | long |
| 99 | Paris | Sevastopol | 17 | long |
| 100 | Riga | Brindisi | 17 | long |

## Tickets: Europa 1912 Big Cities (enum 12)

| Id | From | To | Points | Notes |
|---:|---|---|---:|---|
| 0 | Angora | Kharkov | 10 | |
| 1 | Athína | Angora | 5 | |
| 2 | Athína | Wilno | 11 | |
| 3 | Berlin | Bucureşti | 8 | |
| 4 | Berlin | Moskva | 12 | |
| 5 | Berlin | Roma | 9 | |
| 6 | Paris | Edinburgh | 7 | |
| 7 | London | Berlin | 7 | |
| 8 | London | Wien | 10 | |
| 9 | Madrid | Dieppe | 8 | |
| 10 | Madrid | Zürich | 8 | |
| 11 | Paris | Wien | 8 | |
| 12 | Paris | Zágráb | 7 | |
| 13 | Roma | Smyrna | 8 | |
| 14 | Wien | Stockholm | 11 | |
| 15 | Berlin | Wien | 3 | |
| 16 | Berlin | Athína | 11 | |
| 17 | Berlin | Angora | 13 | |
| 18 | London | Madrid | 10 | |
| 19 | London | Athína | 16 | |
| 20 | London | Angora | 20 | |
| 21 | London | Moskva | 19 | |
| 22 | London | Paris | 3 | |
| 23 | London | Roma | 10 | |
| 24 | Madrid | Angora | 21 | |
| 25 | Madrid | Athína | 16 | |
| 26 | Madrid | Berlin | 13 | |
| 27 | Madrid | Moskva | 25 | |
| 28 | Madrid | Roma | 10 | |
| 29 | Madrid | Wien | 13 | |
| 30 | Moskva | Angora | 14 | |
| 31 | Moskva | Athína | 14 | |
| 32 | Paris | Angora | 17 | |
| 33 | Paris | Athína | 13 | |
| 34 | Paris | Berlin | 7 | |
| 35 | Paris | Madrid | 7 | |
| 36 | Paris | Moskva | 18 | |
| 37 | Paris | Roma | 10 | |
| 38 | Roma | Angora | 11 | |
| 39 | Roma | Athína | 6 | |
| 40 | Roma | Moskva | 17 | |
| 41 | Wien | Angora | 10 | |
| 42 | Wien | Athína | 8 | |
| 43 | Wien | Moskva | 12 | |
| 44 | Wien | Roma | 6 | |
