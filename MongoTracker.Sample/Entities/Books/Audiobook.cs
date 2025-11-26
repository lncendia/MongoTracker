namespace MongoTracker.Sample.Entities.Books;

/// <summary>
/// Audiobook model for MongoDB database operations.
/// </summary>
public class Audiobook
{
    /// <summary>
    /// The unique identifier of the audiobook's author.
    /// </summary>
    public Guid Author { get; init; }

    /// <summary>
    /// The duration of the audiobook in minutes.
    /// </summary>
    public double Duration { get; set; }
}