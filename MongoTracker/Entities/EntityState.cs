namespace MongoTracker.Entities;

/// <summary>
/// Enumeration representing the state of an entity.
/// </summary>
public enum EntityState
{
    /// <summary>
    /// Default state (entity has not been modified, added or deleted).
    /// </summary>
    Default,

    /// <summary>
    /// The entity has been added.
    /// </summary>
    Added,

    /// <summary>
    /// The entity has been modified.
    /// </summary>
    Modified,

    /// <summary>
    /// The entity has been deleted.
    /// </summary>
    Deleted
}