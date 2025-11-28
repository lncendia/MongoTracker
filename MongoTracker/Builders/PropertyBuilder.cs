using System.Collections;

namespace MongoTracker.Builders;

/// <summary>
/// Builder for configuring property behavior and metadata
/// </summary>
/// <param name="config">The property configuration instance</param>
public class PropertyBuilder(PropertyConfig config)
{
  /// <summary>
  /// Gets the underlying property configuration
  /// </summary>
  private PropertyConfig Config { get; } = config;

  /// <summary>
  /// Marks the property as an entity identifier
  /// </summary>
  /// <returns>The property builder for fluent configuration</returns>
  public PropertyBuilder IsIdentifier()
  {
    Config.Kind = PropertyKind.Identifier;
    return this;
  }

  /// <summary>
  /// Marks the property as a tracked object
  /// </summary>
  /// <returns>The property builder for fluent configuration</returns>
  public PropertyBuilder IsTrackedObject()
  {
    Config.Kind = PropertyKind.TrackedObject;
    return this;
  }

  /// <summary>
  /// Marks the property as a collection
  /// </summary>
  /// <returns>The property builder for fluent configuration</returns>
  public PropertyBuilder IsCollection()
  {
    if (!typeof(IEnumerable).IsAssignableFrom(Config.Type))
      throw new InvalidOperationException(
        $"Property '{Config.Name}' is configured as Collection but does not implement IEnumerable.");

    Config.Kind = PropertyKind.Collection;
    return this;
  }

  /// <summary>
  /// Marks the property as a collection of tracked objects
  /// </summary>
  /// <returns>The property builder for fluent configuration</returns>
  public PropertyBuilder IsTrackedObjectCollection()
  {
    if (!typeof(IEnumerable).IsAssignableFrom(Config.Type))
      throw new InvalidOperationException(
        $"Property '{Config.Name}' is configured as TrackedObjectCollection but does not implement IEnumerable.");

    Config.Kind = PropertyKind.TrackedObjectCollection;
    return this;
  }

  /// <summary>
  /// Marks the property as a version field for optimistic concurrency control
  /// </summary>
  /// <returns>The property builder for fluent configuration</returns>
  public PropertyBuilder IsVersion()
  {
    if (Config.Type != typeof(DateTime) && Config.Type != typeof(DateTime?))
      throw new InvalidOperationException(
        $"Property '{Config.Name}' must be of type DateTime to be used as a version field.");

    Config.Kind = PropertyKind.Version;
    return this;
  }
}