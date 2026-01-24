using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

using Incendia.MongoTracker.Builders;

using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Incendia.MongoTracker;

/// <summary>
/// Utility methods for common operations
/// </summary>
internal static class Utils
{
  /// <summary>
  /// Caches MongoDB ignore metadata for properties to avoid repeated
  /// reflection and class map lookups.
  /// </summary>
  private static readonly ConcurrentDictionary<string, bool> _bsonIgnoreCache = new();

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

  /// <param name="property">
  /// The <see cref="PropertyInfo"/> to inspect.
  /// </param>
  extension(PropertyInfo property)
  {
    /// <summary>
    /// Determines whether the specified property is ignored by MongoDB serialization
    /// by any supported mechanism (attributes or class map).
    /// </summary>
    /// <returns>
    /// <c>true</c> if the property is ignored by MongoDB;
    /// otherwise, <c>false</c>.
    /// </returns>
    public bool IsBsonIgnored()
    {
      return _bsonIgnoreCache.GetOrAdd(CalculateCacheKey(property), _ =>
      {
        // 1. ClassMap has absolute priority
        if (BsonClassMap.IsClassMapRegistered(property.DeclaringType))
        {
          var classMap = BsonClassMap.LookupClassMap(property.DeclaringType);
          return classMap.AllMemberMaps.All(m => property.IsSameProperty(m.MemberInfo));
        }

        // 2. Fallback to attribute-based ignore
        return property.IsDefined(typeof(BsonIgnoreAttribute), inherit: true);
      });
    }

    /// <summary>
    /// Checks whether the specified member represents the same property.
    /// </summary>
    /// <param name="b">Member to compare.</param>
    /// <returns>
    /// <c>true</c> if both members have the same declaring type and name; otherwise <c>false</c>.
    /// </returns>
    private bool IsSameProperty(MemberInfo b)
    {
      return property.DeclaringType == b.DeclaringType && property.Name == b.Name;
    }
  }

  /// <summary>
  /// Calculates a cache key for the specified member.
  /// </summary>
  /// <param name="p">Member to generate a cache key for.</param>
  /// <returns>A stable cache key string for the given member.</returns>
  public static string CalculateCacheKey(MemberInfo p)
  {
    return $"{p.DeclaringType!.FullName}::{p.Name}";
  }
}
