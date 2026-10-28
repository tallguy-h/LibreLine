// SPDX-License-Identifier: GPL-2.0-only
using Asmodee.Api;
using Com.Daysofwonder.Async;
using Google.Protobuf;
using LibreLine.Protocol;
using LibreLine.Rules.Engine;
using LibreLine.Rules.Maps;
using Pb;
using ConnectionError = Com.Daysofwonder.Async.AsyncConnectionErrorRequest.Types.ConnectionError;
using GameMode = Com.Daysofwonder.Async.GameConfiguration.Types.GameMode;
using PingRequest = Com.Daysofwonder.Mm.PingRequest;

namespace LibreLine.Host.Tests;

/// <summary>Short scripted exchanges for the sequences in docs/protocol, over a real TLS socket.</summary>
public sealed class ProtocolSequenceTests
{
    private const int Alice = 1000001;
    private const int Bob = 1000002;

    private static readonly MapCatalog Maps = MapCatalog.LoadEmbedded();

    private static async Task<TestServer> StartAsync()
    {
        var server = await TestServer.StartAsync();
        server.Asmodee.Add("alice-token", Alice, "alice", "tickettorideUSA_Steam");
        server.Asmodee.Add("bob-token", Bob, "bob", "tickettorideUSA_Standalone", "tickettorideEuro");
        return server;
    }

    // ----- Connection and login ------------------------------------------------------------------------------------

    [Fact]
    public async Task LoginAnswersAsyncConnectedWithThePlayersOwnRecord()
    {
        await using var server = await StartAsync();
        var (client, connected) = await server.LoginAsync("alice-token");
        await using var _ = client;

        Assert.True(connected.Session.Id != 0);
        Assert.Equal(1, connected.Player.Id);
        Assert.Equal(Alice, connected.Player.WWWId);
        Assert.Equal("alice", connected.Player.Name);
        Assert.Equal(50, connected.Player.Karma);
        Assert.Equal(1500, connected.Player.RankScore);
    }

    [Fact]
    public async Task ARefusedTokenIsAccessDenied()
    {
        await using var server = await StartAsync();
        await using var client = await server.ConnectAsync();
        var error = await client.RequestAsync<AsyncConnectionErrorRequest>(new AsyncAuthRequest
        {
            Name = "mallory",
            PartnerToken = new PartnerToken { SessionToken = "forged" },
            GameType = "TT",
        });
        Assert.Equal(ConnectionError.AccessDenied, error.Code);
    }

    [Fact]
    public async Task NoTokenNeedsAuthentication()
    {
        await using var server = await StartAsync();
        await using var client = await server.ConnectAsync();
        var error = await client.RequestAsync<AsyncConnectionErrorRequest>(new AsyncAuthRequest { Name = "x", GameType = "TT" });
        Assert.Equal(ConnectionError.NeedAuthentication, error.Code);
    }

    [Fact]
    public async Task PingsAreEchoedWithTheirOwnId()
    {
        await using var server = await StartAsync();
        await using var client = await server.ConnectAsync();
        var echo = await client.RequestAsync<PingRequest>(new PingRequest { Timestamp = 424242 });
        Assert.Equal(424242, echo.Timestamp);
    }

    [Fact]
    public async Task RequestsBeforeLoginAreRefused()
    {
        await using var server = await StartAsync();
        await using var client = await server.ConnectAsync();
        var error = await client.RequestAsync<ErrorRequest>(new WhatsNewPussycatRequest());
        Assert.Equal(ErrorCode.UnknownPlayer, error.Code);
    }

    [Fact]
    public async Task EveryUnknownRequestStillGetsAReply()
    {
        await using var server = await StartAsync();
        var (client, _) = await server.LoginAsync("alice-token");
        await using var _ = client;
        var error = await client.RequestAsync<ErrorRequest>(new EstimateGameEngagementDurationRequest());
        Assert.Equal(ErrorCode.BadRequest, error.Code);
        await client.RequestAsync<GameStatusReportRequest>(new WhatsNewPussycatRequest());
    }

    [Fact]
    public async Task ANewLoginReplacesTheOldConnection()
    {
        await using var server = await StartAsync();
        var (first, _) = await server.LoginAsync("alice-token");
        await using var _ = first;
        var (second, _) = await server.LoginAsync("alice-token");
        await using var __ = second;
        Assert.True(await first.ClosedAsync(TimeSpan.FromSeconds(10)));
        await second.RequestAsync<GameStatusReportRequest>(new WhatsNewPussycatRequest { SummaryDataOnly = true });
    }

    // ----- Lobby, buddies, chat ------------------------------------------------------------------------------------

    [Fact]
    public async Task EnteringTheLobbySendsThePlayerAndGameLists()
    {
        await using var server = await StartAsync();
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var _ = alice;
        await alice.RequestAsync<LobbyEnteredRequest>(new EnterLobbyRequest());
        var players = await alice.PushAsync<LobbyPlayerListRequest>();
        var list = Zlib.Decompress<LobbyPlayerListRequest.Types.PlayerList>(players.PlayerList);
        Assert.Contains(list.Players, p => p.WWWId == Alice);
        var games = await alice.PushAsync<LobbyGameListRequest>();
        Assert.Empty(Zlib.Decompress<GameList>(games.GameList).Games);

        // Someone else connecting resends the whole list.
        var (bob, _) = await server.LoginAsync("bob-token");
        await using var __ = bob;
        var updated = await alice.PushAsync<LobbyPlayerListRequest>(m =>
            Zlib.Decompress<LobbyPlayerListRequest.Types.PlayerList>(m.PlayerList).Players.Count == 2);
        Assert.NotNull(updated);
    }

    [Fact]
    public async Task BuddiesComeFromAsmodeeOnTheFirstLoginWithPositionsAndRights()
    {
        await using var server = await StartAsync();
        server.Asmodee.Buddies[Alice] = [new AsmodeeBuddy(Bob, "bob"), new AsmodeeBuddy(1000003, "carol")];
        var (bob, _) = await server.LoginAsync("bob-token");
        await using var _ = bob;
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var __ = alice;

        var content = await alice.RequestAsync<AsyncBuddyListContentRequest>(new AsyncBuddyListRequest());
        Assert.Equal([1, 2], content.Buddies.Player.Select(p => p.Id));
        Assert.Equal([Bob, 1000003], content.Buddies.Player.Select(p => p.WWWId));
        Assert.Equal([Bob], content.Buddies.Present);
        Assert.Contains(content.Buddies.Player[0].Rights.Rights, r => r.Name == "OLFT-TTOL-USAM-ALON");
        Assert.Empty(content.Buddies.Player[1].Rights.Rights);

        var added = await alice.RequestAsync<AsyncBuddyAddedRequest>(new AsyncBuddyManagementRequest
        {
            Operation = AsyncBuddyManagementRequest.Types.Operation.Add,
            Buddy = new AsyncBuddy { PlayerId = 1000004 },
        });
        Assert.Equal(3, added.AddedBuddy.Id);

        var removed = await alice.RequestAsync<AsyncBuddyRemovedRequest>(new AsyncBuddyManagementRequest
        {
            Operation = AsyncBuddyManagementRequest.Types.Operation.Remove,
            Buddy = new AsyncBuddy { PlayerId = 1000003 },
        });
        Assert.Equal(1000003, removed.RemovedBuddy.PlayerId);
    }

    [Fact]
    public async Task PresenceRegistrationReportsAndPushesChanges()
    {
        await using var server = await StartAsync();
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var _ = alice;
        var update = await alice.RequestAsync<AsyncBuddyPresencePartialUpdateRequest>(new RegisterPresenceRequest { PlayerIds = { Bob } });
        Assert.Equal([Bob], update.Absent);

        var (bob, _) = await server.LoginAsync("bob-token");
        await using var __ = bob;
        var online = await alice.PushAsync<AsyncBuddyPresencePartialUpdateRequest>();
        Assert.Equal([Bob], online.Present);
    }

    [Fact]
    public async Task LobbyChatLeavesTheGameIdAbsent()
    {
        await using var server = await StartAsync();
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var _ = alice;
        await alice.RequestAsync<LobbyEnteredRequest>(new EnterLobbyRequest());
        await alice.SendAsync(new MulticastChatRequest { Text = "<emoticon_7>" });
        var chat = await alice.PushAsync<ClientChatRequest>();
        Assert.False(chat.HasGameId);
        Assert.Equal("<emoticon_7>", chat.Text);
        Assert.Equal(Alice, chat.Sender.WWWId);

        var history = await alice.RequestAsync<ClientChatHistoryRequest>(new GetChatHistoryRequest());
        Assert.False(history.HasGameId);
        Assert.Equal("alice", history.Entry.Single().From);
    }

    [Fact]
    public async Task StopObserveGameIsAlwaysAnswered()
    {
        await using var server = await StartAsync();
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var _ = alice;
        var reply = await alice.RequestAsync<GameObservedRequest>(new StopObserveGameRequest { GameId = 12345 });
        Assert.Equal(GameObservedRequest.Types.Status.Ok, reply.Status);
    }

    // ----- Games ---------------------------------------------------------------------------------------------------

    private static GameConfiguration Configuration(int map, int players, int seed, int firstPlayer = 1,
        GameMode mode = GameMode.Asynchronous, int? rulesVersion = null)
    {
        var settings = new Settings
        {
            Seed = seed,
            MapEnumIndex = map,
            NbrPlayers = players,
            FirstPlayer = firstPlayer - 1,
            RulesEngineVersion = rulesVersion ?? Maps.Find(map)?.Rules.RulesEngineVersion ?? 0,
        };
        return new GameConfiguration
        {
            Name = "test game",
            GameMode = mode,
            Timeout = 2700,
            RequireFirstUserDataRound = true,
            MinPlayers = players,
            MaxPlayers = players,
            FirstPlayer = firstPlayer,
            Data = new GameFilter { OpaqueSettings = settings.ToByteString() }.ToByteString(),
        };
    }

    private static Settings SettingsOf(GameConfiguration configuration) =>
        Settings.Parser.ParseFrom(GameFilter.Parser.ParseFrom(configuration.Data).OpaqueSettings);

    /// <summary>The opening each seat would keep (all its dealt tickets), worked out from the seed.</summary>
    private static T2RPlayerState Opening(Settings settings, int seat)
    {
        var map = Maps.Find(settings.MapEnumIndex)!;
        var game = new RulesGame(map, new GameSettings(settings.Seed, settings.MapEnumIndex, settings.NbrPlayers,
            settings.FirstPlayer, settings.RulesEngineVersion));
        var choice = new Event { Type = Event.Types.Type.ChooseFirstTicketsPrivate, Player = seat };
        choice.Ticket.AddRange(game.Player(seat).PendingTickets);
        return new T2RPlayerState { ChooseFirstTickets = choice };
    }

    private static T2RCommit Commit(Settings settings, IEnumerable<T2RPlayerState> openings, IEnumerable<Event> actions)
    {
        var commit = new T2RCommit { Settings = settings };
        commit.PlayerState.AddRange(openings.Select((o, i) => new T2RCommit.Types.PlayerState
        {
            PlayerId = i,
            ChooseFirstTickets = o.ChooseFirstTickets,
        }));
        commit.Action.AddRange(actions);
        return commit;
    }

    private static Event DeckDraw(int seat) => new() { Type = Event.Types.Type.PickDeckTrainCard, Player = seat };

    private sealed record FriendsGame(TestServer Server, TestClient Alice, TestClient Bob, long GameId, Settings Settings,
        T2RPlayerState[] Openings) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Alice.DisposeAsync();
            await Bob.DisposeAsync();
            await Server.DisposeAsync();
        }
    }

    /// <summary>Alice invites Bob to a two-player USA game; both accept and choose their tickets.</summary>
    private static async Task<FriendsGame> StartFriendsGameAsync(GameMode mode = GameMode.Asynchronous)
    {
        var server = await StartAsync();
        var (alice, _) = await server.LoginAsync("alice-token");
        var (bob, _) = await server.LoginAsync("bob-token");
        var configuration = Configuration(4, 2, 271828, mode: mode);
        var settings = SettingsOf(configuration);

        var created = await alice.RequestAsync<GameCreatedRequest>(new EngageGameWithFriendsRequest
        {
            Friends = { Bob },
            Configuration = configuration,
            InitialState = new T2RSummary().ToByteString(),
        });
        Assert.Equal(1, created.LocalPlayerId);
        Assert.Equal(1, created.InvitedBy);
        var gameId = created.Details.GameId;

        var invited = await bob.PushAsync<GameCreatedRequest>();
        Assert.Equal(2, invited.LocalPlayerId);
        Assert.Equal(1, invited.InvitedBy);
        Assert.Equal([1, 2], invited.Details.Players.Select(p => p.Id));

        var answered = await bob.RequestAsync<InvitationAnsweredRequest>(new AnswerInvitationRequest { GameId = gameId, Accept = true });
        Assert.True(answered.Accept);
        Assert.Equal(2, answered.Invitee.Id);
        await alice.PushAsync<InvitationAnsweredRequest>();

        await alice.PushAsync<UserDataUpdateRequiredRequest>();
        await bob.PushAsync<UserDataUpdateRequiredRequest>();

        var openings = new[] { Opening(settings, 1), Opening(settings, 2) };
        var updated = await alice.RequestAsync<UserDataUpdatedRequest>(new UpdateUserDataRequest
        {
            GameId = gameId,
            PlayerId = 1,
            ExpectMoreData = false,
            UserData = openings[0].ToByteString(),
        });
        Assert.Equal(1, updated.PlayerId);
        await bob.RequestAsync<UserDataUpdatedRequest>(new UpdateUserDataRequest
        {
            GameId = gameId,
            PlayerId = 2,
            ExpectMoreData = false,
            UserData = openings[1].ToByteString(),
        });

        return new FriendsGame(server, alice, bob, gameId, settings, openings);
    }

    [Fact]
    public async Task AFriendsGameGoesFromInvitationToTheFirstTurn()
    {
        await using var game = await StartFriendsGameAsync();
        var turn = await game.Alice.PushAsync<ActionRequiredRequest>();
        Assert.Equal(game.GameId, turn.GameId);
        Assert.Equal([1], turn.NextPlayerIds);
        Assert.True(turn.State.IsEmpty);
        Assert.False(turn.DifferentPlayer);
        Assert.True(turn.PlayerClock.Armed);
        Assert.Equal(2700_000, turn.PlayerClock.Duration);

        // Before the first commit, a report carries the player's own opening choice only.
        var report = await game.Bob.RequestAsync<GameStatusReportRequest>(new WhatsNewPussycatRequest { GameId = game.GameId, SummaryDataOnly = false });
        var status = report.Reports.Single();
        Assert.Equal(GameStatus.InProgress, status.Status);
        Assert.Equal(1, status.TurnId);
        Assert.Equal([2], status.UserData.Select(u => u.PlayerId));
    }

    [Fact]
    public async Task ATurnIsCommittedActionByActionAndPassesOn()
    {
        await using var game = await StartFriendsGameAsync();
        await game.Alice.PushAsync<ActionRequiredRequest>();

        // The move goes to the other player as ClientData, from the acting seat.
        await game.Alice.SendAsync(new MulticastDataRequest
        {
            GameId = game.GameId,
            RecipientIds = { 2 },
            Data = new T2RMulticast { ActionIndex = 2, Player = 0, Action = DeckDraw(1) }.ToByteString(),
        });
        var relayed = await game.Bob.PushAsync<ClientDataRequest>();
        Assert.Equal(1, relayed.Sender.Id);

        var first = await game.Alice.RequestAsync<ActionCommitedRequest>(new CommitActionRequest
        {
            GameId = game.GameId,
            PlayerId = 1,
            NextState = Commit(game.Settings, game.Openings, [DeckDraw(1)]).ToByteString(),
            NextPlayerIds = { 1, 2 },
        });
        Assert.Equal(1, first.TurnIndex);

        // Bob can't commit while Alice is still at the head of the queue.
        var refused = await game.Bob.RequestAsync<ErrorRequest>(new CommitActionRequest
        {
            GameId = game.GameId,
            PlayerId = 2,
            NextState = Commit(game.Settings, game.Openings, [DeckDraw(1), DeckDraw(2)]).ToByteString(),
            NextPlayerIds = { 1, 2 },
        });
        Assert.Equal(ErrorCode.NotYourTurn, refused.Code);

        await game.Alice.RequestAsync<ActionCommitedRequest>(new CommitActionRequest
        {
            GameId = game.GameId,
            PlayerId = 1,
            NextState = Commit(game.Settings, game.Openings, [DeckDraw(1), DeckDraw(1)]).ToByteString(),
            NextPlayerIds = { 2, 1 },
        });
        var bobsTurn = await game.Bob.PushAsync<ActionRequiredRequest>();
        Assert.Equal([2, 1], bobsTurn.NextPlayerIds);
        Assert.Equal(2, bobsTurn.TurnIndex);
        Assert.False(bobsTurn.State.IsEmpty);

        var clocks = await game.Bob.RequestAsync<ClocksStatusRequest>(new GetClocksRequest { GameId = game.GameId });
        Assert.Equal([false, true], clocks.Clock.Select(c => c.Clock.Armed));
    }

    [Fact]
    public async Task AnIllegalMoveIsRefusedWithWrongState()
    {
        await using var game = await StartFriendsGameAsync();
        await game.Alice.PushAsync<ActionRequiredRequest>();

        // Three train cards in one turn.
        var refused = await game.Alice.RequestAsync<ErrorRequest>(new CommitActionRequest
        {
            GameId = game.GameId,
            PlayerId = 1,
            NextState = Commit(game.Settings, game.Openings, [DeckDraw(1), DeckDraw(1), DeckDraw(1)]).ToByteString(),
            NextPlayerIds = { 2, 1 },
        });
        Assert.Equal(ErrorCode.WrongState, refused.Code);
        Assert.Equal(game.GameId, refused.GameId);
    }

    [Fact]
    public async Task AnIllegalOpeningChoiceIsRefused()
    {
        await using var server = await StartAsync();
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var _ = alice;
        var (bob, _) = await server.LoginAsync("bob-token");
        await using var __ = bob;
        var configuration = Configuration(4, 2, 99);
        var created = await alice.RequestAsync<GameCreatedRequest>(new EngageGameWithFriendsRequest { Friends = { Bob }, Configuration = configuration });
        await bob.RequestAsync<InvitationAnsweredRequest>(new AnswerInvitationRequest { GameId = created.Details.GameId, Accept = true });

        var tooFew = Opening(SettingsOf(configuration), 1);
        var keepOne = tooFew.ChooseFirstTickets.Ticket.Take(1).ToList();
        tooFew.ChooseFirstTickets.Ticket.Clear();
        tooFew.ChooseFirstTickets.Ticket.AddRange(keepOne);
        var error = await alice.RequestAsync<ErrorRequest>(new UpdateUserDataRequest
        {
            GameId = created.Details.GameId,
            PlayerId = 1,
            UserData = tooFew.ToByteString(),
        });
        Assert.Equal(ErrorCode.WrongState, error.Code);
    }

    [Fact]
    public async Task AnUnsupportedMapIsABadRequest()
    {
        await using var server = await StartAsync();
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var _ = alice;
        var oldWest = await alice.RequestAsync<ErrorRequest>(new EngageGameWithFriendsRequest { Friends = { Bob }, Configuration = Configuration(17, 2, 1) });
        Assert.Equal(ErrorCode.BadRequest, oldWest.Code);
        var wrongVersion = await alice.RequestAsync<ErrorRequest>(new LobbyCreateGameRequest { Configuration = Configuration(4, 2, 1, rulesVersion: 3) });
        Assert.Equal(ErrorCode.BadRequest, wrongVersion.Code);
    }

    [Fact]
    public async Task ADeclinedInvitationAbortsTheGame()
    {
        await using var server = await StartAsync();
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var _ = alice;
        var created = await alice.RequestAsync<GameCreatedRequest>(new EngageGameWithFriendsRequest { Friends = { Bob }, Configuration = Configuration(4, 2, 5) });

        // Bob is offline: he finds the invitation in his status report, then declines.
        var (bob, _) = await server.LoginAsync("bob-token");
        await using var __ = bob;
        var report = await bob.RequestAsync<GameStatusReportRequest>(new WhatsNewPussycatRequest { SummaryDataOnly = true });
        var pending = report.Reports.Single();
        Assert.Equal(GameStatus.WaitingInvitation, pending.Status);
        Assert.Equal(1, pending.Invitation.InvitedBy);

        await bob.RequestAsync<InvitationAnsweredRequest>(new AnswerInvitationRequest { GameId = created.Details.GameId, Accept = false });
        var answer = await alice.PushAsync<InvitationAnsweredRequest>();
        Assert.False(answer.Accept);
        var after = await alice.RequestAsync<GameStatusReportRequest>(new WhatsNewPussycatRequest());
        Assert.Equal(GameStatus.Aborted, after.Reports.Single().Status);
    }

    [Fact]
    public async Task ALobbyGameStartsWhenFullWithoutInvitedBy()
    {
        await using var server = await StartAsync();
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var _ = alice;
        var (bob, _) = await server.LoginAsync("bob-token");
        await using var __ = bob;
        await bob.RequestAsync<LobbyEnteredRequest>(new EnterLobbyRequest());

        var created = await alice.RequestAsync<LobbyGameCreatedRequest>(new LobbyCreateGameRequest
        {
            Configuration = Configuration(4, 2, 77),
            InitialSummaryData = new T2RSummary().ToByteString(),
        });
        var listed = await bob.PushAsync<LobbyGameListRequest>(l => Zlib.Decompress<GameList>(l.GameList).Games.Count == 1);
        Assert.Equal(created.Game.GameId, Zlib.Decompress<GameList>(listed.GameList).Games[0].GameId);

        var joined = await bob.RequestAsync<LobbyNewPlayerRequest>(new LobbyJoinGameRequest { GameId = created.Game.GameId });
        Assert.Equal(2, joined.JoiningPlayer);
        await alice.PushAsync<LobbyNewPlayerRequest>();

        var aliceStart = await alice.PushAsync<GameCreatedRequest>();
        var bobStart = await bob.PushAsync<GameCreatedRequest>();
        Assert.False(aliceStart.HasInvitedBy);
        Assert.False(bobStart.HasInvitedBy);
        Assert.Equal(1, aliceStart.LocalPlayerId);
        Assert.Equal(2, bobStart.LocalPlayerId);
        await bob.PushAsync<UserDataUpdateRequiredRequest>();

        var again = await bob.RequestAsync<LobbyJoinDeniedRequest>(new LobbyJoinGameRequest { GameId = created.Game.GameId });
        Assert.Equal(LobbyJoinDeniedRequest.Types.JoinError.PlayerDuplicate, again.Cause);
        var unknown = await bob.RequestAsync<LobbyJoinDeniedRequest>(new LobbyJoinGameRequest { GameId = 42 });
        Assert.Equal(LobbyJoinDeniedRequest.Types.JoinError.UnknownGame, unknown.Cause);
    }

    [Fact]
    public async Task WithdrawingFromATwoPlayerGameAbortsIt()
    {
        await using var game = await StartFriendsGameAsync();
        var forfeited = await game.Bob.RequestAsync<GameForfeitedRequest>(new GameForfeitRequest { GameId = game.GameId });
        Assert.Equal(2, forfeited.PlayerId);
        Assert.True(forfeited.Aborted);
        var told = await game.Alice.PushAsync<GameForfeitedRequest>();
        Assert.True(told.Aborted);
    }

    [Fact]
    public async Task AWithdrawnSeatIsPlayedByARobotInTheCreatorsClient()
    {
        await using var server = await StartAsync();
        server.Asmodee.Add("carol-token", 1000003, "carol");
        var (alice, _) = await server.LoginAsync("alice-token");
        await using var _ = alice;
        var (bob, _) = await server.LoginAsync("bob-token");
        await using var __ = bob;
        var (carol, _) = await server.LoginAsync("carol-token");
        await using var ___ = carol;

        var configuration = Configuration(4, 3, 31415);
        var settings = SettingsOf(configuration);
        var gameId = (await alice.RequestAsync<GameCreatedRequest>(new EngageGameWithFriendsRequest
        {
            Friends = { Bob, 1000003 },
            Configuration = configuration,
        })).Details.GameId;
        await bob.RequestAsync<InvitationAnsweredRequest>(new AnswerInvitationRequest { GameId = gameId, Accept = true });
        await carol.RequestAsync<InvitationAnsweredRequest>(new AnswerInvitationRequest { GameId = gameId, Accept = true });
        var openings = new[] { Opening(settings, 1), Opening(settings, 2), Opening(settings, 3) };
        var clients = new[] { alice, bob, carol };
        for (var seat = 1; seat <= 3; seat++)
        {
            await clients[seat - 1].RequestAsync<UserDataUpdatedRequest>(new UpdateUserDataRequest
            {
                GameId = gameId,
                PlayerId = seat,
                UserData = openings[seat - 1].ToByteString(),
            });
        }

        var forfeited = await carol.RequestAsync<GameForfeitedRequest>(new GameForfeitRequest { GameId = gameId });
        Assert.False(forfeited.Aborted);
        var replaced = await alice.PushAsync<PlayerReplacedRequest>();
        Assert.Equal(3, replaced.PlayerId);
        Assert.True(replaced.BecomeRobot);
        Assert.Equal(PlayerTimeoutRequest.Types.PlayerStatus.Forfeit, replaced.Status);

        var actions = new List<Event>();
        async Task Play(TestClient client, int seat, int[] queue)
        {
            actions.Add(DeckDraw(seat));
            var commit = new CommitActionRequest
            {
                GameId = gameId,
                PlayerId = seat,
                NextState = Commit(settings, openings, actions).ToByteString(),
            };
            commit.NextPlayerIds.AddRange(queue);
            await client.RequestAsync<ActionCommitedRequest>(commit);
        }

        await Play(alice, 1, [1, 2, 3]);
        await Play(alice, 1, [2, 3, 1]);
        await Play(bob, 2, [2, 3, 1]);
        await Play(bob, 2, [3, 1, 2]);

        // The robot's turn wakes the engine in the creator's client: the full state, then PlayerTimeout(ROBOT).
        var wake = await alice.PushAsync<ActionRequiredRequest>(a => a.DifferentPlayer);
        Assert.Equal([3, 1, 2], wake.NextPlayerIds);
        Assert.True(wake.Players.Single(p => p.Id == 3).IsBot);
        var robot = await alice.PushAsync<PlayerTimeoutRequest>();
        Assert.Equal(PlayerTimeoutRequest.Types.PlayerStatus.Robot, robot.Status);
        Assert.Equal(3, robot.OffenderId);
        Assert.False(robot.HasState);

        // Bob can't play the robot; Alice, its controller, can. Its move is echoed back to her.
        actions.Add(DeckDraw(3));
        var stolen = await bob.RequestAsync<ErrorRequest>(new CommitActionRequest
        {
            GameId = gameId,
            PlayerId = 3,
            NextState = Commit(settings, openings, actions).ToByteString(),
        });
        Assert.Equal(ErrorCode.NotYourTurn, stolen.Code);
        actions.RemoveAt(actions.Count - 1);

        await Play(alice, 3, [3, 1, 2]);
        await alice.SendAsync(new MulticastDataRequest
        {
            GameId = gameId,
            RecipientIds = { 2 },
            Data = new T2RMulticast { ActionIndex = 7, Player = 2, Action = DeckDraw(3) }.ToByteString(),
        });
        var echo = await alice.PushAsync<ClientDataRequest>();
        Assert.Equal(3, echo.Sender.Id);

        // After the robot's first card it still has the move, so it is woken again.
        await alice.PushAsync<PlayerTimeoutRequest>();
    }

    [Fact]
    public async Task GameChatGoesToEveryoneInTheGame()
    {
        await using var game = await StartFriendsGameAsync();
        await game.Bob.SendAsync(new MulticastChatRequest { GameId = game.GameId, Text = "good luck" });
        var forAlice = await game.Alice.PushAsync<ClientChatRequest>();
        var forBob = await game.Bob.PushAsync<ClientChatRequest>();
        Assert.Equal(game.GameId, forAlice.GameId);
        Assert.Equal(2, forAlice.Sender.Id);
        Assert.Equal("good luck", forBob.Text);

        var history = await game.Alice.RequestAsync<ClientChatHistoryRequest>(new GetChatHistoryRequest { GameId = game.GameId });
        Assert.Equal(2, history.Entry.Single().PlayerId);
    }

    [Fact]
    public async Task PresenceInAGameListsSeats()
    {
        await using var game = await StartFriendsGameAsync();
        await game.Alice.SendAsync(new SwitchedToGameRequest { GameId = game.GameId });
        await game.Alice.SendAsync(new SubscribePresenceServiceRequest());
        var presence = await game.Alice.PushAsync<PlayerPresenceUpdateRequest>(p => p.InGame.Contains(1));
        Assert.Equal([1, 2], presence.Present.Order());
        Assert.Equal([1], presence.InGame);
    }

    [Fact]
    public async Task ComingBackToAGameResendsTheTurn()
    {
        await using var game = await StartFriendsGameAsync();
        await game.Alice.PushAsync<ActionRequiredRequest>();
        await game.Alice.SendAsync(new SwitchedToGameRequest { GameId = game.GameId });
        var again = await game.Alice.PushAsync<ActionRequiredRequest>();
        Assert.Equal(game.GameId, again.GameId);
    }
}
