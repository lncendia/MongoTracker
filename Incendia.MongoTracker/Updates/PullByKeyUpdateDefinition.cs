using MongoDB.Bson;
using MongoDB.Driver;

namespace Incendia.MongoTracker.Updates;

/// <summary>
/// Update definition that removes array elements by the value of their identifier.
/// </summary>
/// <param name="fieldPath">The path of the array, in member names (resolved into element names when rendered).</param>
/// <param name="keyElementName">The element name of the identifier inside array elements.</param>
/// <param name="keys">The serialized identifiers of the elements to remove.</param>
/// <typeparam name="T">The root document type.</typeparam>
internal sealed class PullByKeyUpdateDefinition<T>(string fieldPath, string keyElementName, BsonArray keys)
  : UpdateDefinition<T>
{
  /// <inheritdoc/>
  public override BsonValue Render(RenderArgs<T> args)
  {
    string fieldName = new StringFieldDefinition<T>(fieldPath).Render(args).FieldName;
    var condition = new BsonDocument(keyElementName, new BsonDocument("$in", keys));
    return new BsonDocument("$pull", new BsonDocument(fieldName, condition));
  }
}
