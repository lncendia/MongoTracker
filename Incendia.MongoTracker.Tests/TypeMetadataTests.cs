using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Metadata;
using Incendia.MongoTracker.Tracker;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

/// <summary>
/// Tests for cached property accessors used instead of reflection calls
/// </summary>
public partial class TypeMetadataTests
{
  private ModelBuilder _builder = null!;

  [OneTimeSetUp]
  public void Initialize()
  {
    _builder = new ModelBuilder();
    _builder.Entity<PrivateSetterEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Version).IsVersion();
    });
    _builder.Entity<IndexerEntity>(b => b.Property(e => e.Id).IsIdentifier());
  }

  private static string Render<T>(UpdateDefinition<T> update) =>
    update.Render(new RenderArgs<T>(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry))
      .ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

  /// <summary>
  /// Tests that properties with private setters are tracked and versions are written back through them
  /// </summary>
  [Test]
  public async Task PrivateSetter_IsTrackedAndWrittenBack()
  {
    // Arrange
    var tracker = new MongoTracker<PrivateSetterEntity>(_builder);
    var collection = FakeMongoCollection<PrivateSetterEntity>.Create();
    PrivateSetterEntity entity = tracker.Track(new PrivateSetterEntity { Id = 1 });

    // Act
    entity.Rename("Egor");
    await tracker.SaveChangesAsync(collection.Collection);

    // Assert
    var update = (UpdateOneModel<PrivateSetterEntity>)collection.Batches[0].Single();

    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(update.Update), Is.EqualTo("{ \"$set\" : { \"Name\" : \"Egor\", \"Version\" : 1 } }"));
      Assert.That(entity.Version, Is.EqualTo(1));
    }
  }

  /// <summary>
  /// Tests that indexers are not treated as tracked properties
  /// </summary>
  [Test]
  public void Indexer_IsNotTracked()
  {
    // Arrange
    var entity = new IndexerEntity { Id = 1, Name = "Max" };
    var tracker = new EntityTracker<IndexerEntity>(entity, _builder.Entities);

    // Act
    entity.Name = "Egor";
    tracker.TrackChanges(entity);

    // Assert
    Assert.That(Render(tracker.UpdateDefinition), Is.EqualTo("{ \"$set\" : { \"Name\" : \"Egor\" } }"));
  }

  /// <summary>
  /// Tests that a property hidden with 'new' is accessed through the most derived declaration
  /// </summary>
  [Test]
  public void HiddenProperty_UsesMostDerivedDeclaration()
  {
    // Arrange
    var entity = new HidingEntity { Id = 1, Value = "Egor" };

    // Act
    TypeMetadata metadata = TypeMetadata.Get(typeof(HidingEntity));

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(metadata.Properties.Count(p => p.Name == nameof(HidingEntity.Value)), Is.EqualTo(1));
      Assert.That(metadata.Find(nameof(HidingEntity.Value))!.GetValue(entity), Is.EqualTo("Egor"));
    }
  }

  /// <summary>
  /// Tests that accessors of struct properties read and write the boxed struct itself
  /// </summary>
  [Test]
  public void StructProperty_ReadsAndWritesBoxedValue()
  {
    // Arrange
    PropertyAccessor x = TypeMetadata.Get(typeof(Point)).Find(nameof(Point.X))!;
    object point = new Point { X = 1, Y = 2 };

    // Act
    x.SetValue(point, 5);

    // Assert
    using (Assert.EnterMultipleScope())
    {
      Assert.That(x.GetValue(point), Is.EqualTo(5));
      Assert.That(((Point)point).X, Is.EqualTo(5));
    }
  }
}
