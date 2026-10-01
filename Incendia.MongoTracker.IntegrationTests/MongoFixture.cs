using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

using Testcontainers.MongoDb;

namespace Incendia.MongoTracker.IntegrationTests;

/// <summary>
/// Starts a single-node replica set in Docker once for all integration tests.
/// </summary>
[SetUpFixture]
public class MongoFixture
{
  /// <summary>
  /// The running MongoDB container.
  /// </summary>
  private static MongoDbContainer? _container;

  /// <summary>
  /// Connection string of the running MongoDB server.
  /// </summary>
  public static string ConnectionString =>
    _container?.GetConnectionString() ?? throw new InvalidOperationException("MongoDB is not started.");

  [OneTimeSetUp]
  public async Task StartAsync()
  {
    // Guids are stored as standard UUIDs, as in Overoom
    BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

    // A replica set is required for transactions, which the EF Core provider uses in SaveChanges
    _container = new MongoDbBuilder("mongo:8.3.11").WithReplicaSet().Build();
    await _container.StartAsync();
  }

  [OneTimeTearDown]
  public async Task StopAsync()
  {
    if (_container != null) await _container.DisposeAsync();
  }
}

/// <summary>
/// A client connected to the test server that records every update command sent to it.
/// </summary>
public sealed class RecordingClient
{
  /// <summary>
  /// Update statements (filter, update and array filters) in the order they were sent.
  /// </summary>
  private readonly List<BsonDocument> _updates = [];

  /// <summary>
  /// The recording client.
  /// </summary>
  public IMongoClient Client { get; }

  /// <summary>
  /// Update statements (filter, update and array filters) in the order they were sent.
  /// </summary>
  public IReadOnlyList<BsonDocument> Updates
  {
    get
    {
      lock (_updates) return _updates.ToList();
    }
  }

  public RecordingClient()
  {
    MongoClientSettings settings = MongoClientSettings.FromConnectionString(MongoFixture.ConnectionString);
    settings.ClusterConfigurator = cluster => cluster.Subscribe<CommandStartedEvent>(OnCommandStarted);
    Client = new MongoClient(settings);
  }

  /// <summary>
  /// Records the statements of update commands.
  /// </summary>
  private void OnCommandStarted(CommandStartedEvent e)
  {
    if (e.CommandName != "update") return;

    lock (_updates)
    {
      foreach (BsonValue statement in e.Command["updates"].AsBsonArray)
        _updates.Add(statement.AsBsonDocument.DeepClone().AsBsonDocument);
    }
  }

  /// <summary>
  /// Writes the recorded update statements to the test output.
  /// </summary>
  /// <param name="title">A heading for the statements.</param>
  public void Print(string title)
  {
    TestContext.Out.WriteLine($"--- {title}");
    foreach (BsonDocument update in Updates)
      TestContext.Out.WriteLine(update.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.Shell }));
  }
}
