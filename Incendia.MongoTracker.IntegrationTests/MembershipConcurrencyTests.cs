using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Exceptions;
using Incendia.MongoTracker.Tracker;

using MongoDB.Driver;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.IntegrationTests;

/// <summary>
/// Room membership with an invariant (at most 10 viewers) protected by a version field.
/// </summary>
[Category("Integration")]
public class MembershipConcurrencyTests
{
  public class Membership
  {
    public Guid Id { get; set; }
    public List<Guid> Viewers { get; set; } = [];
    public DateTime ModifiedAt { get; set; }
  }

  private const int MaxViewersCount = 10;

  private static readonly ModelBuilder Model = CreateModel();

  private static ModelBuilder CreateModel()
  {
    var model = new ModelBuilder();
    model.Entity<Membership>(b =>
    {
      b.Property(m => m.Id).IsIdentifier();
      b.Property(m => m.Viewers).IsSet();
      b.Property(m => m.ModifiedAt).IsVersion();
    });
    return model;
  }

  /// <summary>
  /// Two users join a room with 9 of 10 places taken. Both pass the check, the second save is rejected.
  /// </summary>
  [Test]
  public async Task ConcurrentJoins_ToAlmostFullRoom_SecondIsRejected()
  {
    var client = new RecordingClient();
    IMongoCollection<Membership> rooms = client.Client.GetDatabase($"membership_{Guid.NewGuid():N}")
      .GetCollection<Membership>("Rooms");

    await rooms.InsertOneAsync(new Membership
    {
      Id = Rooms.RoomId,
      Viewers = Enumerable.Range(1, 9).Select(Rooms.ViewerId).ToList(),
      ModifiedAt = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)
    });

    async Task<(MongoTracker<Membership>, Membership)> JoinAsync(Guid userId)
    {
      var tracker = new MongoTracker<Membership>(Model);
      Membership room = tracker.Track(await rooms.Find(r => r.Id == Rooms.RoomId).FirstAsync());
      if (room.Viewers.Count >= MaxViewersCount) throw new InvalidOperationException("Room is full");
      room.Viewers.Add(userId);
      return (tracker, room);
    }

    (MongoTracker<Membership> first, _) = await JoinAsync(Guid.Parse("00000000-0000-0000-0000-0000000000b1"));
    (MongoTracker<Membership> second, _) = await JoinAsync(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));

    await first.SaveChangesAsync(rooms);
    Assert.ThrowsAsync<MongoConcurrencyException>(() => second.SaveChangesAsync(rooms));

    client.Print("Membership: two users join a room with 9 of 10 viewers");
    Membership stored = await rooms.Find(r => r.Id == Rooms.RoomId).FirstAsync();

    Assert.That(stored.Viewers, Has.Count.EqualTo(MaxViewersCount));
  }
}
