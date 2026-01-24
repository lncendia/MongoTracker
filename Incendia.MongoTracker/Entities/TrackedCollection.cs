using MongoDB.Driver;

namespace Incendia.MongoTracker.Entities;

/// <summary>
/// Represents a tracked collection of simple (non-nested) objects within a parent entity.
/// </summary>
/// <typeparam name="T">The root entity type used for MongoDB update definitions.</typeparam>
internal class TrackedCollection<T> where T : class
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

      // If no differences found, the collection hasn't been modified
      return false;
    }
  }

  #endregion

  #region Methods

  /// <summary>
  /// Updates the tracked collection with a new set of values.
  /// </summary>
  /// <param name="updatedCollection">The new collection values to be tracked.</param>
  public void TrackChanges(IEnumerable<object> updatedCollection)
  {
    _collection = updatedCollection.ToList();
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

    // Create a builder for constructing the update definition
    UpdateDefinitionBuilder<T>? updateBuilder = Builders<T>.Update;

    // Check if there are elements in the current collection that aren't in the original
    // This means new elements were added to the collection
    bool someAdded = _collection.Except(_originalCollection).Any();

    // Check if there are elements in the original collection that aren't in the current
    // This means elements were removed from the collection
    bool someRemoved = _originalCollection.Except(_collection).Any();

    // If there are added elements and no removed ones
    if (someAdded && !someRemoved)
    {
      // Identify elements that were added to the current collection
      IEnumerable<object> addedItems = _collection.Except(_originalCollection);

      // Return AddToSetEach operation to add new elements to the collection
      return updateBuilder.AddToSetEach(collectionFullName, addedItems);
    }

    // If there are removed elements and no added ones
    if (someRemoved && !someAdded)
    {
      // Identify elements that were removed from the current collection
      object[] removedItems = _originalCollection.Except(_collection).ToArray();

      // Return PullAll operation to remove elements from the collection
      return updateBuilder.PullAll(collectionFullName, removedItems);
    }

    // If there are both added and removed elements
    if (someAdded && someRemoved)
    {
      // In this case, it's simpler to completely replace the collection with the new value
      return updateBuilder.Set(collectionFullName, _collection);
    }

    // If no changes, return null
    return null;
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked collection by capturing the initial set of items.
  /// </summary>
  /// <param name="originalCollection">The initial collection of items to be tracked.</param>
  public TrackedCollection(IEnumerable<object> originalCollection)
  {
    var collection = originalCollection.ToList();
    _originalCollection = collection;
    _collection = collection;
  }

  #endregion
}