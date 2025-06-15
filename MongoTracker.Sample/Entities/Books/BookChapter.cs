using MongoDB.Driver;
using MongoTracker.Entities;

namespace MongoTracker.Sample.Entities.Books;

/// <summary>
/// Book chapter model for working with MongoDB database.
/// Inherits from the abstract class UpdatedValueObject,
/// which provides change tracking functionality and entity state management.
/// </summary>
public class BookChapter : UpdatedValueObject<Book>
{
    private string _name = null!;
    private int _startPage;
    private int _endPage;
    private TrackedCollection<string, Book>? _footnotes;

    /// <summary>
    /// Array of chapter footnotes.
    /// </summary>
    public List<string>? Footnotes
    {
        // Getter returns the value of the private field _footnotes or an empty array if the value is null.
        get => _footnotes?.Collection;

        // Setter tracks changes and updates the value.
        set => _footnotes = TrackCollection(nameof(Footnotes), _footnotes, value)!;
    }

    /// <summary>
    /// Chapter name. Required field.
    /// </summary>
    public required string Name
    {
        // Getter returns the value of the private field _name.
        get => _name;

        // Setter tracks changes and updates the value.
        set => _name = TrackChange(nameof(Name), _name, value)!;
    }

    /// <summary>
    /// Chapter starting page number. Required field.
    /// </summary>
    public required int StartPage
    {
        // Getter returns the value of the private field _startPage.
        get => _startPage;

        // Setter tracks changes and updates the value.
        set => _startPage = TrackStructChange(nameof(StartPage), _startPage, value);
    }

    /// <summary>
    /// Chapter ending page number. Required field.
    /// </summary>
    public required int EndPage
    {
        // Getter returns the value of the private field _endPage.
        get => _endPage;

        // Setter tracks changes and updates the value.
        set => _endPage = TrackStructChange(nameof(EndPage), _endPage, value);
    }

    /// <inheritdoc/>
    /// <summary>
    /// Returns the MongoDB update definition by combining changes from all tracked entity properties.
    /// </summary>
    public override UpdateDefinition<Book>? GetUpdateDefinition(string? parentPropertyName, string propertyName,
        IReadOnlyCollection<string> blockedParentPropertyNames)
    {
        // If the parent property name is contained in blockedParentPropertyNames,
        // return null as this property shouldn't be updated and this object will be written as a whole.
        if (blockedParentPropertyNames.Contains(propertyName)) return null;

        // Get update definition for _footnotes list if it was modified.
        var footnotesDefinition = _footnotes?
            .GetUpdateDefinition(Combine(parentPropertyName, propertyName), nameof(Footnotes), AddedValueObjects);

        // Get base update definition from the parent class.
        var baseDefinition = base.GetUpdateDefinition(parentPropertyName, propertyName, blockedParentPropertyNames);

        // Combine all update definitions into one and return the result.
        return Combine(footnotesDefinition, baseDefinition);
    }

    /// <inheritdoc/>
    /// <summary>
    /// Returns the current entity state by combining states of all tracked properties.
    /// </summary>
    public override bool IsModified => base.IsModified || (_footnotes?.IsModified ?? false);

    /// <inheritdoc/>
    /// <summary>
    /// Clears all changes in the entity and all its tracked properties.
    /// </summary>
    public override void ClearChanges()
    {
        // Clear changes in footnotes list if it exists.
        _footnotes?.ClearChanges();
        
        // Clear changes in the base entity.
        base.ClearChanges();
    }
}