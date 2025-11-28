using MongoDB.Driver;
using MongoTracker.Builders;

namespace MongoTracker.Entities;

/// <summary>
/// Represents a top-level tracked entity.
/// </summary>
/// <typeparam name="T">The type of the entity being tracked.</typeparam>
internal class TrackedEntity<T> : TrackedNodeBase<T> where T : class
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
  public UpdateDefinition<T> UpdateDefinition
  {
    get
    {
      // Ensure the entity is marked as modified before generating the update
      if (EntityState != EntityState.Modified)
        throw new InvalidOperationException("Entity is not modified");

      // Build and return the full MongoDB update definition
      return GetUpdateDefinition(null, null);
    }
  }

  #endregion

  #region Methods

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
  public TrackedEntity(T entity, IReadOnlyCollection<EntityBuilder> config) : base(entity, config)
  {
  }

  #endregion
}