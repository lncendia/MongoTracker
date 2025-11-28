namespace MongoTracker.Builders;

/// <summary>
/// Defines the different kinds of properties supported by the tracker
/// </summary>
public enum PropertyKind
{
  /// <summary>
  /// Unique identifier property (e.g., primary key)
  /// </summary>
  Identifier,

  /// <summary>
  /// Property that represents a tracked object with change monitoring
  /// </summary>
  TrackedObject,

  /// <summary>
  /// Property that represents a collection of values
  /// </summary>
  Collection,

  /// <summary>
  /// Property that represents a collection of tracked objects
  /// </summary>
  TrackedObjectCollection,

  /// <summary>
  /// Property used for optimistic concurrency control (version field)
  /// </summary>
  Version
}