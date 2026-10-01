using MongoDB.Bson;
using MongoDB.Driver;

namespace Incendia.MongoTracker.Updates;

/// <summary>
/// Update definition that addresses an array element through a filtered positional operator ($[identifier]).
/// </summary>
/// <remarks>
/// The driver resolves member names into element names (and picks member serializers) along a field path,
/// but stops at $[identifier] segments. The inner update is therefore built with the positional operator "$",
/// which the driver resolves like an array index, and that segment is replaced with $[identifier] after rendering.
/// </remarks>
/// <param name="inner">The update of the element, built with "$" in place of the filtered positional operator.</param>
/// <param name="position">Which positional segment of the field paths to replace (1-based, counted from the root).</param>
/// <param name="identifier">The array filter identifier.</param>
/// <typeparam name="T">The root document type.</typeparam>
internal sealed class FilteredPositionalUpdateDefinition<T>(UpdateDefinition<T> inner, int position, string identifier)
  : UpdateDefinition<T>
{
  /// <summary>
  /// The positional operator used as a placeholder in field paths.
  /// </summary>
  public const string Placeholder = "$";

  /// <summary>
  /// Counts positional segments ("$" placeholders and filtered positional operators) in a field path.
  /// </summary>
  /// <param name="path">The field path.</param>
  /// <returns>The number of positional segments.</returns>
  public static int CountPositionalSegments(string? path) =>
    path?.Split('.').Count(IsPositional) ?? 0;

  /// <inheritdoc/>
  public override BsonValue Render(RenderArgs<T> args)
  {
    BsonDocument rendered = inner.Render(args).AsBsonDocument;
    var result = new BsonDocument();

    // Rename the fields of every update operator ($set, $push, $pull, ...)
    foreach (BsonElement op in rendered)
    {
      if (op.Value is not BsonDocument fields)
      {
        result.Add(op);
        continue;
      }

      var renamed = new BsonDocument();
      foreach (BsonElement field in fields)
        renamed.Add(ReplaceSegment(field.Name), field.Value);

      result.Add(op.Name, renamed);
    }

    return result;
  }

  /// <summary>
  /// Replaces the positional segment at the configured position with the filtered positional operator.
  /// </summary>
  /// <param name="path">The rendered field path.</param>
  /// <returns>The field path addressing the element through the array filter.</returns>
  private string ReplaceSegment(string path)
  {
    string[] segments = path.Split('.');
    var count = 0;

    for (var i = 0; i < segments.Length; i++)
    {
      if (!IsPositional(segments[i]) || ++count != position) continue;
      segments[i] = $"$[{identifier}]";
      break;
    }

    return string.Join(".", segments);
  }

  /// <summary>
  /// Checks whether a path segment is a positional operator.
  /// </summary>
  private static bool IsPositional(string segment) => segment == Placeholder || segment.StartsWith("$[");
}
