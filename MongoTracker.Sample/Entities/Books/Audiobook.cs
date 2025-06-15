using MongoTracker.Entities;

namespace MongoTracker.Sample.Entities.Books;

/// <summary>
/// Audiobook model for MongoDB database operations.
/// Inherits from the abstract UpdatedValueObject class,
/// which provides change tracking and entity state management capabilities.
/// </summary>
public class Audiobook : UpdatedValueObject<Book>
{
    private Guid _author;
    private double _duration;

    /// <summary>
    /// The unique identifier of the audiobook's author.
    /// </summary>
    public Guid Author
    {
        // Returns the value of the private _authorId field
        get => _author;
        
        // Tracks changes and updates the value
        set => _author = TrackStructChange(nameof(Author), _author, value);
    }

    /// <summary>
    /// The duration of the audiobook in minutes.
    /// </summary>
    public double Duration
    {
        // Returns the value of the private _durationInMinutes field
        get => _duration;
        
        // Tracks changes and updates the value
        set => _duration = TrackStructChange(nameof(Duration), _duration, value);
    }
}