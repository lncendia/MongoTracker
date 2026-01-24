namespace Incendia.MongoTracker.Tests;

public partial class TrackedChildObjectCollectionTests
{
  public class TestEntity
  {
    public int Id { get; set; }
    public List<ChildTestEntity>? Children { get; set; }
  }

  public class ChildTestEntity
  {
    public string? Name { get; set; }
    public ChildTestEntity? Child { get; set; }

    public List<ChildTestEntity>? Children { get; set; }
    public List<string>? Tags { get; set; }
  }
}