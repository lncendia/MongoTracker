using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Tracker;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

/// <summary>
/// Integration tests for keyed sets nested in keyed sets (seasons → episodes → versions), executed against a real
/// MongoDB server, because only the server resolves filtered positional operators ($[identifier]) with array filters.
/// </summary>
/// <remarks>
/// The server is taken from the MONGOTRACKER_TEST_MONGO environment variable (mongodb://localhost:27017 by default).
/// When it is not reachable, the tests are ignored.
/// </remarks>
[Category("Integration")]
public partial class NestedKeyedSetMongoTests
{
  private const string ConnectionVariable = "MONGOTRACKER_TEST_MONGO";

  private ModelBuilder _builder = null!;
  private MongoClient _client = null!;
  private string _databaseName = null!;
  private IMongoCollection<Film> _films = null!;

  [OneTimeSetUp]
  public void Initialize()
  {
    BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

    _builder = new ModelBuilder();
    _builder.Entity<Film>(b =>
    {
      b.Property(f => f.Id).IsIdentifier();
      b.Property(f => f.Seasons).IsTrackedSet();
    });
    _builder.Entity<Season>(b =>
    {
      b.Property(s => s.Number).IsIdentifier();
      b.Property(s => s.Episodes).IsTrackedSet();
    });
    _builder.Entity<Episode>(b =>
    {
      b.Property(e => e.Number).IsIdentifier();
      b.Property(e => e.Versions).IsSet();
    });

    string connection = Environment.GetEnvironmentVariable(ConnectionVariable) ?? "mongodb://localhost:27017";
    var settings = MongoClientSettings.FromConnectionString(connection);
    settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
    _client = new MongoClient(settings);

    try
    {
      _client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
    }
    catch (TimeoutException)
    {
      Assert.Ignore($"MongoDB is not reachable ({connection}); set {ConnectionVariable} to run integration tests.");
    }

    _databaseName = $"MongoTrackerTests_{Guid.NewGuid():N}";
    _films = _client.GetDatabase(_databaseName).GetCollection<Film>("films");
  }

  [OneTimeTearDown]
  public void Cleanup()
  {
    if (_databaseName != null) _client.DropDatabase(_databaseName);
    _client?.Dispose();
  }

  /// <summary>
  /// Tests that a value added to an episode is pushed into the episode addressed by season and episode numbers
  /// </summary>
  [Test]
  public async Task AddVersion_PushesIntoAddressedEpisode()
  {
    // Arrange
    Guid id = await SeedAsync();
    (MongoTracker<Film> tracker, Film film) = await LoadAsync(id);

    // Act
    FindEpisode(film, 1, 2).Versions.Add("Original");
    await tracker.SaveChangesAsync(_films);

    // Assert
    Assert.That(await DumpAsync(id), Is.EqualTo("S1: E1[Dub], E2[Dub/Original], E3[] | S2: E1[Dub]"));
  }

  /// <summary>
  /// Tests that episodes with the same number in different seasons are addressed independently
  /// </summary>
  [Test]
  public async Task ModifySameEpisodeNumberInDifferentSeasons_UpdatesBoth()
  {
    // Arrange
    Guid id = await SeedAsync();
    (MongoTracker<Film> tracker, Film film) = await LoadAsync(id);

    // Act
    FindEpisode(film, 1, 1).Versions.Add("Original");
    FindEpisode(film, 2, 1).Versions.Add("Original");
    await tracker.SaveChangesAsync(_films);

    // Assert
    Assert.That(await DumpAsync(id), Is.EqualTo("S1: E1[Dub/Original], E2[Dub], E3[] | S2: E1[Dub/Original]"));
  }

  /// <summary>
  /// Tests that a removed episode is pulled by its number from the addressed season
  /// </summary>
  [Test]
  public async Task RemoveEpisode_PullsItByNumber()
  {
    // Arrange
    Guid id = await SeedAsync();
    (MongoTracker<Film> tracker, Film film) = await LoadAsync(id);

    // Act
    FindSeason(film, 1).Episodes.RemoveAll(e => e.Number == 2);
    await tracker.SaveChangesAsync(_films);

    // Assert
    Assert.That(await DumpAsync(id), Is.EqualTo("S1: E1[Dub], E3[] | S2: E1[Dub]"));
  }

  /// <summary>
  /// Tests that an episode modified after a concurrent removal of the preceding episode is still updated
  /// (addressing by position would hit the wrong element)
  /// </summary>
  [Test]
  public async Task ConcurrentRemoveOfPrecedingEpisode_DoesNotShiftModifiedEpisode()
  {
    // Arrange
    Guid id = await SeedAsync();
    (MongoTracker<Film> remover, Film first) = await LoadAsync(id);
    (MongoTracker<Film> modifier, Film second) = await LoadAsync(id);

    // Act
    FindSeason(first, 1).Episodes.RemoveAll(e => e.Number == 1);
    await remover.SaveChangesAsync(_films);
    FindEpisode(second, 1, 2).Versions.Add("Original");
    await modifier.SaveChangesAsync(_films);

    // Assert
    Assert.That(await DumpAsync(id), Is.EqualTo("S1: E2[Dub/Original], E3[] | S2: E1[Dub]"));
  }

  /// <summary>
  /// Tests that an episode concurrently modified by another writer is still removed
  /// (removal by the whole value would not match the modified element)
  /// </summary>
  [Test]
  public async Task RemoveOfConcurrentlyModifiedEpisode_StillRemovesIt()
  {
    // Arrange
    Guid id = await SeedAsync();
    (MongoTracker<Film> modifier, Film first) = await LoadAsync(id);
    (MongoTracker<Film> remover, Film second) = await LoadAsync(id);

    // Act
    FindEpisode(first, 1, 1).Versions.Add("Original");
    await modifier.SaveChangesAsync(_films);
    FindSeason(second, 1).Episodes.RemoveAll(e => e.Number == 1);
    await remover.SaveChangesAsync(_films);

    // Assert
    Assert.That(await DumpAsync(id), Is.EqualTo("S1: E2[Dub], E3[] | S2: E1[Dub]"));
  }

  /// <summary>
  /// Tests that mixed changes of a nested set (added and modified episodes) replace only that nested array
  /// </summary>
  [Test]
  public async Task AddAndModifyEpisodesInOneSave_ReplacesOnlyAddressedSeasonEpisodes()
  {
    // Arrange
    Guid id = await SeedAsync();
    (MongoTracker<Film> tracker, Film film) = await LoadAsync(id);

    // Act
    FindSeason(film, 1).Episodes.Add(new Episode { Number = 4, Versions = ["Dub"] });
    FindEpisode(film, 1, 1).Versions.Add("Original");
    await tracker.SaveChangesAsync(_films);

    // Assert
    Assert.That(await DumpAsync(id),
      Is.EqualTo("S1: E1[Dub/Original], E2[Dub], E3[], E4[Dub] | S2: E1[Dub]"));
  }

  /// <summary>
  /// Tests that changes in different seasons in one save are applied to each of them
  /// </summary>
  [Test]
  public async Task ModifyAndRemoveInDifferentSeasons_AppliesBoth()
  {
    // Arrange
    Guid id = await SeedAsync();
    (MongoTracker<Film> tracker, Film film) = await LoadAsync(id);

    // Act
    FindEpisode(film, 1, 1).Versions.Add("Original");
    FindSeason(film, 2).Episodes.RemoveAll(e => e.Number == 1);
    await tracker.SaveChangesAsync(_films);

    // Assert
    Assert.That(await DumpAsync(id), Is.EqualTo("S1: E1[Dub/Original], E2[Dub], E3[] | S2: "));
  }

  private async Task<Guid> SeedAsync()
  {
    var id = Guid.NewGuid();
    await _films.InsertOneAsync(new Film
    {
      Id = id,
      Seasons =
      [
        new Season
        {
          Number = 1,
          Episodes =
          [
            new Episode { Number = 1, Versions = ["Dub"] },
            new Episode { Number = 2, Versions = ["Dub"] },
            new Episode { Number = 3 }
          ]
        },
        new Season { Number = 2, Episodes = [new Episode { Number = 1, Versions = ["Dub"] }] }
      ]
    });
    return id;
  }

  private async Task<(MongoTracker<Film> Tracker, Film Film)> LoadAsync(Guid id)
  {
    var tracker = new MongoTracker<Film>(_builder);
    Film film = await _films.Find(f => f.Id == id).FirstAsync();
    return (tracker, tracker.Track(film));
  }

  private async Task<string> DumpAsync(Guid id)
  {
    Film film = await _films.Find(f => f.Id == id).FirstAsync();
    return string.Join(" | ", film.Seasons.Select(s =>
      $"S{s.Number}: " + string.Join(", ", s.Episodes.Select(e => $"E{e.Number}[{string.Join("/", e.Versions)}]"))));
  }

  private static Season FindSeason(Film film, int number) => film.Seasons.First(s => s.Number == number);

  private static Episode FindEpisode(Film film, int season, int number) =>
    FindSeason(film, season).Episodes.First(e => e.Number == number);
}
