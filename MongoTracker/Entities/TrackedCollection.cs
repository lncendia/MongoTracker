using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <summary>
/// A class representing a tracked collection that can be modified and tracks its changes.
/// </summary>
/// <typeparam name="TC">The type of collection elements.</typeparam>
/// <typeparam name="TP">The type of parent entity to which the collection belongs.</typeparam>
public class TrackedCollection<TC, TP>
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

            // If no differences found, the collection hasn't been modified
            return false;
        }
    }
    
    /// <summary>
    /// Clears all changes in the collection and resets the state of value objects.
    /// </summary>
    public void ClearChanges()
    {
        // Copy the current collection to the original using spread operator
        _originalCollection = [..Collection];
    }

    /// <summary>
    /// Returns a MongoDB update definition based on collection changes.
    /// The method analyzes differences between the current and original collections
    /// and returns an appropriate MongoDB update definition.
    /// </summary>
    /// <param name="parentPropertyName">Parent property name (for nested documents)</param>
    /// <param name="propertyName">Name of the collection property being updated</param>
    /// <param name="blockedParentPropertyNames">List of blocked properties that cannot be partially updated</param>
    /// <returns>
    /// MongoDB update definition. Returns <c>null</c> if there are no changes.
    /// </returns>
    public UpdateDefinition<TP>? GetUpdateDefinition(string? parentPropertyName, string propertyName,
        IEnumerable<string> blockedParentPropertyNames)
    {
        // If the parent property name is in blockedParentPropertyNames,
        // return null as this property shouldn't be updated and the object will be written as a whole
        if (blockedParentPropertyNames.Contains(propertyName)) return null;
        
        // Form the full property name (including parent properties)
        var collectionFullName = Combine(parentPropertyName, propertyName);
        
        // Create a builder for constructing the update definition
        var updateBuilder = Builders<TP>.Update;

        // Check if there are elements in the current collection that aren't in the original
        // This means new elements were added to the collection
        var someAdded = Collection.Except(_originalCollection).Any();

        // Check if there are elements in the original collection that aren't in the current
        // This means elements were removed from the collection
        var someRemoved = _originalCollection.Except(Collection).Any();

        // If there are added elements and no removed ones
        if (someAdded && !someRemoved)
        {
            // Identify elements that were added to the current collection
            var addedItems = Collection.Except(_originalCollection);

            // Return PushEach operation to add new elements to the collection
            return updateBuilder.PushEach(collectionFullName, addedItems);
        }

        // If there are removed elements and no added ones
        if (someRemoved && !someAdded)
        {
            // Identify elements that were removed from the current collection
            var removedItems = _originalCollection.Except(Collection).ToArray();

            // Return PullAll operation to remove elements from the collection
            return updateBuilder.PullAll(collectionFullName, removedItems);
        }

        // If there are both added and removed elements
        if (someAdded && someRemoved)
        {
            // In this case, it's simpler to completely replace the collection with the new value
            return updateBuilder.Set(collectionFullName, Collection);
        }

        // If no changes, return null
        return null;
    }
    
    /// <summary>
    /// Combines parent property name and current property name to form a MongoDB path.
    /// If parent property name is absent (null), returns only the current property name.
    /// </summary>
    /// <param name="parentPropertyName">Parent property name. Can be null if the property isn't nested.</param>
    /// <param name="propertyName">Current property name.</param>
    /// <returns>
    /// A string representing the property path in MongoDB.
    /// For example, if parentPropertyName = "Parent" and propertyName = "Child", the method returns "Parent.Child".
    /// If parentPropertyName = null, the method returns "Child".
    /// </returns>
    private static string Combine(string? parentPropertyName, string propertyName)
    {
        // If parent property name is absent, return only the current property name
        if (parentPropertyName == null) return propertyName;

        // Return the combined path with properties separated by a dot
        return $"{parentPropertyName}.{propertyName}";
    }
}