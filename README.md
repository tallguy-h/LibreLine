# Libre Line

*Some tickets don't expire.*

A community online server compatible with ***Ticket to Ride* Classic** for PC (published by Asmodee under licence
from Days of Wonder). The game's official online server has been shut down. This server lets people who own the game
play online again, with their friends or in the lobby.

> **This project is unofficial and unaffiliated.** It is not endorsed, authorised or supported by Asmodee or Days of
> Wonder. *Ticket to Ride* is their trademark, used here only to say what this server is compatible with. The project
> exists only for compatibility and preservation, in the spirit of the
> [Stop Killing Games](https://www.stopkillinggames.com/) initiative.

- **No changes to the game.** Players add one line to the game's settings file. The game itself, Steam and the
  Asmodee sign-in stay exactly as they are.
- **Nothing is unlocked.** Players sign in with their own Asmodee accounts, and the maps they can play are the ones
  they own. The server contains none of the game's files.

## Running a server

With Docker, build the image and start it:

```bash
docker build -t libreline .
```

```bash
docker run -d --name libreline -p 2445:2445 -v libreline-data:/data libreline
```

Or run it directly with the .NET 10 SDK: `dotnet run --project src/LibreLine.Host`. Settings are described in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#configuration).

Players reach the server on TCP port 2445. To let friends connect over the internet, open or forward that port to
the machine running the server, and give them its public address or host name.

## Connecting the game

Each player tells their copy of the game which server to use. This is a one-line change to the game's settings file;
the game itself isn't modified.

1. **Start the game once** if it has never been run on this PC, so that it creates its settings file. Then **quit
   the game completely.** The game writes to this file itself, so only edit it while the game is closed.
2. **Open the settings file.**
   1. Press <kbd>Win</kbd>+<kbd>R</kbd> and enter `%LOCALAPPDATA%\Days of Wonder\Ticket to Ride`.
   2. In the folder that opens, right-click `UserDefault.xml` and open it with Notepad.
3. **Add the server's address.** Find the line `<userDefaultRoot>` near the top, and add this line straight after
   it, with your server's address in place of `your.server.example`:

   ```xml
   <customAsyncServerName>your.server.example:2445</customAsyncServerName>
   ```

   The address can be a host name or an IP address. `:2445` is the port, which can be left off if the server uses
   the standard 2445. If the server runs on the same PC as the game, use `127.0.0.1:2445`.

   That part of the file then looks like this (the other lines vary):

   ```xml
   <userDefaultRoot>
       <customAsyncServerName>your.server.example:2445</customAsyncServerName>
       ...
   </userDefaultRoot>
   ```

4. **Save the file** and start the game. Sign in and go online as usual: Steam and the Asmodee account work exactly
   as before.

Things to know:

- **Use `customAsyncServerName`, not `asyncServerName`.** The game has both settings, but with `asyncServerName` it
  only accepts the original official server and refuses to connect to any other.
- **No certificate warning.** The game doesn't check the certificate of a server set with `customAsyncServerName`,
  so the server's self-signed certificate is fine and there is nothing to install.
- **Only one setting changes.** Nothing else on the PC is touched: no hosts file, no certificates, no patched files.
- **To go back,** quit the game, delete the line again and save.

**If it doesn't connect:**

- Check that the game was fully closed while you edited the file, and that the line is still there afterwards.
- Check the address and port, and that the server is running and port 2445 is reachable from your PC.
- The game writes logs to `%TEMP%\Days of Wonder\Ticket to Ride\` (`info.log` and `network.log`). These show which
  server it tried and why the connection failed.

## Documentation

How the game talks to its server, the rules the server checks, and how the server is built: see
[docs/](docs/README.md).

## Status

The protocol and the rules of every multiplayer map have been worked out and checked against the real game.
The server described in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) is implemented, with unit tests for the rules
and scripted tests of the protocol sequences over TLS (`dotnet test`). It has not yet been played against the real
client.

## Licence

Licensed under the GNU General Public License, version 2 only (`GPL-2.0-only`). See [LICENSE](LICENSE).
