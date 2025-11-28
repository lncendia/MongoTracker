namespace MongoTracker.Builders;

/// <summary>
/// Configuration settings for a property
/// </summary>
/// <param name="propName">The name of the property</param>
/// <param name="propType">The type of the property</param>
public class PropertyConfig(string propName, Type propType)
{
  /// <summary>
  /// Gets the name of the property
  /// </summary>
  public string Name { get; } = propName;

  /// <summary>
  /// Gets the type of the property
  /// </summary>
  public Type Type { get; } = propType;

  /// <summary>
  /// Gets or sets the kind/type of the property
  /// </summary>
  public PropertyKind Kind { get; set; }
}