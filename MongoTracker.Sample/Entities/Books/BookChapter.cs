namespace MongoTracker.Sample.Entities.Books;

/// <summary>
/// Book chapter model for working with MongoDB database.
/// </summary>
public class BookChapter
{
    /// <summary>
    /// Array of chapter footnotes.
    /// </summary>
    public List<string>? Footnotes { get; set; }

    /// <summary>
    /// Chapter name. Required field.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Chapter starting page number. Required field.
    /// </summary>
    public required int StartPage { get; set; }
    
    /// <summary>
    /// Chapter ending page number. Required field.
    /// </summary>
    public required int EndPage { get; set; }
}