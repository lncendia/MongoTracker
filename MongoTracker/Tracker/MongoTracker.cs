using System.Linq.Expressions;
using MongoDB.Driver;
using MongoTracker.Builders;
using MongoTracker.Entities;

namespace MongoTracker.Tracker;

/// <summary>
/// Provides a simplified interface for working with MongoDB collections with built-in change tracking.
/// </summary>
public class MongoTracker<T> where T : class
{
  #region Fields

  /// <summary>
  /// Compiled expression for getting entity ID as a function
  /// </summary>
  private readonly Func<T, object> _getId;

  /// <summary>
  /// Expression used to build filters for querying entities by ID
  /// </summary>
  private readonly Expression<Func<T, object>> _getIdExpression;

  /// <summary>
  /// Tracks all internal states and changes for each entity
  /// </summary>
  private readonly Dictionary<object, TrackedEntity<T>> _tracked = new();

  /// <summary>
  /// Stores original entity instances exposed to the user
  /// </summary>
  private readonly Dictionary<object, T> _objects = new();

  /// <summary>
  /// Model configuration used for tracking and versioning
  /// </summary>
  private readonly ModelBuilder _modelBuilder;

  #endregion

  #region Methods

  /// <summary>
  /// Starts tracking the specified model instance.
  /// </summary>
  /// <param name="model">Entity to update.</param>
  /// <returns>Tracked instance (original model).</returns>
  public T Track(T model)
  {
    object? id = _getId(model);

    // If entity already exists in local cache — return existing tracked copy
    if (_objects.TryGetValue(id, out T? entity)) return entity;

    // Create new tracked wrapper
    _tracked.Add(id, new TrackedEntity<T>(model, _modelBuilder.Entities));

    // Cache original object for future access
    _objects.Add(id, model);

    // Return original object (now tracked)
    return model;
  }

  /// <summary>
  /// Marks entity as deleted.
  /// </summary>
  /// <param name="model">Entity to delete.</param>
  /// <exception cref="KeyNotFoundException">Thrown when entity with specified ID is not found.</exception>
  public void Delete(T model)
  {
    object? id = _getId(model);

    // Set deletion state without removing from memory
    _tracked[id].EntityState = EntityState.Deleted;
  }

  /// <summary>
  /// Retrieves an entity with the specified ID from tracked models.
  /// </summary>
  /// <param name="id">Unique identifier of the entity to retrieve.</param>
  /// <returns>The entity with the specified ID.</returns>
  public T Get(object id)
  {
    return _objects[id];
  }

  /// <summary>
  /// Adds a new entity to tracking with state "Added".
  /// </summary>
  /// <param name="model">Entity to add.</param>
  /// <exception cref="ArgumentException">Thrown when entity is already tracked.</exception>
  public void Add(T model)
  {
    // Prevent tracking duplicates
    if (_objects.ContainsKey(_getId(model))) throw new ArgumentException("Entity already tracked");

    object? id = _getId(model);

    // Register entity in tracking system
    _tracked.Add(id, new TrackedEntity<T>(model, _modelBuilder.Entities));
    _objects.Add(id, model);

    // Mark as newly added
    _tracked[id].EntityState = EntityState.Added;
  }

  /// <summary>
  /// Computes and prepares all MongoDB write operations based on tracked changes.
  /// </summary>
  /// <returns>Collection of WriteModels for BulkWrite().</returns>
  public IReadOnlyCollection<WriteModel<T>> Commit()
  {
    // All entities that should be inserted
    object[] added = _tracked
      .Where(s => s.Value.EntityState == EntityState.Added)
      .Select(v => v.Key)
      .ToArray();

    // All entities that should be deleted
    object[] deleted = _tracked
      .Where(s => s.Value.EntityState == EntityState.Deleted)
      .Select(v => v.Key)
      .ToArray();

    // Entities that may have been modified
    object[] probablyModified = _tracked
      .Where(s => s.Value.EntityState == EntityState.Default)
      .Select(v => v.Key)
      .ToArray();

    // Final list of MongoDB operations
    var bulkOperations = new List<WriteModel<T>>();

    // INSERT operations
    foreach (object id in added)
    {
      bulkOperations.Add(new InsertOneModel<T>(_objects[id]));

      // Reset tracker state for model
      _tracked[id].EntityState = EntityState.Default;
    }

    // DELETE operations
    foreach (object id in deleted)
    {
      TrackedEntity<T>? tracked = _tracked[id];

      // Build filter by ID
      FilterDefinition<T>? filter = Builders<T>.Filter.Eq(_getIdExpression, id);

      KeyValuePair<string, object?>? version = tracked.Version;
      IReadOnlyList<KeyValuePair<string, object?>> tokens = tracked.ConcurrencyTokens;

      // Add optimistic concurrency check
      if (version.HasValue)
      {
        FilterDefinition<T>? concurrencyFilter = Builders<T>.Filter.Eq(version.Value.Key, version.Value.Value);
        filter = Builders<T>.Filter.And(filter, concurrencyFilter);
      }

      foreach (KeyValuePair<string, object?> token in tokens)
      {
        FilterDefinition<T>? concurrencyFilter = Builders<T>.Filter.Eq(token.Key, token.Value);
        filter = Builders<T>.Filter.And(filter, concurrencyFilter);
      }

      bulkOperations.Add(new DeleteOneModel<T>(filter));

      // Reset tracker state for model
      _tracked.Remove(id);
      _objects.Remove(id);
    }

    // UPDATE operations
    foreach (object id in probablyModified)
    {
      TrackedEntity<T>? tracked = _tracked[id];
      T? entity = _objects[id];

      // Compute diff between original and current state
      tracked.TrackChanges(entity);

      // Skip if no modifications
      if (tracked.EntityState != EntityState.Modified) continue;

      // Build filter by ID
      FilterDefinition<T>? filter = Builders<T>.Filter.Eq(_getIdExpression, id);

      KeyValuePair<string, object?>? version = tracked.Version;
      IReadOnlyList<KeyValuePair<string, object?>> tokens = tracked.ConcurrencyTokens;

      // Add optimistic concurrency check
      if (version.HasValue)
      {
        FilterDefinition<T>? concurrencyFilter = Builders<T>.Filter.Eq(version.Value.Key, version.Value.Value);
        filter = Builders<T>.Filter.And(filter, concurrencyFilter);
      }

      foreach (KeyValuePair<string, object?> token in tokens)
      {
        FilterDefinition<T>? concurrencyFilter = Builders<T>.Filter.Eq(token.Key, token.Value);
        filter = Builders<T>.Filter.And(filter, concurrencyFilter);
      }

      // Register UPDATE operation
      bulkOperations.Add(new UpdateOneModel<T>(filter, tracked.UpdateDefinition));

      // Reset tracker state for model
      _tracked[id] = new TrackedEntity<T>(entity, _modelBuilder.Entities);
    }

    // Return prepared MongoDB operations
    return bulkOperations;
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Creates a new instance of MongoTracker and configures ID accessor.
  /// </summary>
  /// <param name="modelBuilder">Model metadata used for mapping and tracking.</param>
  public MongoTracker(ModelBuilder modelBuilder)
  {
    _getIdExpression = Utils.GetIdentifierExpression<T>(modelBuilder.Entities);
    _modelBuilder = modelBuilder;

    // Compile ID getter for fast runtime access
    _getId = _getIdExpression.Compile();
  }

  #endregion
}
