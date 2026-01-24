using MongoDB.Driver;
using Incendia.MongoTracker.Sample.Entities.Authors;
using Incendia.MongoTracker.Sample.Entities.Books;

namespace Incendia.MongoTracker.Sample.Contexts;

/// <summary>
/// Provides a simplified interface for working with MongoDB collections.
/// </summary>
public class MongoDbContext
{
  /// <summary>
  /// MongoDB client used for interacting with the MongoDB server.
  /// </summary>
  public IMongoClient Client { get; }

  /// <summary>
  /// Provides access to the Authors collection.
  /// Contains data about authors.
  /// </summary>
  public IMongoCollection<Author> Authors { get; }

  /// <summary>
  /// Provides access to the Books collection.
  /// Contains data about books.
  /// </summary>
  public IMongoCollection<Book> Books { get; }

  /// <summary>
  /// The MongoDB database this context works with.
  /// </summary>
  private readonly IMongoDatabase _database;

  /// <summary>
  /// Initializes a new instance of the MongoDB context.
  /// </summary>
  /// <param name="mongoClient">MongoDB client used to connect to the server.</param>
  /// <param name="databaseName">Name of the database to connect to.</param>
  public MongoDbContext(IMongoClient mongoClient, string databaseName)
  {
    // Initialize the Client property with the provided MongoDB client
    Client = mongoClient;

    // Get the database with the specified name
    _database = mongoClient.GetDatabase(databaseName);

    // Initialize the Authors property by getting the "Authors" collection
    Authors = _database.GetCollection<Author>("Authors");

    // Initialize the Books property by getting the "Books" collection
    Books = _database.GetCollection<Book>("Books");
  }

  /// <summary>
  /// Asynchronously creates collections in the database if they don't exist.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the async operation.</param>
  public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
  {
    // Create the "Authors" collection if it doesn't exist
    await _database.CreateCollectionAsync("Authors", cancellationToken: cancellationToken);

    // Create the "Books" collection if it doesn't exist
    await _database.CreateCollectionAsync("Books", cancellationToken: cancellationToken);

    // Clears all documents from collections.
    await ClearCollectionsAsync(cancellationToken);
  }

  /// <summary>
  /// Asynchronously clears all documents from the Authors and Books collections.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the async operation.</param>
  private async Task ClearCollectionsAsync(CancellationToken cancellationToken = default)
  {
    // Clear all documents from the "Authors" collection
    await _database.GetCollection<Author>("Authors").DeleteManyAsync(FilterDefinition<Author>.Empty, cancellationToken);

    // Clear all documents from the "Books" collection
    await _database.GetCollection<Book>("Books").DeleteManyAsync(FilterDefinition<Book>.Empty, cancellationToken);
  }
}
