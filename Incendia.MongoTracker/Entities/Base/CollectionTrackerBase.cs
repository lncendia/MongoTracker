using System.Collections;

using MongoDB.Driver;

namespace Incendia.MongoTracker.Entities.Base;

/// <summary>
/// Represents a tracked collection of simple (non-nested) objects within a parent entity.
/// </summary>
/// <typeparam name="T">The root entity type used for MongoDB update definitions.</typeparam>
internal abstract class CollectionTrackerBase<T> where T : class
{
  #region Fields

  /// <summary>
  /// The element type of the tracked collection.
  /// </summary>
  protected readonly Type CollectionType;

  /// <summary>
  /// The original collection of value objects storing the state before changes.
  /// </summary>
  protected readonly IReadOnlyList<object> OriginalCollection;

  /// <summary>
  /// The current collection that can be modified.
  /// </summary>
  protected IReadOnlyList<object> Collection;

  #endregion

  #region Properties

  /// <summary>
  /// A flag indicating whether the set has been modified.
  /// </summary>
  public abstract bool IsModified { get; }

  #endregion

  #region Methods

  /// <summary>
  /// Updates the tracked collection with a new collection of values.
  /// </summary>
  /// <param name="updatedCollection">The new collection values to be tracked.</param>
  public virtual void TrackChanges(IEnumerable updatedCollection)
  {
    Collection = updatedCollection.Cast<object>().ToList();
  }

  /// <summary>
  /// Returns a MongoDB update definition based on collection changes.
  /// </summary>
  /// <param name="parentPropertyName">Parent property name (for nested documents)</param>
  /// <param name="propertyName">Name of the collection property being updated</param>
  /// <returns>MongoDB update definition. Returns <c>null</c> if there are no changes.</returns>
  public abstract UpdateDefinition<T>? GetUpdateDefinition(string? parentPropertyName, string propertyName);

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked collection by capturing the initial collection of items.
  /// </summary>
  /// <param name="originalCollection">The initial collection of items to be tracked.</param>
  /// <param name="collectionType">The element type of the tracked collection.</param>
  protected CollectionTrackerBase(IEnumerable originalCollection, Type collectionType)
  {
    var collection = originalCollection.Cast<object>().ToList();
    OriginalCollection = collection;
    Collection = collection;
    CollectionType = collectionType;
  }

  #endregion
}
