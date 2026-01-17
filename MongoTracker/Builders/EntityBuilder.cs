using System.Linq.Expressions;
using System.Reflection;

namespace MongoTracker.Builders;

/// <summary>
/// Base class for entity configuration builders
/// </summary>
public abstract class EntityBuilder
{
  /// <summary>
  /// Gets the list of configured properties
  /// </summary>
  internal abstract IReadOnlyDictionary<string, PropertyConfig> Properties { get; }

  /// <summary>
  /// Gets the names of properties used as concurrency tokens
  /// </summary>
  internal IReadOnlyCollection<string> ConcurrencyTokenNames =>
    field ??= Properties.Values
      .Where(p => p.Kind == PropertyKind.ConcurrencyToken)
      .Select(p => p.Name)
      .ToHashSet();

  /// <summary>
  /// Gets the name of the version property, if configured
  /// </summary>
  internal string? VersionPropertyName =>
    field ??= Properties.Values
      .FirstOrDefault(p => p.Kind == PropertyKind.Version)
      ?.Name;

  /// <summary>
  /// Gets the name of the identifier property, if configured
  /// </summary>
  internal string? IdentifierPropertyName =>
    field ??= Properties.Values
      .FirstOrDefault(p => p.Kind == PropertyKind.Identifier)
      ?.Name;
}

/// <summary>
/// Builder for configuring entity properties
/// </summary>
/// <typeparam name="TEntity">The type of entity to configure</typeparam>
public class EntityBuilder<TEntity> : EntityBuilder
{
  /// <summary>
  /// Stores the configuration for each property
  /// </summary>
  private readonly Dictionary<string, PropertyConfig> _properties = new();

  /// <summary>
  /// Returns the read-only list of configured properties
  /// </summary>
  internal override IReadOnlyDictionary<string, PropertyConfig> Properties => _properties;

  /// <summary>
  /// Configures a specific property of the entity
  /// </summary>
  /// <param name="property">Expression that selects the property to configure</param>
  /// <typeparam name="TProp">The type of the property being configured</typeparam>
  /// <returns>Property builder for fluent configuration</returns>
  /// <exception cref="InvalidOperationException">Thrown when expression is not a valid property access</exception>
  public PropertyBuilder Property<TProp>(Expression<Func<TEntity, TProp>> property)
  {
    // Validate that the expression is a property access
    if (property.Body is not MemberExpression member)
      throw new InvalidOperationException("Expression must be a property access");

    // Validate that this is a PROPERTY, not a field / method
    if (member.Member is not PropertyInfo propInfo)
      throw new InvalidOperationException($"Member '{member.Member.Name}' is not a property");

    // Validate that the property is readable and writable
    if (!propInfo.CanRead)
      throw new InvalidOperationException($"Property '{propInfo.Name}' must be readable");

    if (!propInfo.CanWrite)
      throw new InvalidOperationException($"Property '{propInfo.Name}' must be writable");

    string name = member.Member.Name;

    // Reuse existing property configuration if available
    if (_properties.TryGetValue(name, out PropertyConfig? existingConfig))
      return new PropertyBuilder(existingConfig);

    // Create configuration for the property
    var config = new PropertyConfig(name, propInfo.PropertyType);

    // Add property configuration to the dictionary
    _properties[name] = config;

    // Return property builder for further configuration
    return new PropertyBuilder(config);
  }
}
