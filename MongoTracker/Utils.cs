using System.Linq.Expressions;
using System.Reflection;

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
  /// Creates an expression that retrieves the identifier (Id) property of an entity.
  /// </summary>
  /// <param name="config">The entity configuration collection.</param>
  /// <typeparam name="T">Entity type.</typeparam>
  /// <returns>Expression like: tk => tk.Id.</returns>
  /// <exception cref="InvalidOperationException">Thrown when Id property is not configured or missing</exception>
  public static Expression<Func<T, object>> GetIdentifierExpression<T>(IReadOnlyDictionary<Type, EntityBuilder> config)
    where T : class
  {
    // Resolve CLR type of T
    Type type = typeof(T);

    // Fetch configuration for this entity type
    EntityBuilder? entityConfig = config.GetValueOrDefault(type);

    // Find identifier property metadata
    string? identifierProperty = entityConfig?.IdentifierPropertyName;
    if (identifierProperty == null)
      throw new InvalidOperationException($"Entity '{type.Name}' has no Id property configured.");

    // Resolve actual CLR property via reflection
    PropertyInfo? property = type.GetProperty(identifierProperty);
    if (property == null)
      throw new InvalidOperationException($"Property '{identifierProperty}' not found on entity.");

    // Create lambda parameter: tk =>
    ParameterExpression param = Expression.Parameter(type, "tk");

    // Access property: tk.Id
    MemberExpression body = Expression.Property(param, property);

    // For value types wrap in Convert(..., object)
    if (property.PropertyType.IsValueType)
      return Expression.Lambda<Func<T, object>>(Expression.Convert(body, typeof(object)), param);

    // For reference types no conversion needed
    return Expression.Lambda<Func<T, object>>(body, param);
  }
}
