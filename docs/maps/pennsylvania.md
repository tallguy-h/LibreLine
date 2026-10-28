# Pennsylvania

| Map enum | Name | Short name |
|---:|---|---|
| 9 | Pennsylvania | pa |

## Rules

| Rule | Pennsylvania |
|---|---|
| Players | 2-5 |
| Trains each | 45 |
| Opening hand | 4 cards |
| Opening tickets | 5, keep 3+ |
| Ticket draw | 4, keep 1+ |
| Unkept opening tickets | under the deck |
| Returned tickets | under the deck |
| Double routes | one side only with 2-3 players |
| Locomotives | wild on any route |
| Face-up locomotive | whole turn |
| Face-up row redealt at | 3 locomotives |
| Stock shares | see below |
| Longest path bonus | none |
| Most tickets bonus | 15 |
| Rules engine version | 1 |

**Stock shares.** Claiming a route that shows railroad logos lets the claimer take one share of one of those
railroads (`BUY_STOCK_SHARE{railroad}`). In a 2-player game the claimer then also picks a share for the dummy
shareholder (`BUY_STOCK_SHARE{railroad, for_dummy: true}`), which ends the turn. A route without logos gives no share,
and the claim alone ends the turn. Whether the dummy exists with more players hasn't been seen.

At the end, each railroad's shareholders are ranked by how many shares they hold and paid by position. The dummy
takes shares out of the supply but doesn't compete for payouts. Tied players are given the same position's payout
(ties not yet seen in a real game).

| Wire id | Railroad | Short | Payouts (1st, 2nd, ...) | Shares |
|---:|---|---|---|---:|
| 0 | Buffalo, Rochester & Pittsburgh | BRP | 7, 4 | |
| 1 | Jersey Central | JC | 8, 5 | |
| 2 | Western Maryland | WM | 9, 5 | |
| 3 | New York Central | NYC | 10, 6, 3 | |
| 4 | Lehigh Valley | LV | 12, 7, 3 | |
| 5 | Reading | RDG | 14, 9, 5 | |
| 6 | Erie | ERIE | 16, 10, 5, 1 | |
| 7 | Baltimore & Ohio | BO | 20, 14, 9, 5, 2 | |
| 8 | Pennsylvania Railroad | PRR | 30, 21, 14, 9, 6 | |

The supply of shares per railroad (last column) isn't known yet.

The railroads offered by each route were read off the board (the in-game board image and its route pop-ups) and
checked against the real client. They are listed in the routes table under "Shares".

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

Ontario appears twice on the board: once across Lake Erie from Erie (west), once next to Buffalo (east).

| Id | City |
|---:|---|
| 0 | Ontario West |
| 1 | Ontario East |
| 2 | Buffalo |
| 3 | Rochester |
| 4 | Syracuse |
| 5 | Albany |
| 6 | Erie |
| 7 | Warren |
| 8 | Coudersport |
| 9 | Elmira |
| 10 | Towanda |
| 11 | Binghamton |
| 12 | Youngstown |
| 13 | Oil City |
| 14 | DuBois |
| 15 | Williamsport |
| 16 | Scranton |
| 17 | New York |
| 18 | Wheeling |
| 19 | Pittsburgh |
| 20 | Johnstown |
| 21 | Altoona |
| 22 | Lewistown |
| 23 | Harrisburg |
| 24 | Lancaster |
| 25 | Reading |
| 26 | Allentown |
| 27 | Stroudsburg |
| 28 | Morgantown |
| 29 | Cumberland |
| 30 | Chambersburg |
| 31 | Gettysburg |
| 32 | York |
| 33 | Baltimore |
| 34 | Philadelphia |
| 35 | Atlantic City |

## Routes

| Id | From | To | Length | Colour | Notes | Shares |
|---:|---|---|---:|---|---|---|
| 0 | Ontario West | Erie | 3 | grey | ferry 2 | PRR, ERIE, NYC |
| 1 | Ontario West | Erie | 3 | grey | ferry 2 | PRR, ERIE, NYC |
| 2 | Erie | Buffalo | 5 | orange | | ERIE, NYC |
| 3 | Erie | Buffalo | 5 | white | | ERIE, NYC |
| 4 | Ontario East | Buffalo | 1 | grey | ferry 1 | PRR, ERIE, NYC |
| 5 | Ontario East | Buffalo | 1 | grey | ferry 1 | PRR, ERIE, NYC |
| 6 | Buffalo | Rochester | 5 | yellow | | ERIE, BO, LV, NYC, BRP |
| 7 | Buffalo | Rochester | 5 | black | | ERIE, BO, LV, NYC, BRP |
| 8 | Rochester | Syracuse | 4 | purple | | LV, NYC |
| 9 | Rochester | Syracuse | 4 | blue | | LV, NYC |
| 10 | Syracuse | Binghamton | 2 | yellow | | ERIE |
| 11 | Syracuse | Binghamton | 2 | orange | | ERIE |
| 12 | Syracuse | Albany | 6 | white | | NYC |
| 13 | Syracuse | Albany | 6 | red | | NYC |
| 14 | Albany | New York | 6 | blue | | NYC, BO, PRR |
| 15 | Albany | New York | 6 | green | | NYC, BO, PRR |
| 16 | Erie | Warren | 3 | blue | | PRR, ERIE, NYC |
| 17 | Erie | Oil City | 3 | black | | PRR, ERIE, NYC |
| 18 | Warren | Buffalo | 4 | green | | BRP, BO, ERIE, PRR |
| 19 | Warren | Coudersport | 4 | grey | | PRR, ERIE, BO, NYC, BRP |
| 20 | Buffalo | Coudersport | 4 | grey | | PRR, ERIE |
| 21 | Coudersport | Elmira | 4 | orange | | PRR, ERIE, BO, NYC, BRP |
| 22 | Coudersport | Williamsport | 4 | green | | PRR |
| 23 | Rochester | Elmira | 3 | green | | PRR, ERIE, BO, LV, NYC, BRP |
| 24 | Elmira | Syracuse | 4 | black | | ERIE, LV |
| 25 | Elmira | Binghamton | 3 | white | | ERIE |
| 26 | Elmira | Towanda | 2 | yellow | | PRR, ERIE, LV |
| 27 | Towanda | Binghamton | 2 | red | | ERIE |
| 28 | Binghamton | Albany | 6 | purple | | none |
| 29 | Binghamton | Scranton | 3 | black | | ERIE |
| 30 | Binghamton | Scranton | 3 | green | | ERIE |
| 31 | Youngstown | Erie | 4 | yellow | | ERIE, NYC |
| 32 | Youngstown | Erie | 4 | green | | ERIE, NYC |
| 33 | Youngstown | Oil City | 3 | white | | ERIE |
| 34 | Youngstown | Pittsburgh | 4 | orange | | PRR, BO, NYC |
| 35 | Youngstown | Pittsburgh | 4 | blue | | PRR, BO, NYC |
| 36 | Youngstown | Wheeling | 5 | purple | | NYC, BO, PRR |
| 37 | Oil City | Warren | 2 | orange | | BO, ERIE, PRR |
| 38 | Oil City | DuBois | 3 | purple | | none |
| 39 | Warren | DuBois | 3 | black | | ERIE, BRP |
| 40 | DuBois | Altoona | 2 | grey | | PRR |
| 41 | DuBois | Williamsport | 6 | white | | none |
| 42 | Williamsport | Towanda | 2 | black | | ERIE, RDG |
| 43 | Towanda | Scranton | 3 | grey | | PRR, ERIE, LV |
| 44 | Williamsport | Scranton | 5 | orange | | none |
| 45 | Scranton | New York | 5 | red | | ERIE, LV, JC |
| 46 | Scranton | New York | 5 | purple | | ERIE, LV, JC |
| 47 | Scranton | Stroudsburg | 2 | yellow | | RDG, LV, JC |
| 48 | Scranton | Allentown | 3 | white | | JC, LV, RDG, PRR |
| 49 | Scranton | Allentown | 3 | blue | | JC, LV, RDG, PRR |
| 50 | New York | Atlantic City | 6 | black | | JC |
| 51 | New York | Atlantic City | 6 | white | | JC |
| 52 | Wheeling | Pittsburgh | 2 | green | | PRR |
| 53 | Wheeling | Pittsburgh | 2 | white | | PRR |
| 54 | Wheeling | Morgantown | 3 | blue | | BO |
| 55 | Pittsburgh | Oil City | 4 | red | | BO, PRR |
| 56 | Pittsburgh | Johnstown | 4 | purple | | PRR, BO |
| 57 | Pittsburgh | Johnstown | 4 | black | | PRR, BO |
| 58 | Pittsburgh | Morgantown | 3 | yellow | | none |
| 59 | Johnstown | Altoona | 1 | yellow | | PRR |
| 60 | Johnstown | Altoona | 1 | blue | | PRR |
| 61 | Johnstown | Cumberland | 3 | grey | | WM, PRR, BO |
| 62 | Altoona | Lewistown | 2 | green | | none |
| 63 | Altoona | Harrisburg | 5 | red | | PRR |
| 64 | Altoona | Harrisburg | 5 | orange | | PRR |
| 65 | Lewistown | Williamsport | 3 | yellow | | RDG, PRR |
| 66 | Lewistown | Harrisburg | 2 | grey | | PRR |
| 67 | Harrisburg | Scranton | 6 | grey | | RDG, PRR |
| 68 | Harrisburg | Reading | 2 | purple | | RDG |
| 69 | Harrisburg | Lancaster | 2 | grey | | PRR |
| 70 | Harrisburg | Lancaster | 2 | grey | | PRR |
| 71 | Harrisburg | York | 1 | black | | WM, PRR |
| 72 | Reading | Allentown | 2 | green | | RDG |
| 73 | Allentown | Stroudsburg | 2 | orange | | JC, LV, RDG |
| 74 | Allentown | Philadelphia | 3 | red | | PRR, RDG |
| 75 | Allentown | Philadelphia | 3 | black | | PRR, RDG |
| 76 | Lancaster | Reading | 1 | yellow | | RDG |
| 77 | Lancaster | Philadelphia | 4 | green | | PRR |
| 78 | Lancaster | Philadelphia | 4 | orange | | PRR |
| 79 | Morgantown | Cumberland | 5 | red | | WM, BO |
| 80 | Cumberland | Chambersburg | 2 | green | | WM, PRR |
| 81 | Cumberland | Baltimore | 7 | blue | | WM, BO |
| 82 | Chambersburg | Harrisburg | 2 | blue | | RDG, WM, PRR |
| 83 | Chambersburg | Gettysburg | 1 | black | | none |
| 84 | Gettysburg | Harrisburg | 2 | yellow | | PRR, RDG |
| 85 | Gettysburg | York | 1 | grey | | WM, PRR |
| 86 | Gettysburg | Baltimore | 3 | red | | WM, PRR |
| 87 | York | Lancaster | 1 | purple | | PRR |
| 88 | York | Baltimore | 2 | white | | WM, PRR |
| 89 | Baltimore | Philadelphia | 4 | yellow | | PRR, BO |
| 90 | Baltimore | Philadelphia | 4 | purple | | PRR, BO |
| 91 | Philadelphia | New York | 6 | grey | | JC, RDG, BO, PRR |
| 92 | Philadelphia | New York | 6 | grey | | JC, RDG, BO, PRR |
| 93 | Philadelphia | Atlantic City | 2 | grey | | PRR, RDG, JC |
| 94 | Philadelphia | Atlantic City | 2 | grey | | PRR, RDG, JC |

## Tickets (enum 9)

Tickets 9-11 can be completed to either Ontario; both score the same.

| Id | From | To | Points |
|---:|---|---|---:|
| 0 | Altoona | Binghamton | 9 |
| 1 | Baltimore | Albany | 16 |
| 2 | Baltimore | New York | 10 |
| 3 | Baltimore | Philadelphia | 4 |
| 4 | Buffalo | Baltimore | 16 |
| 5 | Buffalo | Harrisburg | 13 |
| 6 | Buffalo | Johnstown | 10 |
| 7 | Buffalo | New York | 18 |
| 8 | Buffalo | Philadelphia | 19 |
| 9 | Pittsburgh | Ontario West / Ontario East | 10 / 10 |
| 10 | Syracuse | Ontario West / Ontario East | 10 / 10 |
| 11 | Warren | Ontario West / Ontario East | 5 / 5 |
| 12 | Chambersburg | Scranton | 8 |
| 13 | Coudersport | Binghamton | 7 |
| 14 | Cumberland | Harrisburg | 4 |
| 15 | DuBois | Cumberland | 6 |
| 16 | DuBois | Stroudsburg | 12 |
| 17 | Erie | Albany | 20 |
| 18 | Erie | Altoona | 8 |
| 19 | Gettysburg | Reading | 3 |
| 20 | Harrisburg | Baltimore | 3 |
| 21 | Harrisburg | New York | 11 |
| 22 | Harrisburg | Philadelphia | 6 |
| 23 | Johnstown | Elmira | 10 |
| 24 | Lewistown | Syracuse | 9 |
| 25 | Morgantown | Chambersburg | 7 |
| 26 | Morgantown | Williamsport | 13 |
| 27 | New York | Atlantic City | 6 |
| 28 | Oil City | Johnstown | 6 |
| 29 | Oil City | Scranton | 14 |
| 30 | Philadelphia | Atlantic City | 2 |
| 31 | Philadelphia | New York | 6 |
| 32 | Pittsburgh | Baltimore | 13 |
| 33 | Pittsburgh | Buffalo | 10 |
| 34 | Pittsburgh | Harrisburg | 9 |
| 35 | Pittsburgh | New York | 20 |
| 36 | Pittsburgh | Philadelphia | 15 |
| 37 | Rochester | Elmira | 3 |
| 38 | Rochester | Reading | 13 |
| 39 | Scranton | Allentown | 3 |
| 40 | Syracuse | Allentown | 8 |
| 41 | Towanda | Lancaster | 9 |
| 42 | Warren | York | 10 |
| 43 | Wheeling | Albany | 22 |
| 44 | Wheeling | Allentown | 15 |
| 45 | Wheeling | Erie | 9 |
| 46 | Williamsport | Albany | 10 |
| 47 | Youngstown | Morgantown | 7 |
| 48 | Youngstown | Rochester | 14 |
| 49 | Youngstown | Scranton | 17 |
