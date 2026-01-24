namespace Incendia.MongoTracker.Sample.Entities.Books;

/// <summary>
/// Book model for working with MongoDB database.
/// </summary>
public class Book
{
  /// <summary>
  /// Unique book identifier.
  /// </summary>
  public Guid Id { get; init; }

  /// <summary>
  /// Book title. Required field.
  /// </summary>
  public required string Title { get; set; }

  /// <summary>
  /// Audiobook data. Optional field.
  /// </summary>
  public Audiobook? Audiobook { get; set; }

  /// <summary>
  /// Array of book author IDs. Empty array by default.
  /// </summary>
  public List<Guid> Authors { get; set; } = [];

  /// <summary>
  /// Array of book chapters. Empty array by default.
  /// </summary>
  public List<BookChapter> Chapters { get; set; } = [];

  /// <summary>
  /// Timestamp used for optimistic concurrency control
  /// </summary>
  public DateTime LastUpdate { get; set; }
}
