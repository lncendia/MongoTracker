using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Enums;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

public partial class ValueSetTrackerTests
{
  private ModelBuilder _builder = null!;

  private readonly RenderArgs<TestEntity> _renderArgs = new(
    BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
    BsonSerializer.SerializerRegistry);

  // Expected JSON results for assertions
  private const string SetSets_OnRootAndChildEntities_GeneratesCorrectSetUpdateEtalonJson =
    "{ \"$set\" : { \"Tags\" : [\"First\", \"Second\"], \"Child.Tags\" : [\"First\", \"Second\"] } }";

  private const string AddItems_ToSets_GeneratesCorrectPushUpdateEtalonJson =
    "{ \"$push\" : { \"Child.Tags\" : \"Third\", \"Tags\" : \"Third\" } }";

  private const string RemoveItems_FromSets_GeneratesCorrectPullUpdateEtalonJson =
    "{ \"$pull\" : { \"Child.Tags\" : \"Second\", \"Tags\" : \"Second\" } }";

  private const string MixedOperations_OnSets_GeneratesCorrectSetUpdateEtalonJson =
    "{ \"$set\" : { \"Child.Tags\" : [\"First\", \"Third\"], \"Tags\" : [\"First\", \"Third\"] } }";

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
    _builder.Entity<ChildTestEntity>(b =>
    {
      b.Property(e => e.Child).IsChild();
      b.Property(e => e.Tags).IsSet();
    });
  }

  /// <summary>
  /// Tests that setting sets on both root entity and child object generates correct $set update
  /// </summary>
  [Test]
  public void SetSets_OnRootAndChildEntities_GeneratesCorrectSetUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Child = new ChildTestEntity()
    };
    var trackedEntity = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Tags = ["First", "Second"];
    entity.Child.Tags = ["First", "Second"];
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    using (Assert.EnterMultipleScope())
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(SetSets_OnRootAndChildEntities_GeneratesCorrectSetUpdateEtalonJson));
    }
  }

  /// <summary>
  /// Tests that adding items to existing sets generates correct $push update
  /// </summary>
  [Test]
  public void AddItems_ToSets_GeneratesCorrectPushUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Tags = ["First", "Second"],
      Child = new ChildTestEntity
      {
        Tags = ["First", "Second"]
      }
    };
    var trackedEntity = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Tags.Add("Third");
    entity.Child.Tags.Add("Third");
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    using (Assert.EnterMultipleScope())
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(AddItems_ToSets_GeneratesCorrectPushUpdateEtalonJson));
    }
  }

  /// <summary>
  /// Tests that removing items from sets generates correct $pull update
  /// </summary>
  [Test]
  public void RemoveItems_FromSets_GeneratesCorrectPullUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Tags = ["First", "Second"],
      Child = new ChildTestEntity
      {
        Tags = ["First", "Second"]
      }
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
      Assert.That(json, Is.EqualTo(RemoveItems_FromSets_GeneratesCorrectPullUpdateEtalonJson));
    }
  }

  /// <summary>
  /// Tests that mixed add and remove operations on sets generate correct $set update with final state
  /// </summary>
  [Test]
  public void MixedOperations_OnSets_GeneratesCorrectSetUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Tags = ["First", "Second"],
      Child = new ChildTestEntity
      {
        Tags = ["First", "Second"]
      }
    };
    var trackedEntity = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Tags.Add("Third");
    entity.Tags.Remove("Second");
    entity.Child.Tags.Add("Third");
    entity.Child.Tags.Remove("Second");
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    using (Assert.EnterMultipleScope())
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(MixedOperations_OnSets_GeneratesCorrectSetUpdateEtalonJson));
    }
  }

  /// <summary>
  /// Tests that replacing sets with new arrays generates correct $set update with final state
  /// </summary>
  [Test]
  public void ReplaceSets_WithNewArrays_GeneratesCorrectSetUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Tags = ["First", "Second"],
      Child = new ChildTestEntity
      {
        Tags = ["First", "Second"]
      }
    };
    var trackedEntity = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Tags = ["First", "Second", "Third"];
    entity.Child.Tags = ["First", "Second", "Third"];
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    using (Assert.EnterMultipleScope())
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(AddItems_ToSets_GeneratesCorrectPushUpdateEtalonJson));
    }
  }
}
