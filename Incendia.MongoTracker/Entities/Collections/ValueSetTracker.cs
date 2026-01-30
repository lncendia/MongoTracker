using System.Collections;

using Incendia.MongoTracker.Entities.Base;

using MongoDB.Driver;

namespace Incendia.MongoTracker.Entities.Collections;

/// <summary>
/// Represents a tracked set of simple (non-nested) objects within a parent entity.
/// </summary>
/// <typeparam name="T">The root entity type used for MongoDB update definitions.</typeparam>
internal class ValueSetTracker<T> : SetTrackerBase<T> where T : class
{
  #region Methods

  /// <inheritdoc/>
  public override UpdateDefinition<T>? GetUpdateDefinition(string? parentPropertyName, string propertyName)
  {
    // Form the full property name (including parent properties)
    string? setFullName = Utils.CombineName(parentPropertyName, propertyName);

    // Create a builder for constructing the update definition
    UpdateDefinitionBuilder<T>? updateBuilder = Builders<T>.Update;

    // Check if there are elements in the current set that aren't in the original
    bool someAdded = AddedItems?.Count != 0;

    // Check if there are elements in the original set that aren't in the current
    bool someRemoved = RemovedItems?.Count != 0;

    // If there are added elements and no removed ones
    if (someAdded && !someRemoved)
    {
      // Return PushEach operation to add new elements to the set
      return updateBuilder.PushEach(setFullName, AddedItems);
    }

    // If there are removed elements and no added ones
    if (someRemoved && !someAdded)
    {
      // Return PullAll operation to remove elements from the set
      return updateBuilder.PullAll(setFullName, RemovedItems);
    }

    // If there are both added and removed elements
    if (someAdded && someRemoved)
    {
      // In this case, it's simpler to completely replace the set with the new value
      return updateBuilder.Set(setFullName, Collection.ToTypedList(CollectionType));
    }

    // If no changes, return null
    return null;
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked set by capturing the initial set of items.
  /// </summary>
  /// <param name="originalSet">The initial set of items to be tracked.</param>
  /// <param name="setType">The element type of the tracked set.</param>
  public ValueSetTracker(IEnumerable originalSet, Type setType) : base(originalSet, setType)
  {
  }

  #endregion
}
