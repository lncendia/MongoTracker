using System.Collections.Concurrent;
using System.Reflection;

namespace Incendia.MongoTracker.Metadata;

/// <summary>
/// Cached reflection data of a type: the properties the tracker reads and writes.
/// </summary>
internal sealed class TypeMetadata
{
  #region Fields

  /// <summary>
  /// Metadata of every type seen so far.
  /// </summary>
  private static readonly ConcurrentDictionary<Type, TypeMetadata> _cache = new();

  /// <summary>
  /// Tracked properties by name.
  /// </summary>
  private readonly Dictionary<string, PropertyAccessor> _propertiesByName;

  #endregion

  #region Properties

  /// <summary>
  /// Readable and writable instance properties that are not ignored by BSON serialization.
  /// </summary>
  public IReadOnlyList<PropertyAccessor> Properties { get; }

  #endregion

  #region Methods

  /// <summary>
  /// Returns the cached metadata of the type, building it on first use.
  /// </summary>
  /// <param name="type">The type to describe.</param>
  /// <returns>The metadata of the type.</returns>
  public static TypeMetadata Get(Type type) => _cache.GetOrAdd(type, t => new TypeMetadata(t));

  /// <summary>
  /// Finds a tracked property by name.
  /// </summary>
  /// <param name="name">The name of the property.</param>
  /// <returns>The accessor of the property, or <c>null</c> if the type has no such tracked property.</returns>
  public PropertyAccessor? Find(string name) => _propertiesByName.GetValueOrDefault(name);

  /// <summary>
  /// Returns the depth of the type in its inheritance hierarchy.
  /// </summary>
  private static int GetDepth(Type? type)
  {
    var depth = 0;
    for (; type != null; type = type.BaseType) depth++;
    return depth;
  }

  #endregion

  #region Constructors

  /// <summary>
  /// Builds the metadata of the type.
  /// </summary>
  /// <param name="type">The type to describe.</param>
  private TypeMetadata(Type type)
  {
    Properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
      .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && !p.IsBsonIgnored())

      // A property hidden with 'new' is returned once per declaring type, keep the most derived one
      .GroupBy(p => p.Name)
      .Select(g => g.OrderByDescending(p => GetDepth(p.DeclaringType)).First())
      .Select(p => new PropertyAccessor(p))
      .ToArray();

    _propertiesByName = Properties.ToDictionary(p => p.Name);
  }

  #endregion
}
