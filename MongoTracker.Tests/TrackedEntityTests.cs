using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoTracker.Builders;
using MongoTracker.Entities;

// ReSharper disable InconsistentNaming

namespace MongoTracker.Tests;

public partial class TrackedEntityTests
{
  private ModelBuilder _builder = null!;

  private readonly RenderArgs<TestEntity> _renderArgs = new(
    BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
    BsonSerializer.SerializerRegistry);

  private const string UpdateProperties_WithNullAndNullableValues_GeneratesCorrectSetUpdateEtalonJson =
    "{ \"$set\" : { \"Name\" : null, \"Age\" : null, \"Money\" : { \"$numberDecimal\" : \"5000\" } }, \"$currentDate\" : { \"LastUpdated\" : { \"$type\" : \"date\" } } }";

  [OneTimeSetUp]
  public void Initialize()
  {
    _builder = new ModelBuilder();
    _builder.Entity<TestEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.LastUpdated).IsVersion();
      b.Property(e => e.Name).IsConcurrencyToken();
    });
  }

  /// <summary>
  /// Tests that updating entity properties with null and nullable values generates correct $set update
  /// </summary>
  [Test]
  public void UpdateProperties_WithNullAndNullableValues_GeneratesCorrectSetUpdate()
  {
    // Arrange
    var entity = new TestEntity
    {
      Id = 1,
      Name = "Egor",
      Age = 22,
      Money = null
    };
    var trackedEntity = new TrackedEntity<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Name = null;
    entity.Age = null;
    entity.Money = 5000;
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonValue? rendered = trackedEntity.UpdateDefinition.Render(_renderArgs);
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    Assert.Multiple(() =>
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(UpdateProperties_WithNullAndNullableValues_GeneratesCorrectSetUpdateEtalonJson));
    });
  }
}
