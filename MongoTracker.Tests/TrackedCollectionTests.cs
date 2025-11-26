using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoTracker.Builders;
using MongoTracker.Entities;

// ReSharper disable InconsistentNaming

namespace MongoTracker.Tests;

public partial class TrackedCollectionTests
{
    private ModelBuilder _builder = null!;

    private readonly RenderArgs<TestEntity> _renderArgs = new(
        BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
        BsonSerializer.SerializerRegistry);

    // Expected JSON results for assertions
    private const string SetCollections_OnRootAndChildEntities_GeneratesCorrectSetUpdateEtalonJson =
        "{ \"$set\" : { \"Tags\" : [\"First\", \"Second\"], \"Child.Tags\" : [\"First\", \"Second\"] } }";

    private const string AddItems_ToCollections_GeneratesCorrectPushUpdateEtalonJson =
        "{ \"$addToSet\" : { \"Child.Tags\" : \"Third\", \"Tags\" : \"Third\" } }";

    private const string RemoveItems_FromCollections_GeneratesCorrectPullUpdateEtalonJson =
        "{ \"$pull\" : { \"Child.Tags\" : \"Second\", \"Tags\" : \"Second\" } }";

    private const string MixedOperations_OnCollections_GeneratesCorrectSetUpdateEtalonJson =
        "{ \"$set\" : { \"Child.Tags\" : [\"First\", \"Third\"], \"Tags\" : [\"First\", \"Third\"] } }";

    [OneTimeSetUp]
    public void Initialize()
    {
        _builder = new ModelBuilder();
        _builder.Entity<TestEntity>(b =>
        {
            b.Property(e => e.Id).IsIdentifier();
            b.Property(e => e.Child).IsTrackedObject();
            b.Property(e => e.Tags).IsCollection();
        });
        _builder.Entity<ChildTestEntity>(b =>
        {
            b.Property(e => e.Child).IsTrackedObject();
            b.Property(e => e.Tags).IsCollection();
        });
    }

    /// <summary>
    /// Tests that setting collections on both root entity and child object generates correct $set update
    /// </summary>
    [Test]
    public void SetCollections_OnRootAndChildEntities_GeneratesCorrectSetUpdate()
    {
        // Arrange
        var entity = new TestEntity
        {
            Id = 1,
            Child = new ChildTestEntity()
        };
        var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

        // Act
        entity.Tags = ["First", "Second"];
        entity.Child.Tags = ["First", "Second"];
        trackedEntity.TrackChanges(entity);

        // Assert
        var rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
        var json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

        Assert.Multiple(() =>
        {
            Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
            Assert.That(json, Is.EqualTo(SetCollections_OnRootAndChildEntities_GeneratesCorrectSetUpdateEtalonJson));
        });
    }

    /// <summary>
    /// Tests that adding items to existing collections generates correct $push update
    /// </summary>
    [Test]
    public void AddItems_ToCollections_GeneratesCorrectPushUpdate()
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
        var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

        // Act
        entity.Tags.Add("Third");
        entity.Child.Tags.Add("Third");
        trackedEntity.TrackChanges(entity);

        // Assert
        var rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
        var json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

        Assert.Multiple(() =>
        {
            Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
            Assert.That(json, Is.EqualTo(AddItems_ToCollections_GeneratesCorrectPushUpdateEtalonJson));
        });
    }

    /// <summary>
    /// Tests that removing items from collections generates correct $pull update
    /// </summary>
    [Test]
    public void RemoveItems_FromCollections_GeneratesCorrectPullUpdate()
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
        var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

        // Act
        entity.Tags.Remove("Second");
        entity.Child.Tags.Remove("Second");
        trackedEntity.TrackChanges(entity);

        // Assert
        var rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
        var json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

        Assert.Multiple(() =>
        {
            Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
            Assert.That(json, Is.EqualTo(RemoveItems_FromCollections_GeneratesCorrectPullUpdateEtalonJson));
        });
    }

    /// <summary>
    /// Tests that mixed add and remove operations on collections generate correct $set update with final state
    /// </summary>
    [Test]
    public void MixedOperations_OnCollections_GeneratesCorrectSetUpdate()
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
        var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

        // Act
        entity.Tags.Add("Third");
        entity.Tags.Remove("Second");
        entity.Child.Tags.Add("Third");
        entity.Child.Tags.Remove("Second");
        trackedEntity.TrackChanges(entity);

        // Assert
        var rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
        var json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

        Assert.Multiple(() =>
        {
            Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
            Assert.That(json, Is.EqualTo(MixedOperations_OnCollections_GeneratesCorrectSetUpdateEtalonJson));
        });
    }

    /// <summary>
    /// Tests that replacing collections with new arrays generates correct $set update with final state
    /// </summary>
    [Test]
    public void ReplaceCollections_WithNewArrays_GeneratesCorrectSetUpdate()
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
        var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

        // Act
        entity.Tags = ["First", "Second", "Third"];
        entity.Child.Tags = ["First", "Second", "Third"];
        trackedEntity.TrackChanges(entity);

        // Assert
        var rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
        var json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

        Assert.Multiple(() =>
        {
            Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
            Assert.That(json, Is.EqualTo(AddItems_ToCollections_GeneratesCorrectPushUpdateEtalonJson));
        });
    }
}