using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Enums;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

public partial class EntityTrackerTests
{
  private ModelBuilder _builder = null!;

  private readonly RenderArgs<TestEntity> _renderArgs = new(
    BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
    BsonSerializer.SerializerRegistry);

  private const string UpdateProperties_WithNullAndNullableValues_GeneratesCorrectSetUpdateEtalonJson =
    "{ \"$set\" : { \"Name\" : null, \"Age\" : null, \"Money\" : { \"$numberDecimal\" : \"5000\" } } }";

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
    var trackedEntity = new EntityTracker<TestEntity>(entity, _builder.Entities);

    // Act
    entity.Name = null;
    entity.Age = null;
    entity.Money = 5000;
    trackedEntity.TrackChanges(entity);

    // Assert
    BsonDocument rendered = trackedEntity.UpdateDefinition.Render(_renderArgs).AsBsonDocument;

    // The version value is generated at render time, so check it separately from the etalon
    BsonValue version = rendered["$set"]["LastUpdated"];
    rendered["$set"].AsBsonDocument.Remove("LastUpdated");
    string? json = rendered.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

    using (Assert.EnterMultipleScope())
    {
      Assert.That(trackedEntity.EntityState, Is.EqualTo(EntityState.Modified));
      Assert.That(json, Is.EqualTo(UpdateProperties_WithNullAndNullableValues_GeneratesCorrectSetUpdateEtalonJson));
      Assert.That(version.IsValidDateTime, Is.True);
    }
  }
}
