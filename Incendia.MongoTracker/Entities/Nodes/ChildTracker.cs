using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Entities.Base;

namespace Incendia.MongoTracker.Entities.Nodes;

/// <summary>
/// Represents a tracked nested object inside a parent entity.
/// </summary>
/// <param name="entity">The nested object instance whose state should be tracked.</param>
/// <param name="config">The tracking configuration describing how the object's properties should be monitored.</param>
/// <typeparam name="T">The root entity type used for building MongoDB update definitions.</typeparam>
internal class ChildTracker<T>(object entity, IReadOnlyDictionary<Type, EntityBuilder> config)
  : ChangeTrackerBase<T>(entity, config) where T : class;
