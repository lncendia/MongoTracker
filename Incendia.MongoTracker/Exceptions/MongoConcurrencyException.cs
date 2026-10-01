namespace Incendia.MongoTracker.Exceptions;

/// <summary>
/// Thrown when a save affects fewer documents than expected, which means that some of them
/// were modified or deleted by someone else since they were loaded (optimistic concurrency conflict).
/// </summary>
/// <remarks>
/// Tracked changes are not accepted when this exception is thrown. Operations that did succeed are already
/// persisted unless the save was executed inside a transaction, so use a session with a transaction
/// when the whole unit of work must be atomic.
/// </remarks>
public class MongoConcurrencyException : Exception
{
  /// <summary>
  /// Number of update operations that were expected to match a document.
  /// </summary>
  public long ExpectedMatchedCount { get; }

  /// <summary>
  /// Number of documents actually matched by update operations.
  /// </summary>
  public long MatchedCount { get; }

  /// <summary>
  /// Number of delete operations that were expected to remove a document.
  /// </summary>
  public long ExpectedDeletedCount { get; }

  /// <summary>
  /// Number of documents actually deleted.
  /// </summary>
  public long DeletedCount { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="MongoConcurrencyException"/> class.
  /// </summary>
  /// <param name="expectedMatchedCount">Number of update operations that were expected to match a document.</param>
  /// <param name="matchedCount">Number of documents actually matched by update operations.</param>
  /// <param name="expectedDeletedCount">Number of delete operations that were expected to remove a document.</param>
  /// <param name="deletedCount">Number of documents actually deleted.</param>
  public MongoConcurrencyException(long expectedMatchedCount, long matchedCount, long expectedDeletedCount,
    long deletedCount)
    : base($"Optimistic concurrency conflict: expected to update {expectedMatchedCount} and delete " +
           $"{expectedDeletedCount} document(s), but matched {matchedCount} and deleted {deletedCount}. " +
           "The data may have been modified or deleted since it was loaded.")
  {
    ExpectedMatchedCount = expectedMatchedCount;
    MatchedCount = matchedCount;
    ExpectedDeletedCount = expectedDeletedCount;
    DeletedCount = deletedCount;
  }
}
