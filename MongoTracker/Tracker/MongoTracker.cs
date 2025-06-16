using System.Linq.Expressions;
using MongoDB.Driver;
using MongoTracker.Entities;

namespace MongoTracker.Tracker;

/// <summary>
/// Provides a simplified interface for working with MongoDB collections with built-in change tracking.
/// This class serves as a facade for accessing and managing database collections with support for
/// optimistic concurrency control.
/// </summary>
/// <typeparam name="TK">The type of the entity's primary key/identifier.</typeparam>
/// <typeparam name="T">The type of the entity being tracked, which must implement <see cref="UpdatedEntity{T}"/>.</typeparam>
/// <param name="getIdExpression">An expression that retrieves the unique identifier for entities of type T.</param>
/// <param name="optimisticConcurrencyControl">
/// Whether to enable optimistic concurrency control (default: true).
/// When enabled, updates will verify the entity hasn't changed since it was loaded.
/// </param>
/// <remarks>
/// This tracker maintains the state of entities and provides mechanisms for detecting changes
/// and applying them to the database with proper concurrency handling.
/// </remarks>
public class MongoTracker<TK, T>(Expression<Func<T, TK>> getIdExpression, bool optimisticConcurrencyControl = true)
    where T : UpdatedEntity<T>
    where TK : notnull
{
    /// <summary>
    /// Compiled expression for getting entity ID as a function
    /// </summary>
    private readonly Func<T, TK> _getId = getIdExpression.Compile();

    /// <summary>
    /// Dictionary for tracking entity changes.
    /// Key - unique entity identifier (ID).
    /// Value - the entity itself.
    /// </summary>
    private Dictionary<TK, TrackerEntry<T>> _trackedModels = new();

    /// <summary>
    /// Updates an entity.
    /// If the entity is already being tracked, returns it from the dictionary.
    /// Otherwise, adds it to the tracked models dictionary.
    /// </summary>
    /// <param name="model">Entity to update.</param>
    /// <returns>Updated entity.</returns>
    public T Track(T model)
    {
        // Check if entity with this ID exists in tracked models dictionary
        if (_trackedModels.TryGetValue(_getId(model), out var trackedEntry)) return trackedEntry.Entity;

        // If entity not found, clear all changes (if any)
        model.ClearChanges();

        // Add entity to tracked models dictionary
        _trackedModels.Add(_getId(model), new TrackerEntry<T>(model, model.LastModified));

        // Return updated entity
        return model;
    }

    /// <summary>
    /// Removes an entity from tracked models.
    /// </summary>
    /// <param name="id">Unique identifier of entity to remove.</param>
    /// <exception cref="KeyNotFoundException">Thrown when entity with specified ID is not found.</exception>
    public void Remove(TK id)
    {
        // Set entity state to "Deleted"
        _trackedModels[id].Entity.EntityState = EntityState.Deleted;
    }
    
    /// <summary>
    /// Retrieves an entity with the specified ID from tracked models.
    /// </summary>
    /// <param name="id">Unique identifier of the entity to retrieve.</param>
    /// <returns>The entity with the specified ID.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when entity with specified ID is not found.</exception>
    public T Get(TK id)
    {
        return _trackedModels[id].Entity;
    }
    
    /// <summary>
    /// Adds a new entity to tracked models.
    /// </summary>
    /// <param name="model">Entity to add.</param>
    /// <exception cref="ArgumentException">Thrown when entity is already marked as deleted.</exception>
    public void Add(T model)
    {
        // Verify entity isn't marked as deleted
        if (model.EntityState == EntityState.Deleted) throw new ArgumentException("Model cannot be added");

        // Set entity state to "Added"
        model.EntityState = EntityState.Added;

        // Add entity to tracked models dictionary
        _trackedModels.Add(_getId(model), new TrackerEntry<T>(model, model.LastModified));
    }

    /// <summary>
    /// Commits changes and prepares operations for MongoDB execution.
    /// </summary>
    /// <returns>Collection of operations to execute in MongoDB.</returns>
    public IReadOnlyCollection<WriteModel<T>> Commit()
    {
        // Find all added entities (state == Added)
        var added = _trackedModels
            .Where(s => s.Value.Entity.EntityState == EntityState.Added) // Filter only added
            .Select(v => v.Value.Entity) // Take entity itself (without key)
            .ToArray(); // Convert to array

        // Find all deleted entities (state == Deleted)
        var deleted = _trackedModels
            .Where(s => s.Value.Entity.EntityState == EntityState.Deleted) // Filter only deleted
            .ToArray(); // Convert to array

        // Find all modified entities (state == Modified)
        var modified = _trackedModels
            .Where(s => s.Value.Entity.EntityState == EntityState.Modified) // Filter only modified
            .ToArray(); // Convert to array

        // Create list to store all BulkWrite operations
        var bulkOperations = new List<WriteModel<T>>();

        // Add insert operations for new entities
        foreach (var entity in added)
        {
            // Create InsertOne operation for each added entity
            bulkOperations.Add(new InsertOneModel<T>(entity));
        }

        // Add delete operations for deleted entities
        foreach (var entry in deleted)
        {
            // Create filter to find entity by ID
            var filter = Builders<T>.Filter.Eq(getIdExpression, entry.Key); // Eq means "equals"

            // If optimistic concurrency control is enabled
            if (optimisticConcurrencyControl)
            {
                // Add a condition to check that the LastModified field matches
                // This ensures we only delete if the record hasn't been modified by someone else
                var concurrencyFilter = Builders<T>.Filter.Eq(t => t.LastModified, entry.Value.LastModified);

                // Combine both filters (ID match AND LastModified match)
                filter = Builders<T>.Filter.And(filter, concurrencyFilter);
            }

            // Create delete operation for found entity
            // This will be executed as part of the bulk operation
            bulkOperations.Add(new DeleteOneModel<T>(filter));
        }

        // Add update operations for modified entities
        foreach (var entry in modified)
        {
            // Create filter to find entity by ID
            var filter = Builders<T>.Filter.Eq(getIdExpression, entry.Key); // Eq means "equals"

            // If optimistic concurrency control is enabled
            if (optimisticConcurrencyControl)
            {
                // Add a condition to check that the LastModified field matches
                // This ensures we only update if the record hasn't been modified by someone else
                var concurrencyFilter = Builders<T>.Filter.Eq(t => t.LastModified, entry.Value.LastModified);

                // Combine both filters (ID match AND LastModified match)
                filter = Builders<T>.Filter.And(filter, concurrencyFilter);
            }

            // Create update operation
            // UpdateDefinition is a special property from UpdatedEntity<T> interface
            // that describes which fields need to be updated and their new values
            bulkOperations.Add(new UpdateOneModel<T>(filter, entry.Value.Entity.UpdateDefinition));
        }

        // Create a new dictionary to store the updated tracking state
        // We initialize it with the same capacity as the current tracked models collection
        // to maintain optimal memory allocation
        var newDictionary = new Dictionary<TK, TrackerEntry<T>>(_trackedModels.Count);

        // Process each entity in the current tracking dictionary
        foreach (var entry in _trackedModels)
        {
            // Clear any change tracking markers from the entity
            // This prepares the entity for the next round of change detection
            entry.Value.Entity.ClearChanges();

            // Create a new tracking entry in our refreshed dictionary:
            // - The entity itself (now with cleared change markers)
            // - The last modified timestamp (preserved from previous tracking)
            newDictionary[entry.Key] = new TrackerEntry<T>(entry.Value.Entity, entry.Value.Entity.LastModified);
        }

        // Replace the old tracking dictionary with our new refreshed version
        // This effectively resets our change tracking while preserving the entity states
        _trackedModels = newDictionary;

        // Return the collection of bulk operations to be executed
        // These operations represent all the changes that were detected in this cycle
        return bulkOperations;
    }
}