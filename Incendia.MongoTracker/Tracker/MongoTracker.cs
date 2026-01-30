using System.Linq.Expressions;

using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Enums;

using MongoDB.Driver;

namespace Incendia.MongoTracker.Tracker;

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
  /// Tracks all added entities
  /// </summary>
  private readonly List<T> _added = [];

  /// <summary>
  /// Tracks all internal states and changes for each entity
  /// </summary>
  private readonly Dictionary<object, EntityTracker<T>> _tracked = new();

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
  public virtual T Track(T model)
  {
    object? id = _getId(model);

    // If entity already exists in local cache — return existing tracked copy
    if (_objects.TryGetValue(id, out T? entity)) return entity;

    // Create new tracked wrapper
    _tracked.Add(id, new EntityTracker<T>(model, _modelBuilder.Entities));

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
  public virtual void Delete(T model)
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
  public virtual T Get(object id)
  {
    if (_objects.TryGetValue(id, out T? model))
      return model;

    T? inserted = _added.FirstOrDefault(o => _getId(o).Equals(id));
    return inserted ?? throw new KeyNotFoundException();
  }

  /// <summary>
  /// Adds a new entity to tracking with state "Added".
  /// </summary>
  /// <param name="model">Entity to add.</param>
  /// <exception cref="ArgumentException">Thrown when entity is already tracked.</exception>
  public virtual void Add(T model)
  {
    // Prevent tracking duplicates
    if (_objects.ContainsKey(_getId(model))) throw new ArgumentException("Entity already tracked");
    _added.Add(model);
  }

  /// <summary>
  /// Computes and prepares all MongoDB write operations based on tracked changes.
  /// </summary>
  /// <returns>Collection of WriteModels for BulkWrite().</returns>
  protected virtual IReadOnlyCollection<WriteModel<T>> Commit()
  {
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
    foreach (T added in _added)
    {
      bulkOperations.Add(new InsertOneModel<T>(added));
    }

    // DELETE operations
    foreach (object id in deleted)
    {
      EntityTracker<T>? tracked = _tracked[id];

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
      EntityTracker<T>? tracked = _tracked[id];
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
      _tracked[id] = new EntityTracker<T>(entity, _modelBuilder.Entities);
    }

    // Return prepared MongoDB operations
    return bulkOperations;
  }

  /// <summary>
  /// Performs multiple write operations.
  /// </summary>
  /// <param name="collection">The collection of documents.</param>
  /// <param name="session">The session.</param>
  /// <param name="options">The options.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>The result of writing.</returns>
  public virtual BulkWriteResult<T>? SaveChanges(IMongoCollection<T> collection, IClientSessionHandle? session,
    BulkWriteOptions? options = null, CancellationToken cancellationToken = default)
  {
    IReadOnlyCollection<WriteModel<T>> requests = Commit();

    if (requests.Count <= 0) return null;

    BulkWriteResult<T> result = session == null
      ? collection.BulkWrite(requests, options, cancellationToken)
      : collection.BulkWrite(session, requests, options, cancellationToken);

    TrackAddedIfAcknowledged(result);
    return result;
  }

  /// <summary>
  /// Performs multiple write operations.
  /// </summary>
  /// <param name="collection">The collection of documents.</param>
  /// <param name="session">The session.</param>
  /// <param name="options">The options.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>The result of writing.</returns>
  public virtual async Task<BulkWriteResult<T>?> SaveChangesAsync(IMongoCollection<T> collection,
    IClientSessionHandle? session, BulkWriteOptions? options = null,
    CancellationToken cancellationToken = default)
  {
    IReadOnlyCollection<WriteModel<T>> requests = Commit();

    if (requests.Count <= 0) return null;

    BulkWriteResult<T> result = session == null
      ? await collection.BulkWriteAsync(requests, options, cancellationToken)
      : await collection.BulkWriteAsync(session, requests, options, cancellationToken);

    TrackAddedIfAcknowledged(result);

    return result;
  }

  /// <summary>
  /// Performs multiple write operations.
  /// </summary>
  /// <param name="collection">The collection of documents.</param>
  /// <param name="options">The options.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>The result of writing.</returns>
  public BulkWriteResult<T>? SaveChanges(IMongoCollection<T> collection, BulkWriteOptions? options = null,
    CancellationToken cancellationToken = default)
  {
    return SaveChanges(collection, null, options, cancellationToken);
  }

  /// <summary>
  /// Performs multiple write operations.
  /// </summary>
  /// <param name="collection">The collection of documents.</param>
  /// <param name="options">The options.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>The result of writing.</returns>
  public Task<BulkWriteResult<T>?> SaveChangesAsync(IMongoCollection<T> collection, BulkWriteOptions? options = null,
    CancellationToken cancellationToken = default)
  {
    return SaveChangesAsync(collection, null, options, cancellationToken);
  }

  /// <summary>
  /// Finalizes adding entities after a successful write operation.
  /// </summary>
  /// <param name="result"> The result of the write operation.</param>
  private void TrackAddedIfAcknowledged(BulkWriteResult<T> result)
  {
    if (result.IsAcknowledged)
    {
      foreach (T added in _added)
      {
        Track(added);
      }
    }

    _added.Clear();
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
