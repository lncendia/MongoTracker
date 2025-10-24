using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <summary>
/// Abstract class representing an entity that can be modified and tracks its changes.
/// </summary>
/// <typeparam name="T">The data type to be used for this entity.</typeparam>
public abstract class UpdatedEntity<T> where T : UpdatedEntity<T>
{
    /// <summary>
    /// Current entity state (Default, Added, Modified, Deleted).
    /// </summary>
    [BsonIgnore]
    public virtual EntityState EntityState
    {
        get => _entityStateValue;
        set => _entityStateValue = value;
    }

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
    private readonly Dictionary<string, ValueType?> _structChanges = new();

    /// <summary>
    /// Collection of property names representing added (not modified) value objects.
    /// If a value object was added (e.g., changed from null to non-null), it needs to be updated
    /// as a whole rather than by individual properties to prevent update conflicts in MongoDB.
    /// </summary>
    protected readonly HashSet<string> AddedValueObjects = [];

    /// <summary>
    /// Current entity state (Default, Added, Modified, Deleted).
    /// </summary>
    private EntityState _entityStateValue = EntityState.Default;

    /// <summary>
    /// Returns a MongoDB update definition.
    /// This property is used to create a database update query.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when entity state is not Modified</exception>
    public virtual UpdateDefinition<T> UpdateDefinition
    {
        get
        {
            // If entity state isn't "Modified", throw InvalidOperationException as there are no changes
            if (EntityState != EntityState.Modified) throw new InvalidOperationException("Cannot create UpdateDefinition for an entity that is not modified.");

            // If state is Modified but no changes are tracked, return null
            // This can occur when using Combine method where related entities have changes
            // In properly configured entities, UpdateDefinition is overridden and uses Combine method that handles null cases
            if (_changes.Count == 0 && _structChanges.Count == 0) return null!;
            
            // Create builder for constructing update definition
            var updateBuilder = Builders<T>.Update;

            // For each changed property (stored in _changes and _structChanges dictionaries)
            // create a Set operation specifying which field to update
            var updates = _changes

                // Create Set operation for each change in _changes dictionary
                // (_changes contains reference types or nullable values)
                .Select(change => updateBuilder.Set(change.Key, change.Value))

                // Add Set operations for changes from _structChanges dictionary
                // (_structChanges contains value types to avoid boxing)
                .Concat(_structChanges.Select(change => updateBuilder.Set(change.Key, change.Value)))

                // Convert to array for subsequent combination
                .ToArray();

            // Combine all Set operations into a single update definition
            // This allows executing all changes in a single database request
            return updateBuilder.Combine(updates);
        }
    }

    /// <summary>
    /// Tracks property changes and returns the new value.
    /// </summary>
    /// <typeparam name="TV">Property value type.</typeparam>
    /// <param name="propertyName">Property name.</param>
    /// <param name="currentValue">Current property value.</param>
    /// <param name="value">New property value.</param>
    /// <returns>New property value.</returns>
    protected TV? TrackChange<TV>(string propertyName, TV? currentValue, TV? value) where TV: class
    {
        // Check if both current and new values are null
        if (currentValue == null && value == null) return value;

        // Check if current and new values are equal (with null consideration)
        if (currentValue?.Equals(value) ?? false) return currentValue;

        // Store new property value in changes dictionary
        _changes[propertyName] = value;

        // Set entity state to "Modified"
        _entityStateValue = EntityState.Modified;

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
        // Check if current and new values are equal
        if (currentValue.Equals(value)) return currentValue;

        // Store new property value in struct changes dictionary
        _structChanges[propertyName] = value;

        // Set entity state to "Modified"
        _entityStateValue = EntityState.Modified;

        // Return new value
        return value;
    }
    
    
    /// <summary>
    /// Tracks changes in nullavle value type (struct) properties and returns new value.
    /// </summary>
    /// <typeparam name="TV">Property value type (struct).</typeparam>
    /// <param name="propertyName">Property name.</param>
    /// <param name="currentValue">Current property value.</param>
    /// <param name="value">New property value.</param>
    /// <returns>New property value.</returns>
    protected TV? TrackStructChange<TV>(string propertyName, TV? currentValue, TV? value) where TV : struct
    {
        // Check if current and new values are equal
        if (currentValue.Equals(value)) return currentValue;

        // Store new property value in struct changes dictionary
        _structChanges[propertyName] = value;

        // Set entity state to "Modified"
        _entityStateValue = EntityState.Modified;

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
        where TV : UpdatedValueObject<T>
    {
        // If current value is null and new value isn't null,
        // this means value object was added
        if (currentValue == null && value != null)
        {
            // Set entity state to "Modified"
            _entityStateValue = EntityState.Modified;
            
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
            // Set entity state to "Modified"
            _entityStateValue = EntityState.Modified;
            
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
    /// <typeparam name="T">Model type the collection belongs to.</typeparam>
    /// <param name="propertyName">Tracked property name.</param>
    /// <param name="currentValue">Current tracked collection state.</param>
    /// <param name="value">New collection value.</param>
    /// <returns>Updated TrackedCollection instance or null if collection was removed.</returns>
    protected TrackedCollection<TV, T>? TrackCollection<TV>(
        string propertyName,
        TrackedCollection<TV, T>? currentValue,
        List<TV>? value)
    {
        // Scenario 1: Adding new collection
        if (currentValue == null && value != null)
        {
            // Set entity state to "Modified"
            _entityStateValue = EntityState.Modified;
            
            // Record new collection addition in change log
            _changes[propertyName] = value;

            // Mark property as completely new for subsequent processing
            AddedValueObjects.Add(propertyName);

            // Create new tracked collection
            return new TrackedCollection<TV, T>
            {
                Collection = value // Initialize collection with new values
            };
        }

        // Scenario 2: Removing existing collection
        if (currentValue != null && value == null)
        {
            // Set entity state to "Modified"
            _entityStateValue = EntityState.Modified;
            
            // Record collection removal (store null)
            _changes[propertyName] = value;

            // Return null indicating collection removal
            return null;
        }

        // Scenario 3: Updating existing collection
        if (currentValue != null && value != null)
        {
            // Set entity state to "Modified"
            _entityStateValue = EntityState.Modified;
            
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
    /// <typeparam name="T">Model type the collection belongs to.</typeparam>
    /// <param name="propertyName">Tracked property name.</param>
    /// <param name="currentValue">Current tracked collection state.</param>
    /// <param name="value">New collection value.</param>
    /// <returns>Updated TrackedObjectCollection instance or null if collection was removed.</returns>
    protected TrackedValueObjectCollection<TV, T>? TrackValueObjectCollection<TV>(
        string propertyName,
        TrackedValueObjectCollection<TV, T>? currentValue,
        List<TV>? value) where TV : UpdatedValueObject<T>
    {
        // Scenario 1: Adding new value object collection
        if (currentValue == null && value != null)
        {
            // Set entity state to "Modified"
            _entityStateValue = EntityState.Modified;
            
            // Record addition in change log
            _changes[propertyName] = value;

            // Mark property as completely new
            AddedValueObjects.Add(propertyName);

            // Create new tracked value object collection
            return new TrackedValueObjectCollection<TV, T>
            {
                Collection = value // Initialize collection with new values
            };
        }

        // Scenario 2: Removing existing collection
        if (currentValue != null && value == null)
        {
            // Set entity state to "Modified"
            _entityStateValue = EntityState.Modified;
            
            // Record removal (store null)
            _changes[propertyName] = value;

            // Return null indicating removal
            return null;
        }

        // Scenario 3: Updating existing collection
        if (currentValue != null && value != null)
        {
            // Set entity state to "Modified"
            _entityStateValue = EntityState.Modified;
            
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
    /// This method removes all tracked property changes and resets
    /// entity state to default (EntityState.Default).
    /// </summary>
    public virtual void ClearChanges()
    {
        // Reset entity state to "Default"
        // This means entity is no longer considered modified
        _entityStateValue = EntityState.Default;

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
    /// Combines multiple update definitions into one.
    /// </summary>
    /// <typeparam name="T">MongoDB document type.</typeparam>
    /// <param name="updates">Array of update definitions. Null elements are ignored.</param>
    /// <returns>Combined update definition or null if all array elements are null.</returns>
    protected static UpdateDefinition<T> Combine(params UpdateDefinition<T>?[] updates)
    {
        // Filter array by removing all null values
        var nonNullUpdates = updates.Where(u => u != null).ToArray();

        // Use Combine method from Builders<T>.Update to merge all update definitions
        return Builders<T>.Update.Combine(nonNullUpdates);
    }

    /// <summary>
    /// Combines entity state with child object modification flags.
    /// The method checks if any child objects were modified and updates entity state
    /// accordingly. If at least one child object was modified, entity state
    /// is set to "Modified".
    /// </summary>
    /// <param name="modified">
    /// Array of child object modification flags. Each element indicates whether
    /// corresponding child object was modified (true - modified, false - not modified, null - unknown).
    /// </param>
    /// <returns>Resulting entity state.</returns>
    protected EntityState Combine(params bool?[] modified)
    {
        // If entity state isn't "Default", return it unchanged
        // This means entity state was already modified previously
        if (_entityStateValue != EntityState.Default) return _entityStateValue;

        // If no child objects were modified, return current entity state
        // In this case it will be "Default" as state hasn't changed
        if (!modified.Any(m => m.HasValue && m.Value)) return _entityStateValue;

        // Set entity state to "Modified"
        // Indicates entity was modified and requires database update
        _entityStateValue = EntityState.Modified;

        // Return "Modified" state
        return _entityStateValue;
    }
}