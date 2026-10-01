using System.Linq.Expressions;

using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Nodes;
using Incendia.MongoTracker.Enums;
using Incendia.MongoTracker.Exceptions;

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

  /// <summary>
  /// IDs of entities included as deletions in the last commit, pending until the write is accepted
  /// </summary>
  private readonly List<object> _pendingDeleted = [];

  /// <summary>
  /// IDs of entities included as updates in the last commit, pending until the write is accepted
  /// </summary>
  private readonly List<object> _pendingModified = [];

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
    // Forget the results of a previous commit that was never accepted
    _pendingDeleted.Clear();
    _pendingModified.Clear();

    // All entities that should be deleted
    object[] deleted = _tracked
      .Where(s => s.Value.EntityState == EntityState.Deleted)
      .Select(v => v.Key)
      .ToArray();

    // Entities that may have been modified (including ones already detected as modified by a failed save)
    object[] probablyModified = _tracked
      .Where(s => s.Value.EntityState != EntityState.Deleted)
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
      _pendingDeleted.Add(id);
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
      bulkOperations.Add(tracked.CreateUpdateModel(filter));
      _pendingModified.Add(id);
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

    AcceptChanges(result);
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

    AcceptChanges(result);

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
  /// Verifies the write result and accepts the committed changes into the tracker state.
  /// </summary>
  /// <param name="result">The result of the write operation.</param>
  /// <exception cref="MongoConcurrencyException">
  /// Thrown when fewer documents were updated or deleted than expected. Changes are not accepted in that case.
  /// </exception>
  private void AcceptChanges(BulkWriteResult<T> result)
  {
    // Unacknowledged writes carry no counts, so the conflict check is only possible for acknowledged ones
    if (result.IsAcknowledged
        && (result.MatchedCount < _pendingModified.Count || result.DeletedCount < _pendingDeleted.Count))
    {
      throw new MongoConcurrencyException(_pendingModified.Count, result.MatchedCount,
        _pendingDeleted.Count, result.DeletedCount);
    }

    // Stop tracking deleted entities
    foreach (object id in _pendingDeleted)
    {
      _tracked.Remove(id);
      _objects.Remove(id);
    }

    // Write new versions into updated entities and take a fresh snapshot of them
    foreach (object id in _pendingModified)
    {
      T entity = _objects[id];
      _tracked[id].AcceptVersions();
      _tracked[id] = new EntityTracker<T>(entity, _modelBuilder.Entities);
    }

    // Start tracking inserted entities
    if (result.IsAcknowledged)
    {
      foreach (T added in _added)
      {
        Track(added);
      }
    }

    _added.Clear();
    _pendingDeleted.Clear();
    _pendingModified.Clear();
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
