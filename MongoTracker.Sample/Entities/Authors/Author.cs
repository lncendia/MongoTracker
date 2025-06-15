using MongoTracker.Entities;

namespace MongoTracker.Sample.Entities.Authors;

/// <summary>
/// Author model for working with MongoDB database.
/// Inherits from the abstract UpdatedEntity class,
/// which provides change tracking and entity state management functionality.
/// </summary>
public class Author : UpdatedEntity<Author>
{
    private string _name = null!;

    /// <summary>
    /// Unique identifier for the author.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Author's name. Required field.
    /// </summary>
    public required string Name
    {
        // Getter returns the value of the private _name field
        get => _name;
        
        // Setter tracks changes and updates the value
        set => _name = TrackChange(nameof(Name), _name, value)!;
    }
}