namespace Incendia.MongoTracker.Tests;

public partial class MongoTrackerTests
{
  public class IntVersionEntity
  {
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Version { get; set; }
  }

  public class DateVersionEntity
  {
    public int Id { get; set; }
    public string? Name { get; set; }
    public DateTime? LastUpdated { get; set; }
  }

  public class ParentEntity
  {
    public int Id { get; set; }
    public string? Title { get; set; }
    public VersionedChild Child { get; set; } = new();
  }

  public class SetOwnerEntity
  {
    public int Id { get; set; }
    public List<VersionedChild> Items { get; set; } = [];
  }

  public class VersionedChild
  {
    public string? Value { get; set; }
    public long Version { get; set; }
  }
}
