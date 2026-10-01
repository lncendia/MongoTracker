namespace Incendia.MongoTracker.Tests;

public partial class NestedKeyedSetMongoTests
{
  public class Film
  {
    public Guid Id { get; set; }
    public List<Season> Seasons { get; set; } = [];
  }

  public class Season
  {
    public int Number { get; set; }
    public List<Episode> Episodes { get; set; } = [];
  }

  public class Episode
  {
    public int Number { get; set; }
    public List<string> Versions { get; set; } = [];
  }
}
