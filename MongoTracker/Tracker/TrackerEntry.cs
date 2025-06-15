namespace MongoTracker.Tracker;

/// <summary>
/// Represents a tracked entity with its last modification timestamp, used for change tracking
/// and optimistic concurrency control.
/// </summary>
/// <typeparam name="T">The type of the entity being tracked.</typeparam>
/// <param name="entry">The entity instance being tracked.</param>
/// <param name="lastModified">The timestamp when the entity was last modified.</param>
public readonly struct TrackerEntry<T>(T entry, DateTime lastModified)
{
    /// <summary>
    /// Gets the tracked entity instance.
    /// </summary>
    /// <value>The entity being tracked for changes.</value>
    public T Entity { get; } = entry;

    /// <summary>
    /// Gets the timestamp of the entity's last modification.
    /// This value is used for optimistic concurrency control to detect
    /// concurrent modifications between load and save operations.
    /// </summary>
    /// <value>The UTC timestamp when the entity was last modified.</value>
    public DateTime LastModified { get; } = lastModified;
}