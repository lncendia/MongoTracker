using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Tracker;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

/// <summary>
/// Tests for tracked sets whose element type has an identifier: elements must be addressed by it, not by position
/// </summary>
public partial class KeyedChildSetTrackerTests
{
  private ModelBuilder _builder = null!;

  private readonly RenderArgs<TestEntity> _renderArgs = new(
    BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
    BsonSerializer.SerializerRegistry);

  private static readonly JsonWriterSettings _jsonSettings = new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

  [OneTimeSetUp]
  public void Initialize()
  {
    _builder = new ModelBuilder();
    _builder.Entity<TestEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Children).IsTrackedSet();
      b.Property(e => e.Coded).IsTrackedSet();
    });
    _builder.Entity<KeyedChild>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Children).IsTrackedSet();
    });
    _builder.Entity<CodedChild>(b => b.Property(e => e.Code).IsIdentifier());
  }

  private static TestEntity CreateEntity() => new()
  {
    Id = 1,
    Children =
    [
      new KeyedChild { Id = 10, Name = "Max" },
      new KeyedChild { Id = 20, Name = "Anton", Children = [new KeyedChild { Id = 21, Name = "Nikita" }] },
      new KeyedChild { Id = 30, Name = "Oleg" }
    ]
  };

  private string Render(UpdateDefinition<TestEntity> update) => update.Render(_renderArgs).ToJson(_jsonSettings);

  private static string[] Render(IEnumerable<ArrayFilterDefinition>? filters) =>
    (filters ?? [])
    .Select(f => f.Render(BsonDocumentSerializer.Instance, BsonSerializer.SerializerRegistry).ToJson(_jsonSettings))
    .ToArray();

  /// <summary>
  /// Tests that a modified element is addressed by its identifier through a filtered positional operator
  /// </summary>
  [Test]
  public void ModifyItem_AddressesItByIdentifier()
  {
    // Arrange
    TestEntity entity = CreateEntity();
    var tracker = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Children[1].Name = "Egor";
    tracker.TrackChanges(entity);
    UpdateOneModel<TestEntity> model = tracker.CreateUpdateModel(Builders<TestEntity>.Filter.Empty);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(model.Update), Is.EqualTo("{ \"$set\" : { \"Children.$[i0].Name\" : \"Egor\" } }"));
      Assert.That(Render(model.ArrayFilters), Is.EqualTo(new[] { "{ \"i0._id\" : 20 }" }));
    }
  }

  /// <summary>
  /// Tests that every modified element gets its own array filter identifier
  /// </summary>
  [Test]
  public void ModifySeveralItems_UsesDistinctIdentifiers()
  {
    // Arrange
    TestEntity entity = CreateEntity();
    var tracker = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Children[0].Name = "Egor";
    entity.Children[2].Name = "Kirill";
    tracker.TrackChanges(entity);
    UpdateOneModel<TestEntity> model = tracker.CreateUpdateModel(Builders<TestEntity>.Filter.Empty);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(model.Update),
        Is.EqualTo("{ \"$set\" : { \"Children.$[i0].Name\" : \"Egor\", \"Children.$[i1].Name\" : \"Kirill\" } }"));
      Assert.That(Render(model.ArrayFilters), Is.EqualTo(new[] { "{ \"i0._id\" : 10 }", "{ \"i1._id\" : 30 }" }));
    }
  }

  /// <summary>
  /// Tests that elements of a keyed set nested in a keyed set are addressed by identifiers on every level
  /// </summary>
  [Test]
  public void ModifyNestedItem_AddressesEveryLevelByIdentifier()
  {
    // Arrange
    TestEntity entity = CreateEntity();
    var tracker = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Children[1].Children[0].Name = "Misha";
    tracker.TrackChanges(entity);
    UpdateOneModel<TestEntity> model = tracker.CreateUpdateModel(Builders<TestEntity>.Filter.Empty);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(model.Update),
        Is.EqualTo("{ \"$set\" : { \"Children.$[i0].Children.$[i1].Name\" : \"Misha\" } }"));
      Assert.That(Render(model.ArrayFilters), Is.EqualTo(new[] { "{ \"i0._id\" : 20 }", "{ \"i1._id\" : 21 }" }));
    }
  }

  /// <summary>
  /// Tests that removed elements are pulled by identifier rather than by their whole value
  /// </summary>
  [Test]
  public void RemoveItems_PullsThemByIdentifier()
  {
    // Arrange
    TestEntity entity = CreateEntity();
    var tracker = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Children.RemoveAt(2);
    entity.Children.RemoveAt(0);
    tracker.TrackChanges(entity);
    UpdateOneModel<TestEntity> model = tracker.CreateUpdateModel(Builders<TestEntity>.Filter.Empty);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(model.Update),
        Is.EqualTo("{ \"$pull\" : { \"Children\" : { \"_id\" : { \"$in\" : [10, 30] } } } }"));
      Assert.That(model.ArrayFilters, Is.Null);
    }
  }

  /// <summary>
  /// Tests that an element replaced by another instance with the same identifier is tracked as a modification
  /// </summary>
  [Test]
  public void ReplaceItem_WithSameIdentifier_TrackedAsModification()
  {
    // Arrange
    TestEntity entity = CreateEntity();
    var tracker = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Children[1] = new KeyedChild { Id = 20, Name = "Egor", Children = [new KeyedChild { Id = 21, Name = "Nikita" }] };
    tracker.TrackChanges(entity);
    UpdateOneModel<TestEntity> model = tracker.CreateUpdateModel(Builders<TestEntity>.Filter.Empty);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(model.Update), Is.EqualTo("{ \"$set\" : { \"Children.$[i0].Name\" : \"Egor\" } }"));
      Assert.That(Render(model.ArrayFilters), Is.EqualTo(new[] { "{ \"i0._id\" : 20 }" }));
    }
  }

  /// <summary>
  /// Tests that the array filter uses the BSON element name and serializer of the identifier
  /// </summary>
  [Test]
  public void ModifyItem_WithCustomElementName_UsesBsonElementName()
  {
    // Arrange
    var modifyEntity = new TestEntity { Id = 1, Coded = [new CodedChild { Code = "a" }, new CodedChild { Code = "b" }] };
    var modifyTracker = new EntityTracker<TestEntity>(modifyEntity, _builder.Entities);
    var removeEntity = new TestEntity { Id = 1, Coded = [new CodedChild { Code = "a" }, new CodedChild { Code = "b" }] };
    var removeTracker = new EntityTracker<TestEntity>(removeEntity, _builder.Entities);

    // Act
    modifyEntity.Coded[1].Name = "Egor";
    modifyTracker.TrackChanges(modifyEntity);
    UpdateOneModel<TestEntity> modify = modifyTracker.CreateUpdateModel(Builders<TestEntity>.Filter.Empty);

    removeEntity.Coded.RemoveAt(0);
    removeTracker.TrackChanges(removeEntity);
    UpdateOneModel<TestEntity> remove = removeTracker.CreateUpdateModel(Builders<TestEntity>.Filter.Empty);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(modify.Update), Is.EqualTo("{ \"$set\" : { \"Coded.$[i0].Name\" : \"Egor\" } }"));
      Assert.That(Render(modify.ArrayFilters), Is.EqualTo(new[] { "{ \"i0.code\" : \"b\" }" }));
      Assert.That(Render(remove.Update),
        Is.EqualTo("{ \"$pull\" : { \"Coded\" : { \"code\" : { \"$in\" : [\"a\"] } } } }"));
    }
  }

  /// <summary>
  /// Tests that several elements with the same identifier cannot be tracked
  /// </summary>
  [Test]
  public void TrackSet_WithDuplicateIdentifiers_Throws()
  {
    // Arrange
    var entity = new TestEntity { Id = 1, Children = [new KeyedChild { Id = 10 }, new KeyedChild { Id = 10 }] };

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() => _ = new EntityTracker<TestEntity>(entity, _builder.Entities));
  }

  /// <summary>
  /// Tests that saving passes the array filters of the update to the bulk write
  /// </summary>
  [Test]
  public async Task SaveChanges_PassesArrayFiltersToBulkWrite()
  {
    // Arrange
    var tracker = new MongoTracker<TestEntity>(_builder);
    var collection = FakeMongoCollection<TestEntity>.Create();
    TestEntity entity = tracker.Track(CreateEntity());

    // Act
    entity.Children[2].Name = "Egor";
    await tracker.SaveChangesAsync(collection.Collection);
    entity.Children[2].Name = "Kirill";
    await tracker.SaveChangesAsync(collection.Collection);

    // Assert
    var first = (UpdateOneModel<TestEntity>)collection.Batches[0].Single();
    var second = (UpdateOneModel<TestEntity>)collection.Batches[1].Single();

    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(first.Update), Is.EqualTo("{ \"$set\" : { \"Children.$[i0].Name\" : \"Egor\" } }"));
      Assert.That(Render(first.ArrayFilters), Is.EqualTo(new[] { "{ \"i0._id\" : 30 }" }));
      Assert.That(Render(second.Update), Is.EqualTo("{ \"$set\" : { \"Children.$[i0].Name\" : \"Kirill\" } }"));
      Assert.That(Render(second.ArrayFilters), Is.EqualTo(new[] { "{ \"i0._id\" : 30 }" }));
    }
  }
}
