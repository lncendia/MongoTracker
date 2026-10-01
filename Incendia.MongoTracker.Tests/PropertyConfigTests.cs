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
/// Tests for property configuration defaults
/// </summary>
public class PropertyConfigTests
{
  public class TestEntity
  {
    public int Id { get; set; }
    public string? Name { get; set; }
    public List<string> Tags { get; set; } = [];
    public Address Address { get; set; } = new();
  }

  public class Address
  {
    public string? City { get; set; }
  }

  /// <summary>
  /// Tests that collections and nested objects without a mode are compared as whole values: changes made in place
  /// are not detected, while assigning a new instance is written as a whole
  /// </summary>
  [Test]
  public void PropertiesWithoutMode_AreComparedAsWholeValues()
  {
    // Arrange
    var builder = new ModelBuilder();
    builder.Entity<TestEntity>(b => b.Property(e => e.Id).IsIdentifier());
    var entity = new TestEntity { Id = 1, Tags = ["a"], Address = new Address { City = "Moscow" } };
    var tracker = new EntityTracker<TestEntity>(entity, builder.Entities);

    // Act
    entity.Tags.Add("b");
    entity.Address.City = "Kazan";
    tracker.TrackChanges(entity);
    EntityState inPlace = tracker.EntityState;

    entity.Tags = ["a", "b"];
    tracker.TrackChanges(entity);

    // Assert
    string json = tracker.UpdateDefinition
      .Render(new RenderArgs<TestEntity>(BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
        BsonSerializer.SerializerRegistry))
      .ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    using (Assert.EnterMultipleScope())
    {
      Assert.That(inPlace, Is.EqualTo(EntityState.Default));
      Assert.That(json, Is.EqualTo("{ \"$set\" : { \"Tags\" : [\"a\", \"b\"] } }"));
    }
  }

  /// <summary>
  /// Tests that a property mentioned in the configuration without a mode stays a plain tracked property
  /// </summary>
  [Test]
  public void PropertyWithoutMode_IsTrackedAsPlainProperty()
  {
    // Arrange
    var builder = new ModelBuilder();
    builder.Entity<TestEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Name);
    });
    var entity = new TestEntity { Id = 1, Name = "Max" };
    var tracker = new EntityTracker<TestEntity>(entity, builder.Entities);

    // Act
    entity.Name = "Egor";
    tracker.TrackChanges(entity);

    // Assert
    string json = tracker.UpdateDefinition
      .Render(new RenderArgs<TestEntity>(BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
        BsonSerializer.SerializerRegistry))
      .ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    Assert.That(json, Is.EqualTo("{ \"$set\" : { \"Name\" : \"Egor\" } }"));
  }
}
