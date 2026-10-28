# Documentation

| Document | What's in it |
|---|---|
| [Protocol overview](protocol/README.md) | connecting, framing, the envelope, the message catalogue |
| [Login](protocol/login.md) | sign-in through Asmodee, the game-server login, reconnecting |
| [Lobby, buddies and invitations](protocol/social.md) | lobby, presence, buddies, chat, invitations, open games, observing |
| [Playing a game](protocol/game.md) | from creation to results: turns, moves per map, robots, clocks, time-outs |
| [Asmodee calls](protocol/asmodee.md) | the Asmodee API calls the server makes |
| [Rules engine](RULES.md) | dealing, turns and scoring shared by every map |
| [Maps](maps/README.md) | each board's rules, cities, routes and tickets |
| [Design decisions](DESIGN.md) | what the server keeps, and how it depends on Asmodee |
| [Architecture](ARCHITECTURE.md) | assemblies, storage, configuration, Docker |

The `.proto` files the client and server share are in [`proto/`](../proto).
