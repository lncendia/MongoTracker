using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

public partial class TrackedChildObjectTests
{
  private ModelBuilder _builder = null!;

  private readonly RenderArgs<TestEntity> _renderArgs = new(
    BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
    BsonSerializer.SerializerRegistry);

  // Expected JSON results for assertions
  private const string SetChildObject_OnRootEntity_GeneratesCorrectSetUpdateEtalonJson =
    "{ \"$set\" : { \"Child\" : { \"Name\" : \"Anton\", \"Child\" : null } } }";

  private const string ModifyChildObjectProperty_OnExistingChild_GeneratesCorrectDottedPathUpdateEtalonJson =
    "{ \"$set\" : { \"Child.Name\" : \"Alex\" } }";

  private const string SetChildObject_OnExistingChild_GeneratesCorrectNestedUpdateEtalonJson =
    "{ \"$set\" : { \"Child.Child\" : { \"Name\" : \"Alex\", \"Child\" : null } } }";

  private const string ModifyProperty_OnNestedChildObject_GeneratesCorrectDeepPathUpdateEtalonJson =
    "{ \"$set\" : { \"Child.Child.Name\" : \"Max\" } }";

  [OneTimeSetUp]
  public void Initialize()
  {
    _builder = new ModelBuilder();
    _builder.Entity<TestEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Child).IsTrackedObject();
    });
    _builder.Entity<ChildTestEntity>(b => b.Property(e => e.Child).IsTrackedObject());
  }

  /// <summary>
  /// Tests that setting a child object on the root entity generates correct $set update
  /// </summary>
  [Test]
  public void SetChildObject_OnRootEntity_GeneratesCorrectSetUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Name = "Egor"
    };
    var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Child = new ChildTestEntity { Name = "Anton" };
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    Assert.Multiple(() =>
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(SetChildObject_OnRootEntity_GeneratesCorrectSetUpdateEtalonJson));
    });
  }

  /// <summary>
  /// Tests that modifying a property of an existing child object generates correct dotted path $set update
  /// </summary>
  [Test]
  public void ModifyChildObjectProperty_OnExistingChild_GeneratesCorrectDottedPathUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Name = "Egor",
      Child = new ChildTestEntity
      {
        Name = "Anton"
      }
    };
    var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Child.Name = "Alex";
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    Assert.Multiple(() =>
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json,
        Is.EqualTo(ModifyChildObjectProperty_OnExistingChild_GeneratesCorrectDottedPathUpdateEtalonJson));
    });
  }

  /// <summary>
  /// Tests that replacing an existing child object with a new one generates correct update
  /// </summary>
  [Test]
  public void ReplaceChildObject_OnRootEntity_GeneratesCorrectSetUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Name = "Egor",
      Child = new ChildTestEntity
      {
        Name = "Anton"
      }
    };
    var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Child = new ChildTestEntity { Name = "Alex" };
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    Assert.Multiple(() =>
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json,
        Is.EqualTo(ModifyChildObjectProperty_OnExistingChild_GeneratesCorrectDottedPathUpdateEtalonJson));
    });
  }

  /// <summary>
  /// Tests that setting a nested child object on an existing child generates correct update
  /// </summary>
  [Test]
  public void SetChildObject_OnExistingChild_GeneratesCorrectNestedUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Name = "Egor",
      Child = new ChildTestEntity
      {
        Name = "Anton"
      }
    };
    var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Child.Child = new ChildTestEntity { Name = "Alex" };
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    Assert.Multiple(() =>
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(SetChildObject_OnExistingChild_GeneratesCorrectNestedUpdateEtalonJson));
    });
  }

  /// <summary>
  /// Tests that modifying a property of a deeply nested child object generates correct multi-level dotted path update
  /// </summary>
  [Test]
  public void ModifyProperty_OnNestedChildObject_GeneratesCorrectDeepPathUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Name = "Egor",
      Child = new ChildTestEntity
      {
        Name = "Anton",
        Child = new ChildTestEntity
        {
          Name = "Alex"
        }
      }
    };
    var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Child.Child.Name = "Max";
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    Assert.Multiple(() =>
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(ModifyProperty_OnNestedChildObject_GeneratesCorrectDeepPathUpdateEtalonJson));
    });
  }
}