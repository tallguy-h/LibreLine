// SPDX-License-Identifier: GPL-2.0-only
using Asmodee.Api;
using LibreLine.Storage.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibreLine.Players.Tests;

internal sealed class FakeAsmodee : IAsmodeeClient
{
    public List<AsmodeeBuddy> Buddies { get; } = [];

    public int BuddyCalls { get; private set; }

    public Task<AsmodeeUser> GetMeAsync(string accessToken, IEnumerable<string> extras, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AsmodeeUser> GetUserAsync(string accessToken, int userId, IEnumerable<string> extras,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<AsmodeeBuddy>> GetBuddiesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        BuddyCalls++;
        return Task.FromResult<IReadOnlyList<AsmodeeBuddy>>(Buddies.ToList());
    }
}

public sealed class ProductCodeTests
{
    [Theory]
    [InlineData("tickettorideUSA_Steam", "OLFT-TTOL-USAM-STEA")]
    [InlineData("tickettorideusa_steam", "OLFT-TTOL-USAM-STEA")]
    [InlineData("tickettorideUSA_Standalone", "OLFT-TTOL-USAM-ALON")]
    [InlineData("tickettorideUSA_Somewhere", "OLFT-TTOL-USAM-ALON")]
    [InlineData("tickettorideUSA_iPad", "OLFT-TTOL-USAM-IPAD")]
    [InlineData("tickettorideEuro", "OLFT-TTOL-EURO")]
    [InlineData("tickettorideEuro_Standalone", "OLFT-TTOL-EURO")]
    [InlineData("tickettorideAsia_Steam", "OLFT-TTOL-ASIA")]
    [InlineData("tickettorideUK", "OLFT-TTOL-UKUK")]
    [InlineData("someOtherGame_Steam", null)]
    public void FeaturesBecomeProductCodes(string feature, string? code)
    {
        Assert.Equal(code, ProductCodes.ForFeature(feature));
    }

    [Fact]
    public void RightsAreDistinct()
    {
        var rights = ProductCodes.ForFeatures(["tickettorideEuro", "tickettorideEuro_Steam", "tickettorideUSA_Steam"]);
        Assert.Equal(2, rights.Count);
        Assert.All(rights, r => Assert.Equal(r.Name, r.Feature));
    }
}

public sealed class PolicyTests
{
    [Fact]
    public void KarmaStaysBetweenZeroAndAHundred()
    {
        var karma = new DefaultKarmaPolicy();
        Assert.Equal(51, karma.AfterFinishing(50));
        Assert.Equal(100, karma.AfterFinishing(100));
        Assert.Equal(45, karma.AfterWithdrawing(50));
        Assert.Equal(0, karma.AfterWithdrawing(3));
    }

    [Fact]
    public void EloBetweenEqualPlayersMovesHalfOfK()
    {
        var ratings = new EloRatingPolicy().Update([(1500, 1), (1500, 2)]);
        Assert.Equal(1516, ratings[0], 6);
        Assert.Equal(1484, ratings[1], 6);
    }

    [Fact]
    public void EloIsZeroSumAndDrawsBetweenEqualsChangeNothing()
    {
        var ratings = new EloRatingPolicy().Update([(1600, 1), (1500, 2), (1400, 3)]);
        Assert.Equal(4500, ratings.Sum(), 6);
        Assert.Equal([1500.0, 1500.0], new EloRatingPolicy().Update([(1500, 1), (1500, 1)]));
    }
}

public sealed class PlayerServiceTests
{
    private static AsmodeeUser User(int id, string name, params string[] features) => new(id, name, "en", "UTC", features, []);

    private static (PlayerService Service, InMemoryPlayerStore Store, FakeAsmodee Asmodee) Create()
    {
        var store = new InMemoryPlayerStore();
        var asmodee = new FakeAsmodee();
        var service = new PlayerService(store, asmodee, new DefaultKarmaPolicy(), new EloRatingPolicy(),
            NullLogger<PlayerService>.Instance);
        return (service, store, asmodee);
    }

    [Fact]
    public async Task FirstLoginCreatesThePlayerWithTheDefaults()
    {
        var (service, store, _) = Create();
        var player = await service.RecordLoginAsync(User(1234567, "alice", "tickettorideUSA_Steam"), "76561190000000001", []);

        Assert.Equal(50, player.Karma);
        Assert.Equal(1500, player.Rating);
        Assert.Equal("76561190000000001", player.SteamId);
        Assert.NotNull(await store.GetAsync(1234567));
        Assert.Equal(["OLFT-TTOL-USAM-STEA"], service.RightsOf(1234567).Select(r => r.Name));
    }

    [Fact]
    public async Task BuddiesAreImportedOnceAndThenOwnedByTheServer()
    {
        var (service, _, asmodee) = Create();
        Assert.True(service.NeedsBuddyImport(1));
        asmodee.Buddies.Add(new AsmodeeBuddy(2, "bob"));
        var buddies = await service.FetchAsmodeeBuddiesAsync("token");
        await service.RecordLoginAsync(User(1, "alice"), null, buddies);
        Assert.False(service.NeedsBuddyImport(1));

        await service.RemoveBuddyAsync(1, 2);
        await service.RecordLoginAsync(User(1, "alice"), null, [new AsmodeeBuddy(2, "bob")]);
        Assert.Empty(service.Find(1)!.Buddies);
    }

    [Fact]
    public async Task ABuddyWhoNeverLoggedInHasNoRightsButKeepsTheirName()
    {
        var (service, _, _) = Create();
        await service.RecordLoginAsync(User(1, "alice"), null, [new AsmodeeBuddy(2, "bob")]);
        Assert.Empty(service.RightsOf(2));
        Assert.Equal("bob", service.NameOf(2));
    }

    [Fact]
    public async Task EntitlementsAreRefreshedAtEveryLogin()
    {
        var (service, _, _) = Create();
        await service.RecordLoginAsync(User(1, "alice", "tickettorideUSA_Steam"), null, []);
        await service.RecordLoginAsync(User(1, "alice", "tickettorideUSA_Steam", "tickettorideEuro"), null, null);
        Assert.Equal(2, service.RightsOf(1).Count);
    }

    [Fact]
    public async Task ARatedGameUpdatesRatingsGamesAndKarma()
    {
        var (service, _, _) = Create();
        await service.RecordLoginAsync(User(1, "alice"), null, []);
        await service.RecordLoginAsync(User(2, "bob"), null, []);
        var changes = await service.RecordRatedGameAsync([new GameResult(1, 1, false), new GameResult(2, 2, true)]);

        Assert.Equal(1516, service.Find(1)!.Rating);
        Assert.Equal(1484, service.Find(2)!.Rating);
        Assert.Equal(51, service.Find(1)!.Karma);
        Assert.Equal(50, service.Find(2)!.Karma);
        Assert.All(changes, c => Assert.Equal(1, c.NewGames));
        Assert.Equal(1, service.RankOf(1));
        Assert.Equal(2, service.RankOf(2));
    }

    [Fact]
    public async Task IgnoringIsPerPlayer()
    {
        var (service, _, _) = Create();
        await service.RecordLoginAsync(User(1, "alice"), null, []);
        await service.AddIgnoreAsync(1, 2);
        Assert.True(service.IsIgnoring(1, 2));
        Assert.False(service.IsIgnoring(2, 1));
    }
}

public sealed class JsonPlayerStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "libreline-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PlayersRoundTripThroughJsonFiles()
    {
        var store = new JsonPlayerStore(directory, NullLogger<JsonPlayerStore>.Instance);
        var player = new PlayerRecord
        {
            AccountId = 42,
            Name = "carol",
            SteamId = "76561190000000042",
            Features = ["tickettorideEuro"],
            Karma = 61,
            Rating = 1544.5,
            GamesPlayed = 7,
            Buddies = [new ContactEntry(43, "dave")],
            BuddiesImported = true,
        };
        await store.SaveAsync(player);

        Assert.True(File.Exists(Path.Combine(directory, "players", "42.json")));
        Assert.Empty(Directory.GetFiles(Path.Combine(directory, "players"), "*.tmp"));

        var loaded = await new JsonPlayerStore(directory, NullLogger<JsonPlayerStore>.Instance).GetAsync(42);
        Assert.NotNull(loaded);
        Assert.Equal("carol", loaded.Name);
        Assert.Equal(1544.5, loaded.Rating);
        Assert.Equal([new ContactEntry(43, "dave")], loaded.Buddies);
        Assert.Single(await store.ListAsync());
    }

    [Fact]
    public async Task AnUnreadableFileIsSkipped()
    {
        var store = new JsonPlayerStore(directory, NullLogger<JsonPlayerStore>.Instance);
        await File.WriteAllTextAsync(Path.Combine(directory, "players", "7.json"), "{ not json");
        Assert.Empty(await store.ListAsync());
    }
}
