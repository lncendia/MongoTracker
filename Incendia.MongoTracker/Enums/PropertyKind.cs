namespace Incendia.MongoTracker.Enums;

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
  Child,

  /// <summary>
  /// Property that represents a set of values
  /// </summary>
  Set,

  /// <summary>
  /// Property that represents a set of tracked objects
  /// </summary>
  TrackedSet,

  /// <summary>
  /// Property that represents a collection of values
  /// </summary>
  Collection,

  /// <summary>
  /// Property used for optimistic concurrency control (version field)
  /// </summary>
  Version,

  /// <summary>
  /// Self-management property used for optimistic concurrency control
  /// </summary>
  ConcurrencyToken,

  /// <summary>
  /// Property that is ignored by persistence and serialization mechanisms.
  /// </summary>
  Ignored
}
