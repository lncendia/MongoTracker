namespace Incendia.MongoTracker.Tests;

public partial class TrackedCollectionTests
{
  public class TestEntity
  {
    public int Id { get; set; }
    public ChildTestEntity? Child { get; set; }
    public List<string>? Tags { get; set; }
  }

  public class ChildTestEntity
  {
    public ChildTestEntity? Child { get; set; }
    public List<string>? Tags { get; set; }
  }
}