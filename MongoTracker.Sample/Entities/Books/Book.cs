using MongoDB.Driver;
using MongoTracker.Entities;

namespace MongoTracker.Sample.Entities.Books;

/// <summary>
/// Book model for working with MongoDB database.
/// Inherits from the abstract class UpdatedEntity,
/// which provides change tracking functionality and entity state management.
/// </summary>
public class Book : UpdatedEntity<Book>
{
    private string _title = null!;
    private Audiobook? _audiobook;
    private TrackedCollection<Guid, Book> _authors = new();
    private TrackedValueObjectCollection<BookChapter, Book> _chapters  = new();

    /// <summary>
    /// Unique book identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Book title. Required field.
    /// </summary>
    public required string Title
    {
        // Getter returns the value of the private field _title.
        get => _title;

        // Setter tracks changes and updates the value.
        set => _title = TrackChange(nameof(Title), _title, value)!;
    }

    /// <summary>
    /// Audiobook data. Optional field.
    /// </summary>
    public Audiobook? Audiobook
    {
        // Getter returns the value of the private field _audiobook.
        get => _audiobook;

        // Setter tracks changes and updates the value.
        set => _audiobook = TrackValueObject(nameof(Audiobook), _audiobook, value)!;
    }

    /// <summary>
    /// Array of book author IDs. Empty array by default.
    /// </summary>
    public List<Guid> Authors
    {
        // Getter returns the value of the private field _authors or an empty array if the value is null.
        get => _authors.Collection;

        // Setter tracks changes and updates the value.
        set => _authors = TrackCollection(nameof(Authors), _authors, value)!;
    }

    /// <summary>
    /// Array of book chapters. Empty array by default.
    /// </summary>
    public List<BookChapter> Chapters
    {
        // Getter returns the value of the private field _chapters or an empty array if the value is null.
        get => _chapters.Collection;

        // Setter tracks changes and updates the value.
        set => _chapters = TrackValueObjectCollection(nameof(Chapters), _chapters, value)!;
    }

    /// <inheritdoc/>
    /// <summary>
    /// Returns the MongoDB update definition by combining changes from all tracked entity properties.
    /// </summary>
    public override UpdateDefinition<Book> UpdateDefinition
    {
        get
        {
            // Get base update definition from the parent class.
            var baseUpdateDefinition = base.UpdateDefinition;

            // Get update definition for audiobook if it was modified.
            var audiobookUpdateDefinition = _audiobook?.GetUpdateDefinition(null, nameof(Audiobook), AddedValueObjects);

            // Get update definition for authors list if it was modified.
            var authorsUpdateDefinition = _authors.GetUpdateDefinition(null, nameof(Authors), AddedValueObjects);

            // Get update definition for chapters list if it was modified.
            var chaptersUpdateDefinition = _chapters.GetUpdateDefinition(null, nameof(Chapters), AddedValueObjects);

            // Combine all update definitions into one and return the result.
            return Combine(
                baseUpdateDefinition,
                audiobookUpdateDefinition,
                authorsUpdateDefinition,
                chaptersUpdateDefinition
            );
        }
    }
    
    /// <inheritdoc/>
    /// <summary>
    /// Returns the current entity state by combining states of all tracked properties.
    /// </summary>
    public override EntityState EntityState => Combine(
        
        // Audiobook state if it was modified.
        _audiobook?.IsModified,

        // Authors list state if it was modified.
        _authors.IsModified,

        // Chapters list state if it was modified.
        _chapters.IsModified
    );

    /// <inheritdoc/>
    /// <summary>
    /// Clears all changes in the entity and all its tracked properties.
    /// </summary>
    public override void ClearChanges()
    {
        // Clear changes in the base entity.
        base.ClearChanges();

        // Clear changes in audiobook if it exists.
        _audiobook?.ClearChanges();

        // Clear changes in authors list.
        _authors.ClearChanges();

        // Clear changes in chapters list.
        _chapters.ClearChanges();
    }
}