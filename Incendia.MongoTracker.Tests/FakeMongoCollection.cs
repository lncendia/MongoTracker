using System.Reflection;

using MongoDB.Driver;

namespace Incendia.MongoTracker.Tests;

/// <summary>
/// Minimal in-memory stand-in for <see cref="IMongoCollection{TDocument}"/> that only supports bulk writes.
/// Records every request batch and answers with a configurable result.
/// </summary>
public class FakeMongoCollection<T> : DispatchProxy
{
  /// <summary>
  /// All request batches passed to BulkWrite / BulkWriteAsync.
  /// </summary>
  public List<IReadOnlyList<WriteModel<T>>> Batches { get; } = [];

  /// <summary>
  /// Produces the result for a batch. Defaults to every operation succeeding.
  /// </summary>
  public Func<IReadOnlyList<WriteModel<T>>, BulkWriteResult<T>> Handler { get; set; } = Succeed;

  /// <summary>
  /// The fake exposed as a collection.
  /// </summary>
  public IMongoCollection<T> Collection => (IMongoCollection<T>)(object)this;

  public static FakeMongoCollection<T> Create() =>
    (FakeMongoCollection<T>)(object)Create<IMongoCollection<T>, FakeMongoCollection<T>>();

  /// <summary>
  /// Result where every update matches, every delete removes and every insert succeeds.
  /// </summary>
  public static BulkWriteResult<T> Succeed(IReadOnlyList<WriteModel<T>> requests) =>
    Result(requests, matched: requests.OfType<UpdateOneModel<T>>().Count(),
      deleted: requests.OfType<DeleteOneModel<T>>().Count());

  /// <summary>
  /// Acknowledged result with the given counts.
  /// </summary>
  public static BulkWriteResult<T> Result(IReadOnlyList<WriteModel<T>> requests, long matched, long deleted) =>
    new BulkWriteResult<T>.Acknowledged(requests.Count, matched, deleted,
      requests.OfType<InsertOneModel<T>>().Count(), matched, requests, []);

  protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
  {
    if (targetMethod?.Name is not ("BulkWrite" or "BulkWriteAsync"))
      throw new NotSupportedException($"{targetMethod?.Name} is not supported by the fake collection.");

    IReadOnlyList<WriteModel<T>> requests = args!.OfType<IEnumerable<WriteModel<T>>>().Single().ToList();
    Batches.Add(requests);
    BulkWriteResult<T> result = Handler(requests);

    return targetMethod.Name == "BulkWriteAsync" ? Task.FromResult(result) : result;
  }
}
