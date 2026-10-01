using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Exceptions;
using Incendia.MongoTracker.Tracker;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

public partial class MongoTrackerTests
{
  private ModelBuilder _builder = null!;

  [OneTimeSetUp]
  public void Initialize()
  {
    _builder = new ModelBuilder();
    _builder.Entity<IntVersionEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Version).IsVersion();
    });
    _builder.Entity<DateVersionEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.LastUpdated).IsVersion();
    });
    _builder.Entity<ParentEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Child).IsChild();
    });
    _builder.Entity<SetOwnerEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Items).IsTrackedSet();
    });
    _builder.Entity<VersionedChild>(b => b.Property(e => e.Version).IsVersion());
  }

  private static RenderArgs<T> Args<T>() =>
    new(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry);

  private static BsonDocument RenderUpdate<T>(WriteModel<T> model) =>
    ((UpdateOneModel<T>)model).Update.Render(Args<T>()).AsBsonDocument;

  private static BsonDocument RenderFilter<T>(WriteModel<T> model) => model switch
  {
    UpdateOneModel<T> update => update.Filter.Render(Args<T>()),
    DeleteOneModel<T> delete => delete.Filter.Render(Args<T>()),
    _ => throw new ArgumentException("Model has no filter", nameof(model))
  };

  /// <summary>
  /// Tests that an integer version is incremented, written back into the entity and used by the next save
  /// </summary>
  [Test]
  public async Task SaveChanges_WithIntVersion_WritesVersionBackAndUsesItInNextFilter()
  {
    // Arrange
    var tracker = new MongoTracker<IntVersionEntity>(_builder);
    var collection = FakeMongoCollection<IntVersionEntity>.Create();
    IntVersionEntity entity = tracker.Track(new IntVersionEntity { Id = 1, Name = "Egor", Version = 1 });

    // Act
    entity.Name = "Egor 2";
    await tracker.SaveChangesAsync(collection.Collection);
    entity.Name = "Egor 3";
    await tracker.SaveChangesAsync(collection.Collection);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(entity.Version, Is.EqualTo(3));
      Assert.That(RenderFilter(collection.Batches[0][0])["Version"].AsInt32, Is.EqualTo(1));
      Assert.That(RenderUpdate(collection.Batches[0][0])["$set"]["Version"].AsInt32, Is.EqualTo(2));
      Assert.That(RenderFilter(collection.Batches[1][0])["Version"].AsInt32, Is.EqualTo(2));
      Assert.That(RenderUpdate(collection.Batches[1][0])["$set"]["Version"].AsInt32, Is.EqualTo(3));
    }
  }

  /// <summary>
  /// Tests that a DateTime version is generated on the client with millisecond precision and written back into the entity
  /// </summary>
  [Test]
  public void SaveChanges_WithDateVersion_WritesVersionBackAndUsesItInNextFilter()
  {
    // Arrange
    var tracker = new MongoTracker<DateVersionEntity>(_builder);
    var collection = FakeMongoCollection<DateVersionEntity>.Create();
    var original = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    DateVersionEntity entity = tracker.Track(new DateVersionEntity { Id = 1, Name = "Egor", LastUpdated = original });

    // Act
    entity.Name = "Egor 2";
    tracker.SaveChanges(collection.Collection);
    DateTime afterFirstSave = entity.LastUpdated!.Value;
    entity.Name = "Egor 3";
    tracker.SaveChanges(collection.Collection);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(afterFirstSave, Is.GreaterThan(original));
      Assert.That(afterFirstSave.Ticks % TimeSpan.TicksPerMillisecond, Is.Zero);
      Assert.That(RenderUpdate(collection.Batches[0][0])["$set"]["LastUpdated"].ToUniversalTime(),
        Is.EqualTo(afterFirstSave));
      Assert.That(RenderFilter(collection.Batches[1][0])["LastUpdated"].ToUniversalTime(), Is.EqualTo(afterFirstSave));
    }
  }

  /// <summary>
  /// Tests that an update matching no document throws and keeps the changes so they can be retried
  /// </summary>
  [Test]
  public void SaveChanges_WhenUpdateMatchesNothing_ThrowsAndKeepsChanges()
  {
    // Arrange
    var tracker = new MongoTracker<IntVersionEntity>(_builder);
    var collection = FakeMongoCollection<IntVersionEntity>.Create();
    collection.Handler = r => FakeMongoCollection<IntVersionEntity>.Result(r, matched: 0, deleted: 0);
    IntVersionEntity entity = tracker.Track(new IntVersionEntity { Id = 1, Name = "Egor", Version = 1 });
    entity.Name = "Egor 2";

    // Act
    var exception = Assert.Throws<MongoConcurrencyException>(() => tracker.SaveChanges(collection.Collection));
    collection.Handler = FakeMongoCollection<IntVersionEntity>.Succeed;
    tracker.SaveChanges(collection.Collection);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(exception.ExpectedMatchedCount, Is.EqualTo(1));
      Assert.That(exception.MatchedCount, Is.Zero);
      Assert.That(collection.Batches, Has.Count.EqualTo(2));
      Assert.That(RenderFilter(collection.Batches[1][0])["Version"].AsInt32, Is.EqualTo(1));
      Assert.That(RenderUpdate(collection.Batches[1][0])["$set"]["Name"].AsString, Is.EqualTo("Egor 2"));
      Assert.That(entity.Version, Is.EqualTo(2));
    }
  }

  /// <summary>
  /// Tests that a delete removing no document throws and the entity stays tracked
  /// </summary>
  [Test]
  public void SaveChanges_WhenDeleteRemovesNothing_ThrowsAndKeepsEntityTracked()
  {
    // Arrange
    var tracker = new MongoTracker<IntVersionEntity>(_builder);
    var collection = FakeMongoCollection<IntVersionEntity>.Create();
    collection.Handler = r => FakeMongoCollection<IntVersionEntity>.Result(r, matched: 0, deleted: 0);
    IntVersionEntity entity = tracker.Track(new IntVersionEntity { Id = 1, Name = "Egor", Version = 1 });
    tracker.Delete(entity);

    // Act
    var exception = Assert.Throws<MongoConcurrencyException>(() => tracker.SaveChanges(collection.Collection));

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(exception.ExpectedDeletedCount, Is.EqualTo(1));
      Assert.That(exception.DeletedCount, Is.Zero);
      Assert.That(tracker.Get(1), Is.SameAs(entity));
    }
  }

  /// <summary>
  /// Tests that a failed write does not reset the tracker, so the same changes are sent again on retry
  /// </summary>
  [Test]
  public void SaveChanges_WhenWriteFails_ChangesAreNotLost()
  {
    // Arrange
    var tracker = new MongoTracker<IntVersionEntity>(_builder);
    var collection = FakeMongoCollection<IntVersionEntity>.Create();
    collection.Handler = _ => throw new TimeoutException();
    IntVersionEntity entity = tracker.Track(new IntVersionEntity { Id = 1, Name = "Egor", Version = 1 });
    entity.Name = "Egor 2";

    // Act
    Assert.Throws<TimeoutException>(() => tracker.SaveChanges(collection.Collection));
    collection.Handler = FakeMongoCollection<IntVersionEntity>.Succeed;
    tracker.SaveChanges(collection.Collection);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(entity.Version, Is.EqualTo(2));
      Assert.That(RenderFilter(collection.Batches[1][0])["Version"].AsInt32, Is.EqualTo(1));
      Assert.That(RenderUpdate(collection.Batches[1][0])["$set"]["Name"].AsString, Is.EqualTo("Egor 2"));
    }
  }

  /// <summary>
  /// Tests that unacknowledged writes are accepted without a concurrency check
  /// </summary>
  [Test]
  public void SaveChanges_WhenUnacknowledged_DoesNotThrow()
  {
    // Arrange
    var tracker = new MongoTracker<IntVersionEntity>(_builder);
    var collection = FakeMongoCollection<IntVersionEntity>.Create();
    collection.Handler = r => new BulkWriteResult<IntVersionEntity>.Unacknowledged(r.Count, r);
    IntVersionEntity entity = tracker.Track(new IntVersionEntity { Id = 1, Name = "Egor", Version = 1 });
    entity.Name = "Egor 2";

    // Act
    tracker.SaveChanges(collection.Collection);

    // Assert
    Assert.That(entity.Version, Is.EqualTo(2));
  }

  /// <summary>
  /// Tests that an accepted save resets the tracker so an unchanged entity produces no operations
  /// </summary>
  [Test]
  public void SaveChanges_AfterSuccessfulSave_ProducesNoOperations()
  {
    // Arrange
    var tracker = new MongoTracker<IntVersionEntity>(_builder);
    var collection = FakeMongoCollection<IntVersionEntity>.Create();
    IntVersionEntity entity = tracker.Track(new IntVersionEntity { Id = 1, Name = "Egor", Version = 1 });
    entity.Name = "Egor 2";
    tracker.SaveChanges(collection.Collection);

    // Act
    BulkWriteResult<IntVersionEntity>? result = tracker.SaveChanges(collection.Collection);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(result, Is.Null);
      Assert.That(collection.Batches, Has.Count.EqualTo(1));
    }
  }

  /// <summary>
  /// Tests that inserted entities become tracked after a successful save
  /// </summary>
  [Test]
  public void SaveChanges_WithAddedEntity_StartsTrackingIt()
  {
    // Arrange
    var tracker = new MongoTracker<IntVersionEntity>(_builder);
    var collection = FakeMongoCollection<IntVersionEntity>.Create();
    var entity = new IntVersionEntity { Id = 1, Name = "Egor", Version = 1 };
    tracker.Add(entity);
    tracker.SaveChanges(collection.Collection);

    // Act
    entity.Name = "Egor 2";
    tracker.SaveChanges(collection.Collection);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(collection.Batches[0][0], Is.InstanceOf<InsertOneModel<IntVersionEntity>>());
      Assert.That(collection.Batches[1][0], Is.InstanceOf<UpdateOneModel<IntVersionEntity>>());
      Assert.That(entity.Version, Is.EqualTo(2));
    }
  }

  /// <summary>
  /// Tests that a version declared on a child object is filtered by its full path and written back into the child
  /// </summary>
  [Test]
  public void SaveChanges_WithChildVersion_FiltersByFullPathAndWritesVersionBack()
  {
    // Arrange
    var tracker = new MongoTracker<ParentEntity>(_builder);
    var collection = FakeMongoCollection<ParentEntity>.Create();
    ParentEntity entity = tracker.Track(new ParentEntity
    {
      Id = 1, Title = "Book", Child = new VersionedChild { Value = "a", Version = 5 }
    });

    // Act
    entity.Child.Value = "b";
    tracker.SaveChanges(collection.Collection);

    // Assert
    BsonDocument filter = RenderFilter(collection.Batches[0][0]);
    BsonDocument update = RenderUpdate(collection.Batches[0][0]);

    using (Assert.EnterMultipleScope())
    {
      Assert.That(filter["Child.Version"].ToInt64(), Is.EqualTo(5));
      Assert.That(update["$set"]["Child.Version"].ToInt64(), Is.EqualTo(6));
      Assert.That(entity.Child.Version, Is.EqualTo(6));
    }
  }

  /// <summary>
  /// Tests that changing one item of a tracked set updates only that item, addressed by its array index
  /// </summary>
  [Test]
  public void SaveChanges_WithModifiedTrackedSetItem_UpdatesOnlyThatItem()
  {
    // Arrange
    var tracker = new MongoTracker<SetOwnerEntity>(_builder);
    var collection = FakeMongoCollection<SetOwnerEntity>.Create();
    SetOwnerEntity entity = tracker.Track(new SetOwnerEntity
    {
      Id = 1,
      Items =
      [
        new VersionedChild { Value = "a", Version = 1 },
        new VersionedChild { Value = "b", Version = 1 },
        new VersionedChild { Value = "c", Version = 1 }
      ]
    });

    // Act
    entity.Items[1].Value = "b2";
    tracker.SaveChanges(collection.Collection);

    // Assert
    BsonDocument set = RenderUpdate(collection.Batches[0][0])["$set"].AsBsonDocument;

    using (Assert.EnterMultipleScope())
    {
      Assert.That(set.Names, Is.EquivalentTo(new[] { "Items.1.Value", "Items.1.Version" }));
      Assert.That(entity.Items.Select(i => i.Version), Is.EqualTo(new long[] { 1, 2, 1 }));
    }
  }
}
