using MongoDB.Bson;
using MongoDB.Driver;

namespace Incendia.MongoTracker.Entities.Base;

/// <summary>
/// Collects state shared by all nodes while a single update definition is being built.
/// </summary>
internal class UpdateContext
{
  #region Fields

  /// <summary>
  /// Array filters referenced by filtered positional operators ($[identifier]) of the update.
  /// </summary>
  private readonly List<ArrayFilterDefinition> _arrayFilters = [];

  #endregion

  #region Properties

  /// <summary>
  /// Array filters that must be sent together with the update.
  /// </summary>
  public IReadOnlyList<ArrayFilterDefinition> ArrayFilters => _arrayFilters;

  #endregion

  #region Methods

  /// <summary>
  /// Registers an array filter that matches array elements by the value of one of their fields.
  /// </summary>
  /// <param name="elementName">The BSON element name of the field inside an array element.</param>
  /// <param name="value">The serialized value the field must be equal to.</param>
  /// <returns>An identifier, unique within the update, to be used as $[identifier] in field paths.</returns>
  public string AddArrayFilter(string elementName, BsonValue value)
  {
    string identifier = $"i{_arrayFilters.Count}";
    var filter = new BsonDocument($"{identifier}.{elementName}", value);
    _arrayFilters.Add(new BsonDocumentArrayFilterDefinition<BsonDocument>(filter));
    return identifier;
  }

  #endregion
}
