using MongoDB.Driver;
using MongoTracker.Builders;

namespace MongoTracker.Entities;

/// <summary>
/// Represents a tracked collection of nested objects (value objects) within a parent entity.
/// </summary>
/// <typeparam name="T">The root entity type used for MongoDB update definitions.</typeparam>
internal class TrackedChildObjectCollection<T> where T : class
{
  #region Fields

  /// <summary>
  /// The original collection of value objects storing the state before changes.
  /// </summary>
  private readonly IReadOnlyList<object> _originalCollection;

  /// <summary>
  /// The current collection that can be modified.
  /// </summary>
  private List<object> _collection;

  /// <summary>
  /// Stores tracked nested objects (single object).
  /// </summary>
  private readonly IReadOnlyDictionary<object, TrackedChildObject<T>> _childObjects;

  #endregion

  #region Properties

  /// <summary>
  /// A flag indicating whether the collection has been modified.
  /// </summary>
  public bool IsModified
  {
    get
    {
      // Check if there are elements in the current collection that aren't in the original
      if (_collection.Except(_originalCollection).Any()) return true;

      // Check if there are elements in the original collection that aren't in the current
      if (_originalCollection.Except(_collection).Any()) return true;

      // Check if any elements present in both collections have been modified
      if (_childObjects.Values.Any(e => e.IsModified)) return true;

      // If no changes detected, return false
      return false;
    }
  }

  #endregion

  #region Methods

  /// <summary>
  /// Updates the tracked collection with a new set of values.
  /// </summary>
  /// <param name="updatedCollection">The new collection of objects to track.</param>
  public void TrackChanges(IEnumerable<object> updatedCollection)
  {
    _collection = updatedCollection.ToList();
    foreach (object? o in _collection)
    {
      if (!_childObjects.TryGetValue(o, out TrackedChildObject<T>? trackedObject)) continue;
      trackedObject.TrackChanges(o);
    }
  }

  /// <summary>
  /// Returns a MongoDB update definition based on collection changes.
  /// </summary>
  /// <param name="parentPropertyName">Parent property name (for nested documents)</param>
  /// <param name="propertyName">Name of the collection property being updated</param>
  /// <returns>MongoDB update definition. Returns <c>null</c> if there are no changes.</returns>
  public UpdateDefinition<T>? GetUpdateDefinition(string? parentPropertyName, string propertyName)
  {
    // Form the full property name (including parent properties)
    string? collectionFullName = Utils.CombineName(parentPropertyName, propertyName);

    // Create builder for constructing update definition
    UpdateDefinitionBuilder<T>? updateBuilder = Builders<T>.Update;

    // Check if there are elements in current collection that aren't in original
    // This means new elements were added to the collection
    bool someAdded = _collection.Except(_originalCollection).Any();

    // Check if there are elements in original collection that aren't in current
    // This means elements were removed from the collection
    bool someRemoved = _originalCollection.Except(_collection).Any();

    // Check if any elements present in both collections have been modified
    // Useful when collection elements themselves can be modified
    bool someModified = _childObjects.Any(e => e.Value.IsModified);

    // If only added elements exist (no removed or modified)
    if (someAdded && !someRemoved && !someModified)
    {
      // Identify elements added to current collection
      IEnumerable<object> addedItems = _collection.Except(_originalCollection);

      // Create PushEach operation to add new elements to collection
      return updateBuilder.PushEach(collectionFullName, addedItems);
    }

    // If only removed elements exist (no added or modified)
    if (someRemoved && !someAdded && !someModified)
    {
      // Identify elements removed from current collection
      object[] removedItems = _originalCollection.Except(_collection).ToArray();

      // Create PullAll operation to remove elements from collection
      return updateBuilder.PullAll(collectionFullName, removedItems);
    }

    // If only modified elements exist (no added or removed)
    if (someModified && !someAdded && !someRemoved)
    {
      // Identify elements present in both collections that were modified
      IEnumerable<UpdateDefinition<T>> updatedItems = _childObjects.Values
        .Select((item, index) => item.GetUpdateDefinition(collectionFullName, index.ToString()))
        .Where(item => item != null);

      // Combine all update definitions for modified elements into one
      return updateBuilder.Combine(updatedItems);
    }

    // If there are added, removed, or modified elements
    if (someAdded || someRemoved || someModified)
    {
      // In this case, it's simplest to completely replace the collection with new value
      return updateBuilder.Set(collectionFullName, _collection);
    }

    // If no changes in collection, return null
    return null;
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked child object collection, capturing the initial set of objects.
  /// </summary>
  /// <param name="originalCollection">The initial collection of nested objects to track.</param>
  /// <param name="config">The tracking configuration describing how each nested object should be monitored.</param>
  public TrackedChildObjectCollection(IEnumerable<object> originalCollection,
    IReadOnlyDictionary<Type, EntityBuilder> config)
  {
    var collection = originalCollection.ToList();
    _originalCollection = collection;
    _collection = collection;
    _childObjects = collection.ToDictionary(v => v, v => new TrackedChildObject<T>(v, config));
  }

  #endregion
}
