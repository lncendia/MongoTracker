using System.Collections.Concurrent;

using Incendia.MongoTracker.Builders;
using Incendia.MongoTracker.Enums;

namespace Incendia.MongoTracker.Metadata;

/// <summary>
/// Cached description of how a type is tracked under a configuration: which properties and in which mode.
/// </summary>
internal sealed class TrackingPlan
{
  #region Fields

  /// <summary>
  /// Plans built so far, keyed by the tracked type and its configuration.
  /// </summary>
  private static readonly ConcurrentDictionary<(Type, EntityBuilder?), TrackingPlan> _cache = new();

  /// <summary>
  /// Positions of the tracked properties in <see cref="Properties"/>, by name.
  /// </summary>
  private readonly Dictionary<string, int> _indexes;

  #endregion

  #region Properties

  /// <summary>
  /// The tracked type.
  /// </summary>
  public Type Type { get; }

  /// <summary>
  /// Tracked properties (identifiers and ignored properties excluded).
  /// </summary>
  public IReadOnlyList<PropertyAccessor> Properties { get; }

  /// <summary>
  /// Tracking mode of each property in <see cref="Properties"/>, <c>null</c> for plain properties.
  /// </summary>
  public IReadOnlyList<PropertyKind?> Kinds { get; }

  #endregion

  #region Methods

  /// <summary>
  /// Returns the cached plan of the type under the configuration, building it on first use.
  /// </summary>
  /// <param name="type">The tracked type.</param>
  /// <param name="config">The configuration of the type, if any.</param>
  /// <returns>The tracking plan.</returns>
  public static TrackingPlan Get(Type type, EntityBuilder? config) =>
    _cache.GetOrAdd((type, config), key => new TrackingPlan(key.Item1, key.Item2));

  /// <summary>
  /// Returns the position of a tracked property in <see cref="Properties"/>.
  /// </summary>
  /// <param name="name">The name of the property.</param>
  /// <returns>The position of the property, or -1 if it is not tracked.</returns>
  public int IndexOf(string name) => _indexes.TryGetValue(name, out int index) ? index : -1;

  #endregion

  #region Constructors

  /// <summary>
  /// Builds the plan of the type under the configuration.
  /// </summary>
  /// <param name="type">The tracked type.</param>
  /// <param name="config">The configuration of the type, if any.</param>
  private TrackingPlan(Type type, EntityBuilder? config)
  {
    Type = type;

    (PropertyAccessor Accessor, PropertyKind? Kind)[] properties = TypeMetadata.Get(type).Properties
      .Select(p => (Accessor: p, Kind: config?.Properties.GetValueOrDefault(p.Name)?.Kind))
      .Where(p => p.Kind is not (PropertyKind.Identifier or PropertyKind.Ignored))
      .ToArray();

    Properties = properties.Select(p => p.Accessor).ToArray();
    Kinds = properties.Select(p => p.Kind).ToArray();
    _indexes = Properties.Select((p, i) => (p.Name, i)).ToDictionary(p => p.Name, p => p.i);
  }

  #endregion
}
