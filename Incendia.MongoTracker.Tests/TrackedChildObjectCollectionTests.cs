using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

public partial class TrackedChildObjectCollectionTests
{
  private ModelBuilder _builder = null!;

  private readonly RenderArgs<TestEntity> _renderArgs = new(
    BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
    BsonSerializer.SerializerRegistry);

  // Expected JSON results for assertions
  private const string ModifyNestedObjects_InCollection_GeneratesCorrectIndexedUpdatesEtalonJson =
    "{ \"$set\" : { \"Children.0.Name\" : \"Egor\", \"Children.0.Child.Name\" : \"Kirill\", \"Children.0.Children.0.Name\" : \"Misha\", \"Children.0.Children.0.Child.Name\" : \"Oleg\" }, \"$addToSet\" : { \"Children.0.Tags\" : \"Second\", \"Children.0.Children.0.Tags\" : \"First\" } }";

  private const string ModifyLastItem_InCollection_GeneratesCorrectIndexedUpdatesEtalonJson =
    "{ \"$set\" : { \"Children.1.Name\" : \"Egor\", \"Children.1.Child.Name\" : \"Kirill\" }, \"$addToSet\" : { \"Children.1.Tags\" : \"First\" } }";

  [OneTimeSetUp]
  public void Initialize()
  {
    _builder = new ModelBuilder();
    _builder.Entity<TestEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Children).IsTrackedObjectCollection();
    });
    _builder.Entity<ChildTestEntity>(b =>
    {
      b.Property(e => e.Child).IsTrackedObject();
      b.Property(e => e.Tags).IsCollection();
      b.Property(e => e.Children).IsTrackedObjectCollection();
    });
  }

  /// <summary>
  /// Tests that modifying nested objects within a collection generates correct indexed $set and $push updates
  /// </summary>
  [Test]
  public void ModifyNestedObjects_InCollection_GeneratesCorrectIndexedUpdates()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Children =
      [
        new ChildTestEntity
        {
          Name = "Max",
          Child = new ChildTestEntity
          {
            Name = "Alex"
          },
          Tags = ["First"],
          Children =
          [
            new ChildTestEntity
            {
              Name = "Anton",
              Child = new ChildTestEntity
              {
                Name = "Nikita"
              },
              Tags = ["Second"],
              Children = []
            }
          ]
        }
      ]
    };
    var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Children.First().Name = "Egor";
    entity.Children.First().Child!.Name = "Kirill";
    entity.Children.First().Tags!.Add("Second");
    entity.Children.First().Children!.First().Name = "Misha";
    entity.Children.First().Children!.First().Child!.Name = "Oleg";
    entity.Children.First().Children!.First().Tags!.Add("First");
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    Assert.Multiple(() =>
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(ModifyNestedObjects_InCollection_GeneratesCorrectIndexedUpdatesEtalonJson));
    });
  }

  /// <summary>
  /// Tests that modifying the last item in a collection generates correct indexed updates
  /// </summary>
  [Test]
  public void ModifyLastItem_InCollection_GeneratesCorrectIndexedUpdates()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Children =
      [
        new ChildTestEntity
        {
          Name = "Max",
          Child = new ChildTestEntity
          {
            Name = "Alex"
          },
          Tags = ["First"]
        },
        new ChildTestEntity
        {
          Name = "Anton",
          Child = new ChildTestEntity
          {
            Name = "Nikita"
          },
          Tags = ["Second"],
          Children = []
        }
      ]
    };
    var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Children.Last().Name = "Egor";
    entity.Children.Last().Child!.Name = "Kirill";
    entity.Children.Last().Tags!.Add("First");
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    Assert.Multiple(() =>
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(ModifyLastItem_InCollection_GeneratesCorrectIndexedUpdatesEtalonJson));
    });
  }
}
