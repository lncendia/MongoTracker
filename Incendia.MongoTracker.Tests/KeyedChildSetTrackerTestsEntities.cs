using MongoDB.Bson.Serialization.Attributes;

namespace Incendia.MongoTracker.Tests;

public partial class KeyedChildSetTrackerTests
{
  public class TestEntity
  {
    public int Id { get; set; }
    public List<KeyedChild> Children { get; set; } = [];
    public List<CodedChild> Coded { get; set; } = [];
  }

  public class KeyedChild
  {
    public int Id { get; set; }
    public string? Name { get; set; }
    public List<KeyedChild> Children { get; set; } = [];
  }

  public class CodedChild
  {
    [BsonElement("code")]
    public string Code { get; set; } = null!;

    public string? Name { get; set; }
  }
}
