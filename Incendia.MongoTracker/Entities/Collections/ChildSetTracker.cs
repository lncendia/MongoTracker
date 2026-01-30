using System.Collections;

using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Base;
using Incendia.MongoTracker.Entities.Nodes;

using MongoDB.Driver;

namespace Incendia.MongoTracker.Entities.Collections;

/// <summary>
/// Represents a tracked set of nested objects (value objects) within a parent entity.
/// </summary>
/// <typeparam name="T">The root entity type used for MongoDB update definitions.</typeparam>
internal class ChildSetTracker<T> : SetTrackerBase<T> where T : class
{
  #region Fields

  /// <summary>
  /// Stores tracked nested objects (single object).
  /// </summary>
  private readonly IReadOnlyDictionary<object, ChildTracker<T>> _childObjects;

  /// <summary>
  /// Flag, that indicates if any elements present in both sets have been modified
  /// </summary>
  private bool _someModified;

  #endregion

  #region Properties

  /// <inheritdoc/>
  public override bool IsModified
  {
    get
    {
      // Check if there are any basic changes
      if (base.IsModified) return true;

      // Check if any elements present in both sets have been modified
      if (_someModified) return true;

      // If no changes detected, return false
      return false;
    }
  }

  #endregion

  #region Methods

  /// <inheritdoc/>
  public override void TrackChanges(IEnumerable updatedSet)
  {
    base.TrackChanges(updatedSet);
    foreach (object? o in Collection)
    {
      if (!_childObjects.TryGetValue(o, out ChildTracker<T>? trackedObject)) continue;
      trackedObject.TrackChanges(o);
    }

    _someModified = _childObjects.Values.Any(e => e.IsModified);
  }

  /// <inheritdoc/>
  public override UpdateDefinition<T>? GetUpdateDefinition(string? parentPropertyName, string propertyName)
  {
    // Form the full property name (including parent properties)
    string? setFullName = Utils.CombineName(parentPropertyName, propertyName);

    // Create builder for constructing update definition
    UpdateDefinitionBuilder<T>? updateBuilder = Builders<T>.Update;

    // Check if there are elements in the current set that aren't in the original
    bool someAdded = AddedItems?.Count != 0;

    // Check if there are elements in the original set that aren't in the current
    bool someRemoved = RemovedItems?.Count != 0;

    // If only added elements exist (no removed or modified)
    if (someAdded && !someRemoved && !_someModified)
    {
      // Create PushEach operation to add new elements to set
      return updateBuilder.PushEach(setFullName, AddedItems);
    }

    // If only removed elements exist (no added or modified)
    if (someRemoved && !someAdded && !_someModified)
    {
      // Create PullAll operation to remove elements from set
      return updateBuilder.PullAll(setFullName, RemovedItems);
    }

    // If only modified elements exist (no added or removed)
    if (_someModified && !someAdded && !someRemoved)
    {
      // Identify elements present in both sets that were modified
      IEnumerable<UpdateDefinition<T>> updatedItems = _childObjects.Values
        .Select((item, index) => item.GetUpdateDefinition(setFullName, index.ToString()))
        .Where(item => item != null);

      // Combine all update definitions for modified elements into one
      return updateBuilder.Combine(updatedItems);
    }

    // If there are added, removed, or modified elements
    if (someAdded || someRemoved || _someModified)
    {
      // In this case, it's simplest to completely replace the set with new value
      return updateBuilder.Set(setFullName, Collection.ToTypedList(CollectionType));
    }

    // If no changes in set, return null
    return null;
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked child object set, capturing the initial set of objects.
  /// </summary>
  /// <param name="originalSet">The initial set of nested objects to track.</param>
  /// <param name="setType">The element type of the tracked set.</param>
  /// <param name="config">The tracking configuration describing how each nested object should be monitored.</param>
  public ChildSetTracker(IEnumerable originalSet, Type setType, IReadOnlyDictionary<Type, EntityBuilder> config)
    : base(originalSet, setType)
  {
    _childObjects = Collection.ToDictionary(v => v, v => new ChildTracker<T>(v, config));
  }

  #endregion
}
