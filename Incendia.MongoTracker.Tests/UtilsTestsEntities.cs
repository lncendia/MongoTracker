using MongoDB.Bson.Serialization.Attributes;

namespace Incendia.MongoTracker.Tests;

public partial class UtilsTests
{
  public class ClassMapEntity
  {
    public int Id { get; set; }
    public string? Mapped { get; set; }
    public string? Unmapped { get; set; }
  }

  public class AttributeEntity
  {
    public int Id { get; set; }
    public string? Name { get; set; }
    [BsonIgnore] public string? Ignored { get; set; }
  }
}
