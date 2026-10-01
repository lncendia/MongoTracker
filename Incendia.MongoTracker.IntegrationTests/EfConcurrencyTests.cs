using Microsoft.EntityFrameworkCore;

using MongoDB.Bson;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.IntegrationTests;

/// <summary>
/// The same room scenarios through the MongoDB EF Core provider, for comparison.
/// </summary>
[Category("Integration")]
public class EfConcurrencyTests
{
  private RecordingClient _client = null!;
  private string _database = null!;

  [SetUp]
  public async Task SetUp()
  {
    _client = new RecordingClient();
    _database = $"ef_{Guid.NewGuid():N}";

    await using RoomsContext context = CreateContext();
    context.Rooms.Add(Rooms.Create(viewers: 4));
    await context.SaveChangesAsync();
  }

  /// <summary>
  /// A new context, as a separate hub call would use.
  /// </summary>
  private RoomsContext CreateContext() => RoomsContext.Create(_client.Client, _database);

  private Task<Room> LoadAsync(RoomsContext context) => context.Rooms.SingleAsync(r => r.Id == Rooms.RoomId);

  private async Task<Room> ReadAsync()
  {
    await using RoomsContext context = CreateContext();
    return await context.Rooms.AsNoTracking().SingleAsync(r => r.Id == Rooms.RoomId);
  }

  /// <summary>
  /// Viewer 3 pauses while viewer 1 leaves. The pause rewrites the whole array and brings the left viewer back.
  /// </summary>
  [Test]
  public async Task PauseDuringLeave_BringsLeftViewerBack()
  {
    await using RoomsContext pausing = CreateContext();
    Viewer paused = (await LoadAsync(pausing)).Viewers.Single(v => v.Id == Rooms.ViewerId(3));
    paused.OnPause = true;
    paused.TimeLine = new TimeSpan(0, 42, 10);

    await using (RoomsContext leaving = CreateContext())
    {
      (await LoadAsync(leaving)).Viewers.RemoveAll(v => v.Id == Rooms.ViewerId(1));
      await leaving.SaveChangesAsync();
    }

    await pausing.SaveChangesAsync();

    _client.Print("EF: viewer 1 leaves, then viewer 3 pauses");
    Room room = await ReadAsync();

    using (Assert.EnterMultipleScope())
    {
      Assert.That(room.Viewers.Select(v => v.Id), Does.Contain(Rooms.ViewerId(1)), "the viewer who left is back");
      Assert.That(room.Viewers.Single(v => v.Id == Rooms.ViewerId(3)).OnPause, Is.True);
    }
  }

  /// <summary>
  /// Adding one tag to one viewer (document → viewers → tags) is written as the whole viewers array.
  /// </summary>
  [Test]
  public async Task NestedChange_RewritesWholeArray()
  {
    await using (RoomsContext context = CreateContext())
    {
      (await LoadAsync(context)).Viewers.Single(v => v.Id == Rooms.ViewerId(3)).Tags.Add("Seeker");
      await context.SaveChangesAsync();
    }

    _client.Print("EF: viewer 3 gets a tag");
    BsonDocument set = _client.Updates.Last()["u"]["$set"].AsBsonDocument;

    using (Assert.EnterMultipleScope())
    {
      Assert.That(set.Names, Does.Contain("Viewers"));
      Assert.That(set["Viewers"].AsBsonArray, Has.Count.EqualTo(4), "all viewers are written");
      Assert.That((await ReadAsync()).Viewers.Single(v => v.Id == Rooms.ViewerId(3)).Tags, Is.EqualTo(new[] { "Seeker" }));
    }
  }
}
