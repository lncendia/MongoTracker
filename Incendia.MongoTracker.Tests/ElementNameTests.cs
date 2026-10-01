using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Tracker;

// ReSharper disable InconsistentNaming

namespace Incendia.MongoTracker.Tests;

/// <summary>
/// Tests that update paths and filters use BSON element names rather than C# property names
/// </summary>
public partial class ElementNameTests
{
  private ModelBuilder _builder = null!;

  private static readonly JsonWriterSettings _jsonSettings = new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

  private static readonly RenderArgs<TestEntity> _renderArgs = new(
    BsonSerializer.SerializerRegistry.GetSerializer<TestEntity>(),
    BsonSerializer.SerializerRegistry);

  [OneTimeSetUp]
  public void Initialize()
  {
    _builder = new ModelBuilder();
    _builder.Entity<TestEntity>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Version).IsVersion();
      b.Property(e => e.Token).IsConcurrencyToken();
      b.Property(e => e.Child).IsChild();
      b.Property(e => e.Tags).IsSet();
      b.Property(e => e.Items).IsTrackedSet();
      b.Property(e => e.Keyed).IsTrackedSet();
    });
    _builder.Entity<Child>(b => b.Property(e => e.Token).IsConcurrencyToken());
    _builder.Entity<KeyedItem>(b =>
    {
      b.Property(e => e.Id).IsIdentifier();
      b.Property(e => e.Children).IsTrackedSet();
    });
  }

  private static TestEntity CreateEntity() => new()
  {
    Id = 1,
    Name = "Max",
    Version = 1,
    Token = "t1",
    Child = new Child { Title = "Alex", Token = "c1" },
    Tags = ["a"],
    Items = [new Item { Title = "First" }],
    Keyed = [new KeyedItem { Id = 5, Title = "Keyed", Children = [new KeyedItem { Id = 6 }] }]
  };

  private async Task<UpdateOneModel<TestEntity>> SaveAsync(Action<TestEntity> change)
  {
    var tracker = new MongoTracker<TestEntity>(_builder);
    var collection = FakeMongoCollection<TestEntity>.Create();
    TestEntity entity = tracker.Track(CreateEntity());
    change(entity);
    await tracker.SaveChangesAsync(collection.Collection);
    return (UpdateOneModel<TestEntity>)collection.Batches.Single().Single();
  }

  private static string Render(UpdateDefinition<TestEntity> update) => update.Render(_renderArgs).ToJson(_jsonSettings);

  private static string Render(FilterDefinition<TestEntity> filter) => filter.Render(_renderArgs).ToJson(_jsonSettings);

  /// <summary>
  /// Tests that simple properties, nested objects and the version use element names in $set
  /// </summary>
  [Test]
  public async Task SetPaths_UseElementNames()
  {
    UpdateOneModel<TestEntity> model = await SaveAsync(e =>
    {
      e.Name = "Egor";
      e.Child!.Title = "Kirill";
    });

    Assert.That(Render(model.Update),
      Is.EqualTo("{ \"$set\" : { \"name\" : \"Egor\", \"child.title\" : \"Kirill\", \"ver\" : 2 } }"));
  }

  /// <summary>
  /// Tests that the version and concurrency tokens in the filter use element names
  /// </summary>
  [Test]
  public async Task ConcurrencyFilter_UsesElementNames()
  {
    UpdateOneModel<TestEntity> model = await SaveAsync(e => e.Name = "Egor");

    Assert.That(Render(model.Filter),
      Is.EqualTo("{ \"_id\" : 1, \"ver\" : 1, \"token\" : \"t1\", \"child.token\" : \"c1\" }"));
  }

  /// <summary>
  /// Tests that set operations use element names
  /// </summary>
  [Test]
  public async Task SetOperations_UseElementNames()
  {
    UpdateOneModel<TestEntity> push = await SaveAsync(e => e.Tags.Add("b"));
    UpdateOneModel<TestEntity> indexed = await SaveAsync(e => e.Items[0].Title = "Second");
    UpdateOneModel<TestEntity> keyed = await SaveAsync(e => e.Keyed[0].Title = "Changed");
    UpdateOneModel<TestEntity> pull = await SaveAsync(e => e.Keyed.Clear());

    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(push.Update), Is.EqualTo("{ \"$push\" : { \"tags\" : \"b\" }, \"$set\" : { \"ver\" : 2 } }"));
      Assert.That(Render(indexed.Update), Is.EqualTo("{ \"$set\" : { \"items.0.title\" : \"Second\", \"ver\" : 2 } }"));
      Assert.That(Render(keyed.Update), Is.EqualTo("{ \"$set\" : { \"keyed.$[i0].title\" : \"Changed\", \"ver\" : 2 } }"));
      Assert.That(Render(pull.Update),
        Is.EqualTo("{ \"$pull\" : { \"keyed\" : { \"_id\" : { \"$in\" : [5] } } }, \"$set\" : { \"ver\" : 2 } }"));
    }
  }

  /// <summary>
  /// Tests that fields of keyed elements keep their serializers and nested keyed sets resolve on every level
  /// </summary>
  [Test]
  public async Task KeyedElements_KeepMemberSerializers_OnEveryLevel()
  {
    UpdateOneModel<TestEntity> kind = await SaveAsync(e => e.Keyed[0].Kind = Kind.Second);
    UpdateOneModel<TestEntity> nested = await SaveAsync(e => e.Keyed[0].Children[0].Kind = Kind.Second);
    UpdateOneModel<TestEntity> nestedPull = await SaveAsync(e => e.Keyed[0].Children.Clear());

    using (Assert.EnterMultipleScope())
    {
      Assert.That(Render(kind.Update), Is.EqualTo("{ \"$set\" : { \"keyed.$[i0].kind\" : \"Second\", \"ver\" : 2 } }"));
      Assert.That(Render(nested.Update),
        Is.EqualTo("{ \"$set\" : { \"keyed.$[i0].children.$[i1].kind\" : \"Second\", \"ver\" : 2 } }"));
      Assert.That(Render(nestedPull.Update),
        Is.EqualTo("{ \"$pull\" : { \"keyed.$[i0].children\" : { \"_id\" : { \"$in\" : [6] } } }, \"$set\" : { \"ver\" : 2 } }"));
    }
  }
}
