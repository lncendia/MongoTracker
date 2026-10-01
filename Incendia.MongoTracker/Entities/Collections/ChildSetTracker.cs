using System.Collections;

using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Base;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Metadata;
using Incendia.MongoTracker.Updates;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Incendia.MongoTracker.Entities.Collections;

/// <summary>
/// Represents a tracked set of nested objects (value objects) within a parent entity.
/// </summary>
/// <remarks>
/// When the element type has an identifier configured, elements are matched by it and addressed in updates
/// by the identifier ($[identifier] with an array filter, $pull by identifier) instead of by their position,
/// so concurrent changes of other elements in the same array cannot shift the targeted element.
/// </remarks>
/// <typeparam name="T">The root entity type used for MongoDB update definitions.</typeparam>
internal class ChildSetTracker<T> : SetTrackerBase<T> where T : class
{
  #region Fields

  /// <summary>
  /// Stores tracked nested objects keyed by their identity (see <see cref="GetKey"/>).
  /// </summary>
  private readonly IReadOnlyDictionary<object, ChildTracker<T>> _childObjects;

  /// <summary>
  /// Identifier property of the element type, if one is configured.
  /// </summary>
  private readonly PropertyAccessor? _keyProperty;

  /// <summary>
  /// How the identifier of the element type is stored in BSON, resolved on first use.
  /// </summary>
  private BsonSerializationInfo? _keySerializationInfo;

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

  /// <summary>
  /// How the identifier of the element type is stored in BSON.
  /// </summary>
  private BsonSerializationInfo KeySerializationInfo =>
    _keySerializationInfo ??= Utils.GetSerializationInfo(CollectionType, _keyProperty!.Name);

  #endregion

  #region Methods

  /// <inheritdoc/>
  protected override object GetKey(object item)
  {
    if (_keyProperty == null)
      return base.GetKey(item);

    return _keyProperty.GetValue(item)
      ?? throw new InvalidOperationException(
        $"Element of type '{CollectionType.Name}' has no value in identifier '{_keyProperty.Name}'.");
  }

  /// <inheritdoc/>
  public override void TrackChanges(IEnumerable updatedSet)
  {
    base.TrackChanges(updatedSet);

    // Compare each element still present in the set with its original state. For keyed sets the element
    // may be a different instance with the same identifier, which is tracked as a modification of the original
    _someModified = false;
    foreach (object? o in Collection)
    {
      if (!_childObjects.TryGetValue(GetKey(o), out ChildTracker<T>? trackedObject)) continue;
      trackedObject.TrackChanges(o);
      _someModified |= trackedObject.IsModified;
    }
  }

  /// <inheritdoc/>
  public override UpdateDefinition<T>? GetUpdateDefinition(string? parentPropertyName, string propertyName,
    UpdateContext context)
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
      // Without an identifier elements can only be matched by their whole value
      if (_keyProperty == null) return updateBuilder.PullAll(setFullName, RemovedItems);

      // Remove elements by identifier, so concurrent changes of their other fields do not prevent removal
      var keys = new BsonArray(RemovedItems!.Select(i => Utils.SerializeValue(KeySerializationInfo.Serializer, GetKey(i))));
      return new PullByKeyUpdateDefinition<T>(setFullName!, KeySerializationInfo.ElementName, keys);
    }

    // If only modified elements exist (no added or removed)
    if (_someModified && !someAdded && !someRemoved)
    {
      // Identify elements present in both sets that were modified, addressing them by identifier when possible
      // and by their position in the stored array otherwise
      List<UpdateDefinition<T>> updatedItems = OriginalCollection
        .Select((item, index) => (Item: item, Tracker: _childObjects[GetKey(item)], Index: index))
        .Where(item => item.Tracker.IsModified)
        .Select(item => GetElementUpdate(item.Item, item.Tracker, item.Index, setFullName!, context))
        .ToList();

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

  /// <summary>
  /// Builds the update of a modified element of the stored array.
  /// </summary>
  /// <param name="item">The element to update.</param>
  /// <param name="tracker">The tracker of the element.</param>
  /// <param name="index">The position of the element in the originally loaded array.</param>
  /// <param name="setFullName">The path of the array.</param>
  /// <param name="context">Shared state of the update being built, receives the array filter for keyed elements.</param>
  /// <returns>An update addressing keyed elements by identifier and other elements by position.</returns>
  private UpdateDefinition<T> GetElementUpdate(object item, ChildTracker<T> tracker, int index, string setFullName,
    UpdateContext context)
  {
    if (_keyProperty == null) return tracker.GetUpdateDefinition(setFullName, index.ToString(), context);

    BsonValue key = Utils.SerializeValue(KeySerializationInfo.Serializer, GetKey(item));
    string identifier = context.AddArrayFilter(KeySerializationInfo.ElementName, key);

    // Build the element update with the "$" placeholder, so the driver resolves element names and serializers
    UpdateDefinition<T> update = tracker.GetUpdateDefinition(setFullName,
      FilteredPositionalUpdateDefinition<T>.Placeholder, context);

    // The placeholder of this array is the next positional segment after the ones already in the path
    int position = FilteredPositionalUpdateDefinition<T>.CountPositionalSegments(setFullName) + 1;
    return new FilteredPositionalUpdateDefinition<T>(update, position, identifier);
  }

  /// <inheritdoc/>
  public override void AcceptVersions()
  {
    foreach (ChildTracker<T> child in _childObjects.Values)
      child.AcceptVersions();
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked child object set, capturing the initial set of objects.
  /// </summary>
  /// <param name="originalSet">The initial set of nested objects to track.</param>
  /// <param name="setType">The element type of the tracked set.</param>
  /// <param name="config">The tracking configuration describing how each nested object should be monitored.</param>
  /// <exception cref="InvalidOperationException">Thrown when several elements share the same identifier.</exception>
  public ChildSetTracker(IEnumerable originalSet, Type setType, IReadOnlyDictionary<Type, EntityBuilder> config)
    : base(originalSet, setType)
  {
    string? keyName = config.GetValueOrDefault(setType)?.IdentifierPropertyName;
    if (keyName != null) _keyProperty = TypeMetadata.Get(setType).Find(keyName);

    var childObjects = new Dictionary<object, ChildTracker<T>>();
    foreach (object item in Collection)
    {
      if (!childObjects.TryAdd(GetKey(item), new ChildTracker<T>(item, config)))
        throw new InvalidOperationException(
          $"Tracked set of '{setType.Name}' contains several elements with the same identity '{GetKey(item)}'.");
    }

    _childObjects = childObjects;
  }

  #endregion
}
