using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Incendia.MongoTracker.Tests;

public partial class ElementNameTests
{
  public class TestEntity
  {
    public int Id { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("ver")]
    public int Version { get; set; }

    [BsonElement("token")]
    public string? Token { get; set; }

    [BsonElement("child")]
    public Child? Child { get; set; }

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = [];

    [BsonElement("items")]
    public List<Item> Items { get; set; } = [];

    [BsonElement("keyed")]
    public List<KeyedItem> Keyed { get; set; } = [];
  }

  public class Child
  {
    [BsonElement("title")]
    public string? Title { get; set; }

    [BsonElement("token")]
    public string? Token { get; set; }
  }

  public class Item
  {
    [BsonElement("title")]
    public string? Title { get; set; }
  }

  public class KeyedItem
  {
    public int Id { get; set; }

    [BsonElement("title")]
    public string? Title { get; set; }

    [BsonElement("kind"), BsonRepresentation(BsonType.String)]
    public Kind Kind { get; set; }

    [BsonElement("children")]
    public List<KeyedItem> Children { get; set; } = [];
  }

  public enum Kind
  {
    First,
    Second
  }
}
