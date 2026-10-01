using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Enums;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

/// <summary>
/// Tests that restoring original values between comparisons is reflected in the tracked changes
/// </summary>
public class RevertTests
{
  public class TestEntity
  {
    public int Id { get; set; }
    public string? Name { get; set; }
    public Child? Child { get; set; }
    public List<string>? Tags { get; set; }
  }

  public class Child
  {
    public string? Title { get; set; }
  }

  private ModelBuilder _builder = null!;

  [OneTimeSetUp]
  public void Initialize()
  {
    _builder = new ModelBuilder();
    _builder.Entity<TestEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Child).IsChild();
      b.Property(e => e.Tags).IsSet();
    });
  }

  private static string Render(UpdateDefinition<TestEntity> update) =>
    update.Render(new RenderArgs<TestEntity>(BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
        BsonSerializer.SerializerRegistry))
      .ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

  /// <summary>
  /// Tests that a property restored to its original value is no longer reported as changed
  /// </summary>
  [Test]
  public void RestoredProperty_IsNotModified()
  {
    // Arrange
    var entity = new TestEntity { Id = 1, Name = "Max" };
    var tracker = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Name = "Egor";
    tracker.TrackChanges(entity);
    entity.Name = "Max";
    tracker.TrackChanges(entity);

    // Assert
    Assert.That(tracker.EntityState, Is.EqualTo(EntityState.Default));
  }

  /// <summary>
  /// Tests that a nested object removed and then restored is tracked field by field again
  /// </summary>
  [Test]
  public void RestoredChildObject_IsTrackedFieldByField()
  {
    // Arrange
    var child = new Child { Title = "Alex" };
    var entity = new TestEntity { Id = 1, Child = child };
    var tracker = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Child = null;
    tracker.TrackChanges(entity);
    string removed = Render(tracker.UpdateDefinition);

    entity.Child = child;
    child.Title = "Kirill";
    tracker.TrackChanges(entity);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(removed, Is.EqualTo("{ \"$set\" : { \"Child\" : null } }"));
      Assert.That(Render(tracker.UpdateDefinition), Is.EqualTo("{ \"$set\" : { \"Child.Title\" : \"Kirill\" } }"));
    }
  }

  /// <summary>
  /// Tests that a nested object which was absent, then set and removed again, is not reported as changed
  /// </summary>
  [Test]
  public void ChildObject_SetAndRemovedAgain_IsNotModified()
  {
    // Arrange
    var entity = new TestEntity { Id = 1 };
    var tracker = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Child = new Child { Title = "Alex" };
    tracker.TrackChanges(entity);
    entity.Child = null;
    tracker.TrackChanges(entity);

    // Assert
    Assert.That(tracker.EntityState, Is.EqualTo(EntityState.Default));
  }

  /// <summary>
  /// Tests that a collection removed and then restored is tracked item by item again
  /// </summary>
  [Test]
  public void RestoredCollection_IsTrackedItemByItem()
  {
    // Arrange
    var tags = new List<string> { "a" };
    var entity = new TestEntity { Id = 1, Tags = tags };
    var tracker = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Tags = null;
    tracker.TrackChanges(entity);
    entity.Tags = tags;
    tags.Add("b");
    tracker.TrackChanges(entity);

    // Assert
    Assert.That(Render(tracker.UpdateDefinition), Is.EqualTo("{ \"$push\" : { \"Tags\" : \"b\" } }"));
  }
}
