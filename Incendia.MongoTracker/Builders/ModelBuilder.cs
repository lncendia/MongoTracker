namespace Incendia.MongoTracker.Builders;

/// <summary>
/// Main builder for configuring the data model
/// </summary>
public class ModelBuilder
{
  /// <summary>
  /// Dictionary of configured entity builders keyed by entity type
  /// </summary>
  private readonly Dictionary<Type, EntityBuilder> _entities = new();

  /// <summary>
  /// Configures an entity type
  /// </summary>
  /// <param name="configure">Action that configures the entity</param>
  /// <typeparam name="TEntity">The entity type to configure</typeparam>
  /// <returns>The entity builder for fluent configuration</returns>
  public EntityBuilder<TEntity> Entity<TEntity>(Action<EntityBuilder<TEntity>> configure)
  {
    Type type = typeof(TEntity);

    // If entity is already configured, reuse the existing builder
    if (_entities.TryGetValue(type, out EntityBuilder? existing))
    {
      var builder = (EntityBuilder<TEntity>)existing;
      configure(builder);
      return builder;
    }

    // Otherwise create new builder and store it
    var e = new EntityBuilder<TEntity>();
    configure(e);
    _entities[type] = e;
    return e;
  }

  /// <summary>
  /// Gets all configured entities
  /// </summary>
  internal IReadOnlyDictionary<Type, EntityBuilder> Entities => _entities;
}
