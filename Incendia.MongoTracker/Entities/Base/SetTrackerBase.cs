using System.Collections;

namespace Incendia.MongoTracker.Entities.Base;

/// <summary>
/// Represents a tracked set of simple (non-nested) objects within a parent entity.
/// </summary>
/// <typeparam name="T">The root entity type used for MongoDB update definitions.</typeparam>
internal abstract class SetTrackerBase<T> : CollectionTrackerBase<T> where T : class
{
  #region Fields

  /// <summary>
  /// Items that were added compared to the original collection.
  /// </summary>
  protected IReadOnlyList<object>? AddedItems;

  /// <summary>
  /// Items that were removed compared to the original collection.
  /// </summary>
  protected IReadOnlyList<object>? RemovedItems;

  #endregion

  #region Properties

  /// <inheritdoc/>
  public override bool IsModified
  {
    get
    {
      // Check if there are elements in the current set that aren't in the original
      if (AddedItems?.Count > 0) return true;

      // Check if there are elements in the original set that aren't in the current
      if (RemovedItems?.Count > 0) return true;

      // If no differences found, the set hasn't been modified
      return false;
    }
  }

  #endregion

  #region Methods

  /// <inheritdoc/>
  public override void TrackChanges(IEnumerable updatedSet)
  {
    base.TrackChanges(updatedSet);
    AddedItems = Collection.Except(OriginalCollection).ToList();
    RemovedItems = OriginalCollection.Except(Collection).ToList();
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked set by capturing the initial set of items.
  /// </summary>
  /// <param name="originalSet">The initial set of items to be tracked.</param>
  /// <param name="setType">The element type of the tracked set.</param>
  protected SetTrackerBase(IEnumerable originalSet, Type setType) : base(originalSet, setType)
  {
  }

  #endregion
}
