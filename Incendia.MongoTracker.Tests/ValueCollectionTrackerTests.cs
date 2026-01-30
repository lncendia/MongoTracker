using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Enums;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

public partial class ValueCollectionTrackerTests
{
  private ModelBuilder _builder = null!;

  private readonly RenderArgs<TestEntity> _renderArgs = new(
    BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
    BsonSerializer.SerializerRegistry);

  // Expected JSON results for assertions
  private const string ValueCollections_ReorderItems_ProducesCorrectSetUpdateJson =
    "{ \"$set\" : { \"Child.Tags\" : [\"Second\", \"First\"], \"Tags\" : [\"Second\", \"First\"] } }";

  private const string ValueCollections_AddItems_ProducesCorrectSetUpdateJson =
    "{ \"$set\" : { \"Child.Tags\" : [\"First\", \"Second\", \"Third\", \"Third\"], \"Tags\" : [\"First\", \"Second\", \"Third\", \"Third\"] } }";

  private const string ValueCollections_RemoveItems_ProducesCorrectSetUpdateJson =
    "{ \"$set\" : { \"Child.Tags\" : [\"First\", \"Second\", \"Second\"], \"Tags\" : [\"First\", \"Second\", \"Second\"] } }";

  [OneTimeSetUp]
  public void Initialize()
  {
    _builder = new ModelBuilder();
    _builder.Entity<TestEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Child).IsChild();
      b.Property(e => e.Tags).IsCollection();
    });
    _builder.Entity<ChildTestEntity>(b =>
    {
      b.Property(e => e.Child).IsChild();
      b.Property(e => e.Tags).IsCollection();
    });
  }

  /// <summary>
  /// Tests that reordering items in value collections produces correct $set update
  /// </summary>
  [Test]
  public void ValueCollections_ReorderItems_ProducesCorrectSetUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Tags = ["First", "Second"],
      Child = new ChildTestEntity { Tags = ["First", "Second"] }
    };
    var trackedEntity = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Tags = ["Second", "First"];
    entity.Child.Tags = ["Second", "First"];
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    using (Assert.EnterMultipleScope())
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(ValueCollections_ReorderItems_ProducesCorrectSetUpdateJson));
    }
  }

  /// <summary>
  /// Tests that adding items to value collections produces correct $set update
  /// </summary>
  [Test]
  public void ValueCollections_AddItems_ProducesCorrectSetUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Tags = ["First", "Second"],
      Child = new ChildTestEntity { Tags = ["First", "Second"] }
    };
    var trackedEntity = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Tags.Add("Third");
    entity.Tags.Add("Third");
    entity.Child.Tags.Add("Third");
    entity.Child.Tags.Add("Third");
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    using (Assert.EnterMultipleScope())
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(ValueCollections_AddItems_ProducesCorrectSetUpdateJson));
    }
  }

  /// <summary>
  /// Tests that removing items from value collections produces correct $set update
  /// </summary>
  [Test]
  public void ValueCollections_RemoveItems_ProducesCorrectSetUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Tags = ["First", "Second", "Second", "Second"],
      Child = new ChildTestEntity { Tags = ["First", "Second", "Second", "Second"] }
    };
    var trackedEntity = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Tags.Remove("Second");
    entity.Child.Tags.Remove("Second");
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    using (Assert.EnterMultipleScope())
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(ValueCollections_RemoveItems_ProducesCorrectSetUpdateJson));
    }
  }
}
