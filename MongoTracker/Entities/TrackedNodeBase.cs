using System.Collections;
using System.Reflection;

using MongoDB.Driver;
using MongoTracker.Builders;

namespace MongoTracker.Entities;

/// <summary>
/// Base class that tracks changes of an entity’s properties, nested objects, and collections.
/// </summary>
internal abstract class TrackedNodeBase<T> where T : class
{
  #region Fields

  /// <summary>
  /// Stores the initial state of all tracked properties.
  /// </summary>
  protected readonly Dictionary<string, object?> State = new();

  /// <summary>
  /// Stores the modified values of simple properties.
  /// </summary>
  private readonly Dictionary<string, object?> _changes = new();

  /// <summary>
  /// Stores tracked nested objects (single object).
  /// </summary>
  private readonly Dictionary<string, TrackedChildObject<T>> _childObjects = new();

  /// <summary>
  /// Stores tracked primitive or value-type collections.
  /// </summary>
  private readonly Dictionary<string, TrackedCollection<T>> _collections = new();

  /// <summary>
  /// Stores tracked collections of nested objects.
  /// </summary>
  private readonly Dictionary<string, TrackedChildObjectCollection<T>> _childObjectCollections = new();

  /// <summary>
  /// Entity metadata loaded from configuration.
  /// </summary>
  protected readonly EntityBuilder? EntityConfig;

  #endregion

  #region Properties

  /// <summary>
  /// Indicates whether this node or any of its nested tracked objects or collections
  /// contain modifications compared to the original captured state.
  /// </summary>
  public bool IsModified => _changes.Count > 0
                            || _childObjects.Values.Any(v => v.IsModified)
                            || _collections.Values.Any(v => v.IsModified)
                            || _childObjectCollections.Values.Any(v => v.IsModified);

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
    foreach (string? propName in State.Keys)
    {
      // Get config for the property (its tracking mode)
      PropertyConfig? config = EntityConfig?.Properties.GetValueOrDefault(propName);
      object? newValue = type.GetProperty(propName)!.GetValue(updatedEntity);

      // Delegate change detection depending on property type
      switch (config?.Kind)
      {
        case PropertyKind.Version:
        case PropertyKind.Identifier:
          break;

        case PropertyKind.TrackedObject:
          TrackChildObject(propName, newValue);
          break;

        case PropertyKind.Collection:
          TrackCollection(propName, newValue);
          break;

        case PropertyKind.TrackedObjectCollection:
          TrackChildObjectCollection(propName, newValue);
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
    object? oldValue = State[name];

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
    object? old = State[name];

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
    object? old = State[name];

    // Collection added → mark whole object as changed
    if (old == null && value != null)
    {
      _changes[name] = value;
      return;
    }

    // Collection removed → record null
    if (old != null && value == null)
    {
      _changes[name] = null;
      _collections.Remove(name);
      return;
    }

    // Existing collection updated → track item-level changes
    if (value is IEnumerable e)
      _collections[name].TrackChanges(e.Cast<object>());
  }

  /// <summary>
  /// Tracks changes in a collection of nested tracked objects.
  /// </summary>
  /// <param name="name">The name of the tracked object collection property.</param>
  /// <param name="value">The new collection of objects, or null if removed.</param>
  private void TrackChildObjectCollection(string name, object? value)
  {
    // Get original collection of nested objects
    object? old = State[name];

    // Collection added
    if (old == null && value != null)
    {
      _changes[name] = value;
      return;
    }

    // Collection removed
    if (old != null && value == null)
    {
      _changes[name] = null;
      _childObjectCollections.Remove(name);
      return;
    }

    // Existing collection updated → deep tracking for nested items
    if (value is IEnumerable e)
      _childObjectCollections[name].TrackChanges(e.Cast<object>());
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

    IEnumerable<KeyValuePair<string,object?>> updatesQuery = _changes;

    if (EntityConfig?.VersionPropertyName != null)
      updatesQuery = updatesQuery.Where(ch => !string.Equals(ch.Key, EntityConfig.VersionPropertyName));

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

    // Generate updates for collections of child objects
    IEnumerable<UpdateDefinition<T>?> childObjectCollectionUpdates = _childObjectCollections
      .Where(v => v.Value.IsModified)
      .Select(v => v.Value.GetUpdateDefinition(fullName, v.Key));

    // Combine all update definitions into a single MongoDB update document
    IEnumerable<UpdateDefinition<T>?> updates = propertyUpdates
      .Union(childObjectUpdates)
      .Union(collectionUpdates)
      .Union(childObjectCollectionUpdates);

    // If a version field is configured, update it with the current date/time
    if (EntityConfig?.VersionPropertyName != null)
    {
      // Compute the fully qualified field name (including nesting)
      string? fieldName = Utils.CombineName(fullName, EntityConfig.VersionPropertyName);

      // Generate an update that sets the version field to the current date
      UpdateDefinition<T>? versionUpdate = updateBuilder.CurrentDate(fieldName, type: UpdateDefinitionCurrentDateType.Date);

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
  protected TrackedNodeBase(object entity, IReadOnlyDictionary<Type, EntityBuilder> config)
  {
    // Find configuration for the entity's exact type
    EntityConfig = config.GetValueOrDefault(entity.GetType());

    // Get all writable + readable properties to track
    Type type = entity.GetType();
    IEnumerable<PropertyInfo> properties = type.GetProperties()
      .Where(p => p.CanRead && p.CanWrite);

    // Initialize tracking structures for each property
    foreach (PropertyInfo? property in properties)
    {
      // Retrieve property-specific config, if defined
      PropertyConfig? propConfig = EntityConfig?.Properties.GetValueOrDefault(property.Name);
      object? value = property.GetValue(entity);

      // Skipping the ID from tracking
      if (propConfig?.Kind is PropertyKind.Identifier)
        continue;

      // Store the initial value in state dictionary
      State[property.Name] = value;

      // Initialize nested tracked object
      if (propConfig?.Kind == PropertyKind.TrackedObject && value != null)
        _childObjects[property.Name] = new TrackedChildObject<T>(value, config);

      // Initialize tracked collection of primitive/value types
      else if (propConfig?.Kind == PropertyKind.Collection && value is IEnumerable enumerable)
        _collections[property.Name] = new TrackedCollection<T>(enumerable.Cast<object>());

      // Initialize tracked collection of nested objects
      else if (propConfig?.Kind == PropertyKind.TrackedObjectCollection && value is IEnumerable col)
        _childObjectCollections[property.Name] = new TrackedChildObjectCollection<T>(col.Cast<object>(), config);
    }
  }

  #endregion
}
