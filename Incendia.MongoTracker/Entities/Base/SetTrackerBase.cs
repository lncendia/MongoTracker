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
    AddedItems = Except(Collection, OriginalCollection);
    RemovedItems = Except(OriginalCollection, Collection);
  }

  /// <summary>
  /// Returns the distinct items of the source whose identity is not present in the other sequence.
  /// </summary>
  /// <param name="source">Items to filter.</param>
  /// <param name="other">Items whose identities are excluded.</param>
  /// <returns>Items of <paramref name="source"/> missing from <paramref name="other"/>.</returns>
  private List<object> Except(IEnumerable<object> source, IEnumerable<object> other)
  {
    var seen = new HashSet<object>(other.Select(GetKey));
    return source.Where(item => seen.Add(GetKey(item))).ToList();
  }

  /// <summary>
  /// Returns the value that identifies an item of the set. By default the item itself is its identity.
  /// </summary>
  /// <param name="item">The set item.</param>
  /// <returns>The identity of the item.</returns>
  protected virtual object GetKey(object item) => item;

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
