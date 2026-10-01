using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Base;
using Incendia.MongoTracker.Enums;

using MongoDB.Driver;

namespace Incendia.MongoTracker.Entities.Nodes;

/// <summary>
/// Represents a top-level tracked entity.
/// </summary>
/// <typeparam name="T">The type of the entity being tracked.</typeparam>
internal class EntityTracker<T> : ChangeTrackerBase<T> where T : class
{
  #region Properties

  /// <summary>
  /// Gets or sets the current state of the tracked entity.
  /// </summary>
  public EntityState EntityState
  {
    get => GetEntityState(field);
    set;
  } = EntityState.Default;

  /// <summary>
  /// Generates a MongoDB <see cref="UpdateDefinition{T}"/> representing all changes detected in the entity.
  /// </summary>
  /// <exception cref="InvalidOperationException">
  /// Thrown when attempting to access the update definition while the entity is not modified.
  /// </exception>
  public UpdateDefinition<T> UpdateDefinition => BuildUpdateDefinition(new UpdateContext());

  #endregion

  #region Methods

  /// <summary>
  /// Creates a MongoDB update operation representing all changes detected in the entity.
  /// </summary>
  /// <param name="filter">The filter selecting the document to update.</param>
  /// <returns>The update operation, including the array filters its update refers to.</returns>
  /// <exception cref="InvalidOperationException">Thrown when the entity is not modified.</exception>
  public UpdateOneModel<T> CreateUpdateModel(FilterDefinition<T> filter)
  {
    var context = new UpdateContext();
    var model = new UpdateOneModel<T>(filter, BuildUpdateDefinition(context));

    if (context.ArrayFilters.Count > 0)
      model.ArrayFilters = context.ArrayFilters;

    return model;
  }

  /// <summary>
  /// Builds the update definition of the entity, registering the array filters it refers to in the context.
  /// </summary>
  /// <param name="context">Shared state of the update being built.</param>
  /// <returns>The full MongoDB update definition.</returns>
  /// <exception cref="InvalidOperationException">Thrown when the entity is not modified.</exception>
  private UpdateDefinition<T> BuildUpdateDefinition(UpdateContext context)
  {
    // Ensure the entity is marked as modified before generating the update
    if (EntityState != EntityState.Modified)
      throw new InvalidOperationException("Entity is not modified");

    // Build and return the full MongoDB update definition
    return GetUpdateDefinition(null, null, context);
  }

  /// <summary>
  /// Determines the effective entity state based on the provided state and the modification status.
  /// </summary>
  /// <param name="state">The current or default entity state to evaluate.</param>
  /// <returns>The effective <see cref="EntityState"/> taking into account modifications.</returns>
  private EntityState GetEntityState(EntityState state)
  {
    if (state != EntityState.Default) return state;
    return IsModified ? EntityState.Modified : state;
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Initializes a new tracked entity by capturing its initial state.
  /// </summary>
  /// <param name="entity">The entity instance to be tracked.</param>
  /// <param name="config">The configuration that describes which properties should be tracked and how.</param>
  public EntityTracker(T entity, IReadOnlyDictionary<Type, EntityBuilder> config) : base(entity, config)
  {
  }

  #endregion
}
