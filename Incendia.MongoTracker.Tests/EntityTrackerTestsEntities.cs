namespace Incendia.MongoTracker.Tests;

public partial class EntityTrackerTests
{
  public class TestEntity
  {
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? Age { get; set; }
    public decimal? Money { get; set; }
    public DateTime? LastUpdated { get; set; }
  }
}
