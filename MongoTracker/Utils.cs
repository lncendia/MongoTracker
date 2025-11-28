using System.Linq.Expressions;
using MongoTracker.Builders;

namespace MongoTracker;

/// <summary>
/// Utility methods for common operations
/// </summary>
internal static class Utils
{
  /// <summary>
  /// Combines parent property name and current property name to form a MongoDB path.
  /// </summary>
  /// <param name="parentPropertyName">Parent property name. Can be null if the property isn't nested.</param>
  /// <param name="propertyName">Current property name.</param>
  /// <returns>A string representing the property path in MongoDB.</returns>
  public static string? CombineName(string? parentPropertyName, string? propertyName)
  {
    // If parent property name is absent, return only the current property name
    if (parentPropertyName == null) return propertyName;

    // Return the combined path with properties separated by a dot
    return $"{parentPropertyName}.{propertyName}";
  }

  /// <summary>
  /// Creates a delegate to get the version value from an entity for optimistic concurrency control.
  /// </summary>
  /// <param name="config">The entity configuration collection.</param>
  /// <typeparam name="T">The type of the entity.</typeparam>
  /// <returns>A delegate that returns the version value, or null if no version property is configured.</returns>
  public static VersionAccessor<T>? GetVersionAccessor<T>(IReadOnlyCollection<EntityBuilder> config) where T : class
  {
    // Get the runtime type of the entity
    var type = typeof(T);

    // Find the entity configuration for this entity type
    var entityConfig = config.SingleOrDefault(e => e.EntityType == type);

    // Find the property marked as Version
    var versionProperty = entityConfig?.Properties.SingleOrDefault(p => p.Kind == PropertyKind.Version);
    if (versionProperty == null) return null;

    // Locate actual CLR property
    var property = type.GetProperty(versionProperty.Name);

    // If reflection fails, versioning is not possible
    if (property == null) return null;

    // Create VersionAccessor that exposes the getter delegate
    return new VersionAccessor<T>(versionProperty.Name, GetVersion);

    // Delegate that returns the current version value from the entity
    object GetVersion(T entity) => property.GetValue(entity);
  }

  /// <summary>
  /// Creates an expression that retrieves the identifier (Id) property of an entity.
  /// </summary>
  /// <param name="config">The entity configuration collection.</param>
  /// <typeparam name="T">Entity type.</typeparam>
  /// <returns>Expression like: tk => tk.Id.</returns>
  /// <exception cref="InvalidOperationException">Thrown when Id property is not configured or missing</exception>
  public static Expression<Func<T, object>> GetIdentifierExpression<T>(IReadOnlyCollection<EntityBuilder> config)
    where T : class
  {
    // Resolve CLR type of T
    var type = typeof(T);

    // Fetch configuration for this entity type
    var entityConfig = config.SingleOrDefault(e => e.EntityType == type);

    // Find identifier property metadata
    var identifierProperty = entityConfig?.Properties.SingleOrDefault(p => p.Kind == PropertyKind.Identifier);
    if (identifierProperty == null)
      throw new InvalidOperationException($"Entity '{type.Name}' has no Id property configured.");

    // Resolve actual CLR property via reflection
    var property = type.GetProperty(identifierProperty.Name);
    if (property == null)
      throw new InvalidOperationException($"Property '{identifierProperty.Name}' not found on entity.");

    // Create lambda parameter: tk =>
    var param = Expression.Parameter(type, "tk");

    // Access property: tk.Id
    var body = Expression.Property(param, property);

    // For value types wrap in Convert(..., object)
    if (property.PropertyType.IsValueType)
      return Expression.Lambda<Func<T, object>>(Expression.Convert(body, typeof(object)), param);

    // For reference types no conversion needed
    return Expression.Lambda<Func<T, object>>(body, param);
  }

  /// <summary>
  /// Provides access to version property information and value retrieval
  /// </summary>
  /// <typeparam name="T">The type of the entity</typeparam>
  public class VersionAccessor<T>(string propertyName, Func<T, object> getValue) where T : class
  {
    /// <summary>
    /// Gets the name of the version property
    /// </summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>
    /// Gets the function to retrieve the version value from an entity
    /// </summary>
    public Func<T, object> GetValue { get; } = getValue;
  }
}