using System.Collections;

using Incendia.MongoTracker.Entities.Base;

using MongoDB.Driver;

namespace Incendia.MongoTracker.Entities.Collections;

/// <summary>
/// Represents a tracked collection of simple (non-nested) objects within a parent entity.
/// </summary>
/// <typeparam name="T">The root entity type used for MongoDB update definitions.</typeparam>
internal class ValueCollectionTracker<T> : CollectionTrackerBase<T> where T : class
{
  #region Fields

  /// <summary>
  /// Flag, that indicates if both collections are equal
  /// </summary>
  private bool _sequenceEqual;

  #endregion

  #region Properties

  /// <summary>
  /// A flag indicating whether the collection has been modified.
  /// </summary>
  public override bool IsModified => !_sequenceEqual;

  #endregion

  #region Methods

  /// <inheritdoc/>
  public override void TrackChanges(IEnumerable updatedCollection)
  {
    base.TrackChanges(updatedCollection);
    _sequenceEqual = Collection.SequenceEqual(OriginalCollection);
  }

  /// <inheritdoc/>
  public override UpdateDefinition<T>? GetUpdateDefinition(string? parentPropertyName, string propertyName)
  {
    if (_sequenceEqual) return null;

    // Form the full property name (including parent properties)
    string? collectionFullName = Utils.CombineName(parentPropertyName, propertyName);

    // Create a builder for constructing the update definition
    UpdateDefinitionBuilder<T>? updateBuilder = Builders<T>.Update;

    // In this case, it's simpler to completely replace the collection with the new value
    return updateBuilder.Set(collectionFullName, Collection.ToTypedList(CollectionType));
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked collection by capturing the initial collection of items.
  /// </summary>
  /// <param name="originalCollection">The initial collection of items to be tracked.</param>
  /// <param name="collectionType">The element type of the tracked collection.</param>
  public ValueCollectionTracker(IEnumerable originalCollection, Type collectionType) : base(originalCollection, collectionType)
  {
  }

  #endregion
}
