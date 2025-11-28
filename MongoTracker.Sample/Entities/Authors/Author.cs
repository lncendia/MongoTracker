namespace MongoTracker.Sample.Entities.Authors;

/// <summary>
/// Author model for working with MongoDB database.
/// </summary>
public class Author
{
  /// <summary>
  /// Unique identifier for the author.
  /// </summary>
  public Guid Id { get; init; }

  /// <summary>
  /// Author's name. Required field.
  /// </summary>
  public required string Name { get; set; }
}