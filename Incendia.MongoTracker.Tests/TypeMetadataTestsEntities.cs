namespace Incendia.MongoTracker.Tests;

public partial class TypeMetadataTests
{
  public class PrivateSetterEntity
  {
    public int Id { get; set; }
    public string? Name { get; private set; }
    public int Version { get; private set; }

    public void Rename(string name) => Name = name;
  }

  public class IndexerEntity
  {
    private readonly Dictionary<string, string> _values = new();

    public int Id { get; set; }
    public string? Name { get; set; }

    public string this[string key]
    {
      get => _values[key];
      set => _values[key] = value;
    }
  }

  public class BaseEntity
  {
    public int Id { get; set; }
    public object? Value { get; set; }
  }

  public class HidingEntity : BaseEntity
  {
    public new string? Value { get; set; }
  }

  public struct Point
  {
    public int X { get; set; }
    public int Y { get; set; }
  }
}
