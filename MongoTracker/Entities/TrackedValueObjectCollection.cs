using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <summary>
/// A class representing a tracked collection of value objects that can be modified and tracks its changes.
/// </summary>
/// <typeparam name="TC">
/// The type of value objects in the collection. For proper collection functionality,
/// it's recommended that <typeparamref name="TC"/> overrides the <see cref="object.Equals(object?)"/> method.
/// This enables proper value object comparison and reduces unnecessary overwrites.
/// </typeparam>
/// <typeparam name="TP">The type of parent entity to which the collection belongs.</typeparam>
public class TrackedValueObjectCollection<TC, TP> where TC : UpdatedValueObject<TP> where TP : UpdatedEntity<TP>
{
    /// <summary>
    /// The original collection of value objects storing the state before changes.
    /// Used for comparison with the current collection to detect changes.
    /// </summary>
    private List<TC> _originalCollection = [];

    /// <summary>
    /// The current collection that can be modified.
    /// </summary>
    public List<TC> Collection { get; set; } = [];

    /// <summary>
    /// A flag indicating whether the collection has been modified.
    /// </summary>
    public bool IsModified
    {
        get
        {
            // Check if there are elements in the current collection that aren't in the original
            if (Collection.Except(_originalCollection).Any()) return true;

            // Check if there are elements in the original collection that aren't in the current
            if (_originalCollection.Except(Collection).Any()) return true;

            // Check if any elements present in both collections have been modified
            if (_originalCollection.Intersect(Collection).Any(e => e.IsModified)) return true;

            // If no changes detected, return false
            return false;
        }
    }

    /// <summary>
    /// Clears all changes in the collection and resets the state of value objects.
    /// </summary>
    public void ClearChanges()
    {
        // Clear changes for all value objects present in both collections
        foreach (var updatedValueObject in Collection) updatedValueObject.ClearChanges();

        // Copy current collection to original using spread operator
        _originalCollection = [..Collection];
    }

    /// <summary>
    /// Returns a MongoDB update definition based on collection changes.
    /// The method analyzes differences between current and original collections,
    /// and checks if elements present in both collections have been modified.
    /// </summary>
    /// <param name="parentPropertyName">Parent property name (for nested documents)</param>
    /// <param name="propertyName">Name of the collection property being updated</param>
    /// <param name="blockedParentPropertyNames">List of blocked properties that cannot be partially updated</param>
    /// <returns>
    /// MongoDB update definition. Returns <c>null</c> if there are no changes.
    /// </returns>
    public UpdateDefinition<TP>? GetUpdateDefinition(
        string? parentPropertyName,
        string propertyName,
        IReadOnlyCollection<string> blockedParentPropertyNames)
    {
        // If parent property name is in blockedParentPropertyNames,
        // return null as this property shouldn't be updated and the object will be written as a whole
        if (blockedParentPropertyNames.Contains(propertyName)) return null;

        // Form the full property name (including parent properties)
        var collectionFullName = Combine(parentPropertyName, propertyName);

        // Create builder for constructing update definition
        var updateBuilder = Builders<TP>.Update;

        // Check if there are elements in current collection that aren't in original
        // This means new elements were added to the collection
        var someAdded = Collection.Except(_originalCollection).Any();

        // Check if there are elements in original collection that aren't in current
        // This means elements were removed from the collection
        var someRemoved = _originalCollection.Except(Collection).Any();

        // Check if any elements present in both collections have been modified
        // Useful when collection elements themselves can be modified
        var someModified = _originalCollection.Intersect(Collection).Any(e => e.IsModified);

        // If only added elements exist (no removed or modified)
        if (someAdded && !someRemoved && !someModified)
        {
            // Identify elements added to current collection
            var addedItems = Collection.Except(_originalCollection);

            // Create PushEach operation to add new elements to collection
            return updateBuilder.PushEach(collectionFullName, addedItems);
        }

        // If only removed elements exist (no added or modified)
        if (someRemoved && !someAdded && !someModified)
        {
            // Identify elements removed from current collection
            var removedItems = _originalCollection.Except(Collection).ToArray();

            // Create PullAll operation to remove elements from collection
            return updateBuilder.PullAll(collectionFullName, removedItems);
        }

        // If only modified elements exist (no added or removed)
        if (someModified && !someAdded && !someRemoved)
        {
            // Identify elements present in both collections that were modified
            var updatedItems = _originalCollection.Intersect(Collection)

                // Get update definition for each modified element
                .Select((item, index) => item.GetUpdateDefinition(collectionFullName, index.ToString(), []))
                
                // Filtering objects without changes
                .Where(item=> item != null);

            // Combine all update definitions for modified elements into one
            return updateBuilder.Combine(updatedItems);
        }

        // If there are added, removed, or modified elements
        if (someAdded || someRemoved || someModified)
        {
            // In this case, it's simplest to completely replace the collection with new value
            return updateBuilder.Set(collectionFullName, Collection);
        }

        // If no changes in collection, return null
        return null;
    }

    /// <summary>
    /// Combines parent property name and current property name to form a MongoDB path.
    /// If parent property name is absent (null), returns only current property name.
    /// </summary>
    /// <param name="parentPropertyName">Parent property name. Can be null if property isn't nested.</param>
    /// <param name="propertyName">Current property name.</param>
    /// <returns>
    /// A string representing property path in MongoDB.
    /// For example, if parentPropertyName = "Parent" and propertyName = "Child", returns "Parent.Child".
    /// If parentPropertyName = null, returns "Child".
    /// </returns>
    private static string Combine(string? parentPropertyName, string propertyName)
    {
        // If parent property name is absent, return only current property name
        if (parentPropertyName == null) return propertyName;

        // Return combined path with properties separated by dot
        return $"{parentPropertyName}.{propertyName}";
    }
}