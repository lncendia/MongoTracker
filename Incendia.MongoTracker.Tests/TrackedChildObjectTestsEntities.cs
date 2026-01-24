namespace Incendia.MongoTracker.Tests;

public partial class TrackedChildObjectTests
{
  public class TestEntity
  {
    public int Id { get; set; }
    public string? Name { get; set; }

    public ChildTestEntity? Child { get; set; }
  }

  public class ChildTestEntity
  {
    public string Name { get; set; } = null!;

    public ChildTestEntity? Child { get; set; }
  }
}