using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Tracker;

using MongoDB.Bson;
using MongoDB.Driver;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.IntegrationTests;

/// <summary>
/// Concurrent changes of the same room made through MongoTracker against a real server.
/// Each scenario is run with viewers addressed by position (no identifier configured) and by identifier.
/// </summary>
[Category("Integration")]
public class TrackerConcurrencyTests
{
  private RecordingClient _client = null!;
  private IMongoCollection<Room> _rooms = null!;

  [SetUp]
  public void SetUp()
  {
    _client = new RecordingClient();
    _rooms = _client.Client.GetDatabase($"tracker_{Guid.NewGuid():N}").GetCollection<Room>("Rooms");
  }

  /// <summary>
  /// Model where viewers have no identifier, so they are addressed by position.
  /// </summary>
  private static readonly ModelBuilder Indexed = CreateModel(keyed: false);

  /// <summary>
  /// Model where viewers are addressed by identifier (the configuration used by Overoom).
  /// </summary>
  private static readonly ModelBuilder Keyed = CreateModel(keyed: true);

  private static ModelBuilder CreateModel(bool keyed)
  {
    var model = new ModelBuilder();
    model.Entity<Room>(b =>
    {
      b.Property(r => r.Id).IsIdentifier();
      b.Property(r => r.Viewers).IsTrackedSet();
    });
    model.Entity<Viewer>(b =>
    {
      if (keyed) b.Property(v => v.Id).IsIdentifier();
      b.Property(v => v.Tags).IsSet();
    });
    return model;
  }

  /// <summary>
  /// Loads the room into a new tracker, as a separate hub call would.
  /// </summary>
  private async Task<(MongoTracker<Room> Tracker, Room Room)> LoadAsync(ModelBuilder model)
  {
    var tracker = new MongoTracker<Room>(model);
    Room room = await _rooms.Find(r => r.Id == Rooms.RoomId).FirstAsync();
    return (tracker, tracker.Track(room));
  }

  private Task<Room> ReadAsync() => _rooms.Find(r => r.Id == Rooms.RoomId).FirstAsync();

  private Task<BsonDocument> ReadRawAsync() =>
    _rooms.Database.GetCollection<BsonDocument>("Rooms").Find(new BsonDocument("_id", new BsonBinaryData(Rooms.RoomId, GuidRepresentation.Standard))).FirstAsync();

  private static Viewer ViewerOf(Room room, int number) => room.Viewers.Single(v => v.Id == Rooms.ViewerId(number));

  private static void Pause(Room room, int number)
  {
    Viewer viewer = ViewerOf(room, number);
    viewer.OnPause = true;
    viewer.TimeLine = new TimeSpan(0, 42, 10);
  }

  private static void Disconnect(Room room, int number)
  {
    Viewer viewer = ViewerOf(room, number);
    viewer.Online = false;
    viewer.OnPause = true;
    viewer.FullScreen = false;
  }

  private static void Leave(Room room, int number) => room.Viewers.RemoveAll(v => v.Id == Rooms.ViewerId(number));

  /// <summary>
  /// Viewer 3 pauses while viewer 1 leaves. By position, the pause lands on viewer 4.
  /// </summary>
  [Test]
  public async Task Indexed_PauseDuringLeave_LandsOnAnotherViewer()
  {
    await _rooms.InsertOneAsync(Rooms.Create(viewers: 4));

    (MongoTracker<Room> pausing, Room pausingRoom) = await LoadAsync(Indexed);
    Pause(pausingRoom, 3);

    (MongoTracker<Room> leaving, Room leavingRoom) = await LoadAsync(Indexed);
    Leave(leavingRoom, 1);
    await leaving.SaveChangesAsync(_rooms);

    await pausing.SaveChangesAsync(_rooms);

    _client.Print("Indexed: viewer 1 leaves, then viewer 3 pauses");
    Room room = await ReadAsync();

    using (Assert.EnterMultipleScope())
    {
      Assert.That(room.Viewers, Has.Count.EqualTo(3));
      Assert.That(ViewerOf(room, 3).OnPause, Is.False, "the viewer who paused is still playing");
      Assert.That(ViewerOf(room, 4).OnPause, Is.True, "somebody else got paused");
      Assert.That(ViewerOf(room, 4).TimeLine, Is.EqualTo(new TimeSpan(0, 42, 10)), "and got somebody else's timeline");
    }
  }

  /// <summary>
  /// Viewer 3 pauses while viewer 1 leaves. By identifier, the pause lands on viewer 3.
  /// </summary>
  [Test]
  public async Task Keyed_PauseDuringLeave_LandsOnTheRightViewer()
  {
    await _rooms.InsertOneAsync(Rooms.Create(viewers: 4));

    (MongoTracker<Room> pausing, Room pausingRoom) = await LoadAsync(Keyed);
    Pause(pausingRoom, 3);

    (MongoTracker<Room> leaving, Room leavingRoom) = await LoadAsync(Keyed);
    Leave(leavingRoom, 1);
    await leaving.SaveChangesAsync(_rooms);

    await pausing.SaveChangesAsync(_rooms);

    _client.Print("Keyed: viewer 1 leaves, then viewer 3 pauses");
    Room room = await ReadAsync();

    using (Assert.EnterMultipleScope())
    {
      Assert.That(room.Viewers, Has.Count.EqualTo(3));
      Assert.That(room.Viewers.Where(v => v.OnPause).Select(v => v.Id), Is.EqualTo(new[] { Rooms.ViewerId(3) }));
    }
  }

  /// <summary>
  /// Viewer 2 leaves while its disconnect is saved. Pulled by its whole value, the viewer silently stays.
  /// </summary>
  [Test]
  public async Task Indexed_LeaveDuringDisconnect_ViewerSilentlyStays()
  {
    await _rooms.InsertOneAsync(Rooms.Create(viewers: 3));

    (MongoTracker<Room> leaving, Room leavingRoom) = await LoadAsync(Indexed);
    Leave(leavingRoom, 2);

    (MongoTracker<Room> disconnecting, Room disconnectingRoom) = await LoadAsync(Indexed);
    Disconnect(disconnectingRoom, 2);
    await disconnecting.SaveChangesAsync(_rooms);

    await leaving.SaveChangesAsync(_rooms);

    _client.Print("Indexed: viewer 2 disconnects, then viewer 2 leaves");
    Room room = await ReadAsync();

    Assert.That(room.Viewers.Select(v => v.Id), Does.Contain(Rooms.ViewerId(2)), "the viewer did not leave");
  }

  /// <summary>
  /// Viewer 2 leaves while its disconnect is saved. Pulled by identifier, the viewer leaves.
  /// </summary>
  [Test]
  public async Task Keyed_LeaveDuringDisconnect_ViewerLeaves()
  {
    await _rooms.InsertOneAsync(Rooms.Create(viewers: 3));

    (MongoTracker<Room> leaving, Room leavingRoom) = await LoadAsync(Keyed);
    Leave(leavingRoom, 2);

    (MongoTracker<Room> disconnecting, Room disconnectingRoom) = await LoadAsync(Keyed);
    Disconnect(disconnectingRoom, 2);
    await disconnecting.SaveChangesAsync(_rooms);

    await leaving.SaveChangesAsync(_rooms);

    _client.Print("Keyed: viewer 2 disconnects, then viewer 2 leaves");
    Room room = await ReadAsync();

    Assert.That(room.Viewers.Select(v => v.Id), Is.EqualTo(new[] { Rooms.ViewerId(1), Rooms.ViewerId(3) }));
  }

  /// <summary>
  /// Viewer 3 gets a tag (document → viewers → tags) while viewer 1 leaves. By position, the tag creates
  /// a garbage viewer.
  /// </summary>
  [Test]
  public async Task Indexed_TagDuringLeave_CreatesGarbageViewer()
  {
    await _rooms.InsertOneAsync(Rooms.Create(viewers: 3));

    (MongoTracker<Room> tagging, Room taggingRoom) = await LoadAsync(Indexed);
    ViewerOf(taggingRoom, 3).Tags.Add("Seeker");

    (MongoTracker<Room> leaving, Room leavingRoom) = await LoadAsync(Indexed);
    Leave(leavingRoom, 1);
    await leaving.SaveChangesAsync(_rooms);

    await tagging.SaveChangesAsync(_rooms);

    _client.Print("Indexed: viewer 1 leaves, then viewer 3 gets a tag");
    BsonArray viewers = (await ReadRawAsync())["Viewers"].AsBsonArray;

    using (Assert.EnterMultipleScope())
    {
      Assert.That(viewers, Has.Count.EqualTo(3), "a third viewer appeared");
      Assert.That(viewers[2].AsBsonDocument.Names, Is.EqualTo(new[] { "Tags" }), "the new viewer has only tags");
      Assert.That(viewers[1]["Tags"].AsBsonArray, Is.Empty, "the tagged viewer got nothing");
    }
  }

  /// <summary>
  /// Viewer 3 gets a tag while viewer 1 leaves. By identifier, the tag lands on viewer 3.
  /// </summary>
  [Test]
  public async Task Keyed_TagDuringLeave_TagsTheRightViewer()
  {
    await _rooms.InsertOneAsync(Rooms.Create(viewers: 3));

    (MongoTracker<Room> tagging, Room taggingRoom) = await LoadAsync(Keyed);
    ViewerOf(taggingRoom, 3).Tags.Add("Seeker");

    (MongoTracker<Room> leaving, Room leavingRoom) = await LoadAsync(Keyed);
    Leave(leavingRoom, 1);
    await leaving.SaveChangesAsync(_rooms);

    await tagging.SaveChangesAsync(_rooms);

    _client.Print("Keyed: viewer 1 leaves, then viewer 3 gets a tag");
    Room room = await ReadAsync();

    using (Assert.EnterMultipleScope())
    {
      Assert.That(room.Viewers, Has.Count.EqualTo(2));
      Assert.That(ViewerOf(room, 3).Tags, Is.EqualTo(new[] { "Seeker" }));
      Assert.That(ViewerOf(room, 2).Tags, Is.Empty);
    }
  }
}
