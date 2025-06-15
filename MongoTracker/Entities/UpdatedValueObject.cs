using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <summary>
/// Abstract class representing a value object that can be modified and tracks its changes.
/// The value object is embedded within a parent entity and doesn't have its own identifier.
/// </summary>
/// <typeparam name="TP">Type of the parent entity that embeds this value object.</typeparam>
public abstract class UpdatedValueObject<TP> where TP : UpdatedEntity<TP>
{
    /// <summary>
    /// Flag indicating whether the entity has been modified.
    /// </summary>
    [BsonIgnore]
    public virtual bool IsModified => _changes.Count > 0 || _structChanges.Count > 0;

    /// <summary>
    /// Dictionary for tracking changes in reference type or nullable properties.
    /// Key is property name, value is new property value.
    /// Used for tracking changes in objects, strings, and other reference types.
    /// </summary>
    private readonly Dictionary<string, object?> _changes = new();

    /// <summary>
    /// Dictionary for tracking changes in value type properties.
    /// Key is property name, value is new property value.
    /// Used for tracking changes in value types (e.g., int, DateTime, bool) without boxing.
    /// </summary>
    private readonly Dictionary<string, ValueType> _structChanges = new();

    /// <summary>
    /// Collection of property names representing added (not modified) value objects.
    /// If a value object was added (e.g., changed from null to non-null), it needs to be updated
    /// as a whole rather than by individual properties to prevent update conflicts in MongoDB.
    /// </summary>
    protected readonly HashSet<string> AddedValueObjects = [];

    /// <summary>
    /// Tracks property changes and returns the new value.
    /// </summary>
    /// <typeparam name="TV">Property value type.</typeparam>
    /// <param name="propertyName">Property name.</param>
    /// <param name="currentValue">Current property value.</param>
    /// <param name="value">New property value.</param>
    /// <returns>New property value.</returns>
    protected TV? TrackChange<TV>(string propertyName, TV? currentValue, TV? value) where TV : class
    {
        // If both current and new values are null, return the new value
        if (currentValue == null && value == null) return value;

        // If current and new values are equal (with null consideration), return current value
        if (currentValue?.Equals(value) ?? false) return currentValue;

        // Store new property value in changes dictionary
        _changes[propertyName] = value;

        // Return new value
        return value;
    }

    /// <summary>
    /// Tracks changes in value type (struct) properties and returns new value.
    /// </summary>
    /// <typeparam name="TV">Property value type (struct).</typeparam>
    /// <param name="propertyName">Property name.</param>
    /// <param name="currentValue">Current property value.</param>
    /// <param name="value">New property value.</param>
    /// <returns>New property value.</returns>
    protected TV TrackStructChange<TV>(string propertyName, TV currentValue, TV value) where TV : struct
    {
        // If current and new values are equal, return current value
        if (currentValue.Equals(value)) return currentValue;

        // Store new property value in struct changes dictionary
        _structChanges[propertyName] = value;

        // Return new value
        return value;
    }

    /// <summary>
    /// Tracks value object changes and returns new value.
    /// If value object was added (changed from null to non-null), it's added to <see cref="AddedValueObjects"/> collection.
    /// This ensures added objects are updated as a whole rather than by individual properties.
    /// </summary>
    /// <typeparam name="TV">Value object type.</typeparam>
    /// <param name="propertyName">Property name being changed.</param>
    /// <param name="currentValue">Current value object.</param>
    /// <param name="value">New value object.</param>
    /// <returns>New value object.</returns>
    protected TV? TrackValueObject<TV>(string propertyName, TV? currentValue, TV? value)
        where TV : UpdatedValueObject<TP>
    {
        // If current value is null and new value isn't null,
        // remove change record for this property from changes dictionary
        if (currentValue == null && value != null)
        {
            // Record new value in changes dictionary
            _changes[propertyName] = value;

            // Add property name to AddedValueObjects collection
            // to indicate this object should be updated as a whole
            AddedValueObjects.Add(propertyName);
        }

        // If current value isn't null and new value is null,
        // this means value object was removed
        else if (currentValue != null && value == null)
        {
            // Record null in changes dictionary
            _changes[propertyName] = value;
        }

        // Return new value object
        return value;
    }

    /// <summary>
    /// Tracks collection changes and returns new or updated TrackedCollection instance.
    /// Implements change tracking mechanism for collections within a unit of work.
    /// </summary>
    /// <typeparam name="TV">Collection element type.</typeparam>
    /// <typeparam name="TP">Model type the collection belongs to.</typeparam>
    /// <param name="propertyName">Tracked property name.</param>
    /// <param name="currentValue">Current tracked collection state.</param>
    /// <param name="value">New collection value.</param>
    /// <returns>Updated TrackedCollection instance or null if collection was removed.</returns>
    protected TrackedCollection<TV, TP>? TrackCollection<TV>(
        string propertyName,
        TrackedCollection<TV, TP>? currentValue,
        List<TV>? value)
    {
        // Scenario 1: Adding new collection
        if (currentValue == null && value != null)
        {
            // Record new collection addition in change log
            _changes[propertyName] = value;

            // Mark property as completely new for subsequent processing
            AddedValueObjects.Add(propertyName);

            // Create new tracked collection
            return new TrackedCollection<TV, TP>
            {
                Collection = value // Initialize collection with new values
            };
        }

        // Scenario 2: Removing existing collection
        if (currentValue != null && value == null)
        {
            // Record collection removal (store null)
            _changes[propertyName] = value;

            // Return null indicating collection removal
            return null;
        }

        // Scenario 3: Updating existing collection
        if (currentValue != null && value != null)
        {
            // Update tracked collection contents
            currentValue.Collection = value;

            // Return updated instance
            return currentValue;
        }

        // Scenario 4: No changes (both values null)
        return null;
    }

    /// <summary>
    /// Tracks value object collection changes and returns new or updated TrackedObjectCollection instance.
    /// Specialized version for working with UpdatedValueObject descendants.
    /// </summary>
    /// <typeparam name="TV">Collection element type (must inherit UpdatedValueObject).</typeparam>
    /// <typeparam name="TP">Model type the collection belongs to.</typeparam>
    /// <param name="propertyName">Tracked property name.</param>
    /// <param name="currentValue">Current tracked collection state.</param>
    /// <param name="value">New collection value.</param>
    /// <returns>Updated TrackedObjectCollection instance or null if collection was removed.</returns>
    protected TrackedValueObjectCollection<TV, TP>? TrackValueObjectCollection<TV>(
        string propertyName,
        TrackedValueObjectCollection<TV, TP>? currentValue,
        List<TV>? value) where TV : UpdatedValueObject<TP>
    {
        // Scenario 1: Adding new value object collection
        if (currentValue == null && value != null)
        {
            // Record addition in change log
            _changes[propertyName] = value;

            // Mark property as completely new
            AddedValueObjects.Add(propertyName);

            // Create new tracked value object collection
            return new TrackedValueObjectCollection<TV, TP>
            {
                Collection = value // Initialize collection with new values
            };
        }

        // Scenario 2: Removing existing collection
        if (currentValue != null && value == null)
        {
            // Record removal (store null)
            _changes[propertyName] = value;

            // Return null indicating removal
            return null;
        }

        // Scenario 3: Updating existing collection
        if (currentValue != null && value != null)
        {
            // Update tracked collection contents
            currentValue.Collection = value;

            // Return updated instance
            return currentValue;
        }

        // Scenario 4: No changes (both values null)
        return null;
    }

    /// <summary>
    /// Clears all changes and resets entity state to initial.
    /// This method removes all tracked property changes
    /// and resets state to default (EntityState.Default).
    /// </summary>
    public virtual void ClearChanges()
    {
        // Clear reference type and nullable value changes dictionary
        // All changes related to objects, strings and other reference types are removed
        _changes.Clear();

        // Clear value type changes dictionary
        // All changes related to value types (e.g., int, DateTime) are removed
        _structChanges.Clear();

        // Clear collection of property names representing added value objects
        AddedValueObjects.Clear();
    }

    /// <summary>
    /// Returns MongoDB update definition based on entity changes.
    /// If property was added, it's not included in update definition as it should be updated as a whole.
    /// </summary>
    /// <param name="parentPropertyName">Parent property name (for nested documents)</param>
    /// <param name="propertyName">Name of collection property being updated</param>
    /// <param name="blockedParentPropertyNames">List of blocked properties that cannot be partially updated</param>
    /// <returns>MongoDB update definition or <c>null</c> if there are no changes.</returns>
    public virtual UpdateDefinition<TP>? GetUpdateDefinition(string? parentPropertyName, string propertyName,
        IReadOnlyCollection<string> blockedParentPropertyNames)
    {
        // If parent property name is in blockedParentPropertyNames,
        // return null as this property shouldn't be updated and the object will be written as a whole
        if (blockedParentPropertyNames.Contains(propertyName)) return null;

        // Create builder for constructing update definition
        var updateBuilder = Builders<TP>.Update;

        // For each changed property (stored in _changes dictionary)
        // create Set operation specifying which field to update
        var updates = _changes

            // Create Set operation for each change
            .Select(change =>
                updateBuilder.Set($"{Combine(parentPropertyName, propertyName)}.{change.Key}", change.Value))

            // Add Set operations for changes from _structChanges dictionary
            // (_structChanges contains value types to avoid boxing)
            .Concat(_structChanges.Select(change =>
                updateBuilder.Set($"{Combine(parentPropertyName, propertyName)}.{change.Key}", change.Value)))

            // Convert to array
            .ToArray();

        // If update operations array is empty, return null
        // Otherwise combine all operations into single update definition
        return updates.Length == 0 ? null : updateBuilder.Combine(updates);
    }

    /// <summary>
    /// Combines parent property name and current property name to form MongoDB path.
    /// If parent property name is absent (null), returns only current property name.
    /// </summary>
    /// <param name="parentPropertyName">Parent property name. Can be null if property isn't nested.</param>
    /// <param name="propertyName">Current property name.</param>
    /// <returns>
    /// String representing property path in MongoDB.
    /// For example, if parentPropertyName = "Parent" and propertyName = "Child", returns "Parent.Child".
    /// If parentPropertyName = null, returns "Child".
    /// </returns>
    protected static string Combine(string? parentPropertyName, string propertyName)
    {
        // If parent property name is absent, return only current property name
        if (parentPropertyName == null) return propertyName;

        // Return combined path with properties separated by dot
        return $"{parentPropertyName}.{propertyName}";
    }

    /// <summary>
    /// Combines multiple update definitions into one.
    /// </summary>
    /// <typeparam name="TP">MongoDB document type.</typeparam>
    /// <param name="updates">Array of update definitions. Null elements are ignored.</param>
    /// <returns>Combined update definition or null if all array elements are null.</returns>
    protected static UpdateDefinition<TP>? Combine(params UpdateDefinition<TP>?[] updates)
    {
        // Filter array by removing all null values
        var nonNullUpdates = updates.Where(u => u != null).ToArray();

        // Use Combine method from Builders<T>.Update to merge all update definitions
        return nonNullUpdates.Length == 0 ? null : Builders<TP>.Update.Combine(nonNullUpdates);
    }
}