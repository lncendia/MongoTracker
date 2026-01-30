using System.Collections;
using System.Reflection;

using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Collections;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Enums;

using MongoDB.Driver;

namespace Incendia.MongoTracker.Entities.Base;

/// <summary>
/// Base class that tracks changes of an entity’s properties, nested objects, and collections.
/// </summary>
internal abstract class ChangeTrackerBase<T> where T : class
{
  #region Fields

  /// <summary>
  /// Stores the initial state of all tracked properties.
  /// </summary>
  private readonly Dictionary<string, object?> _state = new();

  /// <summary>
  /// Stores the modified values of simple properties.
  /// </summary>
  private readonly Dictionary<string, object?> _changes = new();

  /// <summary>
  /// Stores tracked nested objects (single object).
  /// </summary>
  private readonly Dictionary<string, ChildTracker<T>> _childObjects = new();

  /// <summary>
  /// Stores tracked primitive or value-type collections.
  /// </summary>
  private readonly Dictionary<string, CollectionTrackerBase<T>> _collections = new();

  /// <summary>
  /// Entity metadata loaded from configuration.
  /// </summary>
  private readonly EntityBuilder? _entityConfig;

  #endregion

  #region Properties

  /// <summary>
  /// Concurrency token property names with their initial values (if configured)
  /// </summary>
  public IReadOnlyList<KeyValuePair<string, object?>> ConcurrencyTokens =>
    (
      _entityConfig?.ConcurrencyTokenNames
        .Select(name => new KeyValuePair<string, object?>(name, _state[name]))
      ?? []
    )
    .Concat(
      _childObjects.SelectMany(child =>
        child.Value.ConcurrencyTokens.Select(token =>
          new KeyValuePair<string, object?>(
            Utils.CombineName(child.Key, token.Key)!,
            token.Value)))
    )
    .ToList();

  /// <summary>
  /// Version property name and its initial value (if configured)
  /// </summary>
  public KeyValuePair<string, object?>? Version
  {
    get
    {
      string? name = _entityConfig?.VersionPropertyName;
      return name == null
        ? _childObjects.Values.Select(o => o.Version).FirstOrDefault(v => v != null)
        : new KeyValuePair<string, object?>(name, _state[name]);
    }
  }

  /// <summary>
  /// Indicates whether this node or any of its nested tracked objects or collections
  /// contain modifications compared to the original captured state.
  /// </summary>
  public bool IsModified => _changes.Count > 0
    || _childObjects.Values.Any(v => v.IsModified)
    || _collections.Values.Any(v => v.IsModified);

  #endregion

  #region Methods

  /// <summary>
  /// Loads the updated values from entity instance.
  /// </summary>
  /// <param name="updatedEntity">The new version of the entity containing updated property values.</param>
  public void TrackChanges(object updatedEntity)
  {
    // Use reflection on the updated entity to read new property values
    Type type = updatedEntity.GetType();

    // Compare each tracked property with the updated entity
    foreach (string? propName in _state.Keys)
    {
      PropertyInfo? property = type.GetProperty(propName);

      if (property == null || property.IsBsonIgnored())
        continue;

      // Get config for the property (its tracking mode)
      PropertyConfig? config = _entityConfig?.Properties.GetValueOrDefault(propName);
      object? newValue = property.GetValue(updatedEntity);

      // Delegate change detection depending on property type
      switch (config?.Kind)
      {
        case PropertyKind.Version:
        case PropertyKind.Ignored:
        case PropertyKind.Identifier:
          break;

        case PropertyKind.Child:
          TrackChildObject(propName, newValue);
          break;

        case PropertyKind.Set:
        case PropertyKind.Collection:
        case PropertyKind.TrackedSet:
          TrackCollection(propName, newValue);
          break;

        default:
          TrackProperty(propName, newValue);
          break;
      }
    }
  }

  /// <summary>
  /// Tracks a change in a simple (non-nested) property by comparing the old and new values.
  /// </summary>
  /// <param name="name">The name of the property being evaluated.</param>
  /// <param name="value">The new value read from the updated entity.</param>
  private void TrackProperty(string name, object? value)
  {
    // Get original value from state
    object? oldValue = _state[name];

    // No changes if both are null
    if (oldValue == null && value == null) return;

    // No changes if values equal
    if (oldValue?.Equals(value) ?? false) return;

    // Record change
    _changes[name] = value;
  }

  /// <summary>
  /// Tracks changes in a nested tracked object.
  /// </summary>
  /// <param name="name">The name of the nested object property.</param>
  /// <param name="value">The new nested object instance, or null if removed.</param>
  private void TrackChildObject(string name, object? value)
  {
    // Get original nested object reference
    object? old = _state[name];

    // New object where old was null → mark whole object as changed
    if (old == null && value != null)
    {
      _changes[name] = value;
      return;
    }

    // Object removed → record null
    if (old != null && value == null)
    {
      _changes[name] = null;
      _childObjects.Remove(name);
      return;
    }

    // Existing object updated → delegate inside-object change tracking
    if (old != null && value != null)
      _childObjects[name].TrackChanges(value);
  }

  /// <summary>
  /// Tracks changes in a collection of primitive or simple value types.
  /// </summary>
  /// <param name="name">The name of the collection property.</param>
  /// <param name="value">The new collection value, or null if removed.</param>
  private void TrackCollection(string name, object? value)
  {
    // Get original collection
    object? old = _state[name];

    // Set added → mark whole object as changed
    if (old == null && value != null)
    {
      _changes[name] = value;
      return;
    }

    // Set removed → record null
    if (old != null && value == null)
    {
      _changes[name] = null;
      _collections.Remove(name);
      return;
    }

    // Existing collection updated → track item-level changes
    if (value is IEnumerable e)
      _collections[name].TrackChanges(e);
  }

  /// <summary>
  /// Generates a MongoDB <see cref="UpdateDefinition{T}"/> that represents all accumulated changes in this node.
  /// </summary>
  /// <param name="parent">Optional parent path used when the node represents a nested structure.</param>
  /// <param name="name">The local property name of this node within the parent structure.</param>
  /// <returns>
  /// A combined <see cref="UpdateDefinition{T}"/> representing all modifications found in this node.
  /// </returns>
  public UpdateDefinition<T> GetUpdateDefinition(string? parent, string? name)
  {
    // Create update builder root and compute full field prefix
    UpdateDefinitionBuilder<T>? updateBuilder = Builders<T>.Update;
    string? fullName = Utils.CombineName(parent, name);

    // Shortcut reference
    UpdateDefinitionBuilder<T>? update = Builders<T>.Update;

    IEnumerable<KeyValuePair<string, object?>> updatesQuery = _changes;

    if (_entityConfig?.VersionPropertyName != null)
      updatesQuery = updatesQuery.Where(ch => !string.Equals(ch.Key, _entityConfig.VersionPropertyName));

    // Generate $set operations for modified simple properties
    IEnumerable<UpdateDefinition<T>> propertyUpdates = updatesQuery
      .Select(ch => updateBuilder.Set(Utils.CombineName(fullName, ch.Key), ch.Value));

    // Generate updates for modified nested objects
    IEnumerable<UpdateDefinition<T>> childObjectUpdates = _childObjects
      .Where(v => v.Value.IsModified)
      .Select(v => v.Value.GetUpdateDefinition(fullName, v.Key));

    // Generate updates for changed collections
    IEnumerable<UpdateDefinition<T>?> collectionUpdates = _collections
      .Where(v => v.Value.IsModified)
      .Select(v => v.Value.GetUpdateDefinition(fullName, v.Key));

    // Combine all update definitions into a single MongoDB update document
    IEnumerable<UpdateDefinition<T>?> updates = propertyUpdates
      .Union(childObjectUpdates)
      .Union(collectionUpdates);

    // If a version field is configured, update it with the current date/time
    if (_entityConfig?.VersionPropertyName != null)
    {
      // Compute the fully qualified field name (including nesting)
      string? fieldName = Utils.CombineName(fullName, _entityConfig.VersionPropertyName);

      // Generate an update that sets the version field to the current date
      UpdateDefinition<T>? versionUpdate =
        updateBuilder.CurrentDate(fieldName, type: UpdateDefinitionCurrentDateType.Date);

      // Add the version update to the update pipeline
      updates = updates.Append(versionUpdate);
    }

    // Return final combined update definition
    return update.Combine(updates);
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked node by capturing the initial state of all its properties.
  /// </summary>
  /// <param name="entity">The entity instance whose properties should be tracked.</param>
  /// <param name="config">The tracking configuration describing how each property should be treated.</param>
  protected ChangeTrackerBase(object entity, IReadOnlyDictionary<Type, EntityBuilder> config)
  {
    // Find configuration for the entity's exact type
    _entityConfig = config.GetValueOrDefault(entity.GetType());

    // Get all writable + readable properties to track
    Type type = entity.GetType();
    IEnumerable<PropertyInfo> properties = type.GetProperties()
      .Where(p => p.CanRead && p.CanWrite);

    // Initialize tracking structures for each property
    foreach (PropertyInfo property in properties)
    {
      if (property.IsBsonIgnored())
        continue;

      // Retrieve property-specific config, if defined
      PropertyConfig? propConfig = _entityConfig?.Properties.GetValueOrDefault(property.Name);
      object? value = property.GetValue(entity);

      // Skipping the ID from tracking
      if (propConfig?.Kind is PropertyKind.Identifier or PropertyKind.Ignored)
        continue;

      // Store the initial value in state dictionary
      _state[property.Name] = value;

      // Initialize nested tracked object
      if (propConfig?.Kind == PropertyKind.Child && value != null)
        _childObjects[property.Name] = new ChildTracker<T>(value, config);

      // Initialize collection tracked object
      else if (value is IEnumerable enumerable)
      {
        Type[] geneticArguments = property.PropertyType.GetGenericArguments();

        if (geneticArguments.Length == 0)
          continue;

        Type elementType = property.PropertyType.GetGenericArguments()[0];

        CollectionTrackerBase<T>? collection = propConfig?.Kind switch
        {
          PropertyKind.Set => new ValueSetTracker<T>(enumerable, elementType),
          PropertyKind.TrackedSet => new ChildSetTracker<T>(enumerable, elementType, config),
          PropertyKind.Collection => new ValueCollectionTracker<T>(enumerable, elementType),
          _ => null
        };

        if (collection == null)
          continue;

        _collections[property.Name] = collection;
      }
    }
  }

  #endregion
}
