using MongoDB.Bson.Serialization;
using Incendia.MongoTracker.Builders;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

public partial class UtilsTests
{
  [OneTimeSetUp]
  public void Initialize()
  {
    BsonClassMap.RegisterClassMap<ClassMapEntity>(c =>
    {
      c.MapIdProperty(e => e.Id);
      c.MapProperty(e => e.Mapped);
    });
  }

  /// <summary>
  /// Tests that a property mapped by a registered class map is not treated as ignored
  /// </summary>
  [Test]
  public void IsBsonIgnored_WithMappedClassMapProperty_ReturnsFalse()
  {
    Assert.That(typeof(ClassMapEntity).GetProperty(nameof(ClassMapEntity.Mapped))!.IsBsonIgnored(), Is.False);
  }

  /// <summary>
  /// Tests that a property left out of a registered class map is treated as ignored
  /// </summary>
  [Test]
  public void IsBsonIgnored_WithUnmappedClassMapProperty_ReturnsTrue()
  {
    Assert.That(typeof(ClassMapEntity).GetProperty(nameof(ClassMapEntity.Unmapped))!.IsBsonIgnored(), Is.True);
  }

  /// <summary>
  /// Tests that attribute-based ignore is respected for types without a registered class map
  /// </summary>
  [Test]
  public void IsBsonIgnored_WithBsonIgnoreAttribute_ReturnsTrue()
  {
    using (Assert.EnterMultipleScope())
    {
      Assert.That(typeof(AttributeEntity).GetProperty(nameof(AttributeEntity.Ignored))!.IsBsonIgnored(), Is.True);
      Assert.That(typeof(AttributeEntity).GetProperty(nameof(AttributeEntity.Name))!.IsBsonIgnored(), Is.False);
    }
  }

  /// <summary>
  /// Tests that integer versions are incremented, starting from one when absent
  /// </summary>
  [Test]
  public void NextVersion_WithIntegerTypes_Increments()
  {
    using (Assert.EnterMultipleScope())
    {
      Assert.That(Utils.NextVersion(41, typeof(int)), Is.EqualTo(42));
      Assert.That(Utils.NextVersion(null, typeof(int?)), Is.EqualTo(1));
      Assert.That(Utils.NextVersion(41L, typeof(long)), Is.EqualTo(42L));
    }
  }

  /// <summary>
  /// Tests that a DateTime version always moves forward, even if the stored value is ahead of the local clock
  /// </summary>
  [Test]
  public void NextVersion_WithDateAheadOfClock_MovesForward()
  {
    // Arrange
    DateTime future = new DateTime(DateTime.UtcNow.AddHours(1).Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond,
      DateTimeKind.Utc);

    // Act
    var next = (DateTime)Utils.NextVersion(future, typeof(DateTime));

    // Assert
    Assert.That(next, Is.EqualTo(future.AddMilliseconds(1)));
  }

  /// <summary>
  /// Tests that unsupported version types are rejected at configuration time
  /// </summary>
  [Test]
  public void IsVersion_WithUnsupportedType_Throws()
  {
    var builder = new ModelBuilder();

    Assert.Throws<InvalidOperationException>(() =>
      builder.Entity<AttributeEntity>(b => b.Property(e => e.Name).IsVersion()));
  }
}
