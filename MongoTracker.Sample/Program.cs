using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoTracker.Builders;
using MongoTracker.Sample.Contexts;
using MongoTracker.Sample.Entities.Authors;
using MongoTracker.Sample.Entities.Books;
using MongoTracker.Tracker;

// Register Guid serializer to use standard representation (GuidRepresentation.Standard)
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Create MongoDB client for database connection
using var mongoClient = new MongoClient("mongodb://root:example@localhost:27017/admin");

// Initialize database context with MongoDB client and database name
var context = new MongoDbContext(mongoClient, "BookApp");

// Create database collections if they don't exist
await context.EnsureCreatedAsync();

// Build the model configuration during application startup
ModelBuilder config = Configure();

// Initialize database with sample data (authors, books etc.)
await InitializeDatabaseAsync(context, config);

// Create tracker for monitoring changes in books collection
var tracker = new MongoTracker<Book>(config);

// Find author "Jim Dale" ID in Authors collection
Guid jimDaleId = await context.Authors
  .AsQueryable()
  .Where(a => a.Name == "Jim Dale")
  .Select(a => a.Id)
  .FirstAsync();

// Find "A Game of Thrones" book in Books collection
Book? gameOfThrones = await context.Books
  .AsQueryable()
  .FirstAsync(b => b.Title == "A Game of Thrones");

// Find "The Shining" book in Books collection
Book? shining = await context.Books
  .AsQueryable()
  .FirstAsync(b => b.Title == "The Shining");

// Find "Harry Potter and the Philosopher's Stone" book in Books collection
Book? harryPotter = await context.Books
  .AsQueryable()
  .FirstAsync(b => b.Title == "Harry Potter and the Philosopher's Stone");

// Start tracking changes for this book
harryPotter = tracker.Track(harryPotter);

// Start tracking changes for this book
shining = tracker.Track(shining);

// Start tracking changes for this book
gameOfThrones = tracker.Track(gameOfThrones);

// Add new chapter to "A Game of Thrones"
gameOfThrones.Chapters.Add(new BookChapter
{
  Name = "Bran",
  StartPage = 31,
  EndPage = 50
});

// Update audiobook duration for "A Game of Thrones"
gameOfThrones.Audiobook!.Duration = TimeSpan.FromHours(30).TotalMinutes;

// Create new audiobook for "The Shining" and link it to author "Jim Dale"
shining.Audiobook = new Audiobook
{
  Author = jimDaleId,
  Duration = TimeSpan.FromHours(14).TotalMinutes
};

// Mark "Harry Potter and the Philosopher's Stone" for deletion
tracker.Delete(harryPotter);

// Prepare update operations for MongoDB
// The tracker will generate these atomic updates:
// 1. For "A Game of Thrones":
//    - Update Audiobook.Duration to 1800 minutes
//    - Add new chapter to Chapters array
//    - Update LastModified field to current date
//    Example query:
//    db.Books.updateOne(
//        { _id: UUID("..."), LastModified: ISODate("...") },
//        {
//            $set: { "Audiobook.Duration": 1800, LastModified: ISODate("...") },
//            $push: { Chapters: { Name: "Bran", StartPage: 31, EndPage: 50 } }
//        }
//    )
//
// 2. For "The Shining":
//    - Set new Audiobook object with author and duration
//    - Update LastModified field to current date
//    Example query:
//    db.Books.updateOne(
//        { _id: UUID("..."), LastModified: ISODate("...") },
//        {
//            $set: { Audiobook: { Author: ObjectId("..."), Duration: 840 }, LastModified: ISODate("...") }
//        }
//    )
//
// 3. For "Harry Potter and the Philosopher's Stone":
//    - Delete document from Books collection
//    Example query:
//    db.Books.deleteOne({ _id: UUID("..."), LastModified: ISODate("...") })
IReadOnlyCollection<WriteModel<Book>> update = tracker.Commit();

// Apply all changes (inserts, updates, deletes) to database
BulkWriteResult<Book>? result = await context.Books.BulkWriteAsync(update);

Console.WriteLine($"Modified: {result.ModifiedCount}, Deleted: {result.DeletedCount}");

// Exit program
return;

// Async method for database initialization
async Task InitializeDatabaseAsync(MongoDbContext mongoDbContext, ModelBuilder configuration)
{
  // Create tracker for monitoring changes in authors collection
  var mongoAuthorTracker = new MongoTracker<Author>(configuration);

  // Create authors list
  var authors = new List<Author>
  {
    new() { Id = Guid.NewGuid(), Name = "J.K. Rowling" },
    new() { Id = Guid.NewGuid(), Name = "George R.R. Martin" },
    new() { Id = Guid.NewGuid(), Name = "Stephen King" },
    new() { Id = Guid.NewGuid(), Name = "Jim Dale" },
    new() { Id = Guid.NewGuid(), Name = "Roy Dotrice" },
  };

  // Add each author to tracker for change monitoring
  foreach (Author author in authors) mongoAuthorTracker.Add(author);

  // Bulk insert authors into database Authors collection
  await mongoDbContext.Authors.BulkWriteAsync(mongoAuthorTracker.Commit());

  // Create tracker for monitoring changes in books collection
  var mongoBookTracker = new MongoTracker<Book>(configuration);

  // Create books list
  var books = new List<Book>
  {
    new()
    {
      Id = Guid.NewGuid(),
      Title = "Harry Potter and the Philosopher's Stone",
      Authors = [authors[0].Id],
      Audiobook = new Audiobook
      {
        Author = authors[3].Id,
        Duration = 480
      },
      Chapters =
      [
        new BookChapter { Name = "The Boy Who Lived", StartPage = 1, EndPage = 15 },
        new BookChapter { Name = "The Vanishing Glass", StartPage = 16, EndPage = 30 }
      ]
    },
    new()
    {
      Id = Guid.NewGuid(),
      Title = "A Game of Thrones",
      Authors = [authors[1].Id],
      Audiobook = new Audiobook
      {
        Author = authors[4].Id,
        Duration = 480
      },
      Chapters =
      [
        new BookChapter { Name = "Prologue", StartPage = 1, EndPage = 10 }
      ]
    },
    new()
    {
      Id = Guid.NewGuid(),
      Title = "The Shining",
      Authors = [authors[2].Id],
      Chapters =
      [
        new BookChapter { Name = "Prologue", StartPage = 1, EndPage = 5 },
        new BookChapter { Name = "The Interview", StartPage = 6, EndPage = 20 }
      ]
    }
  };

  // Add each book to tracker for change monitoring
  foreach (Book book in books) mongoBookTracker.Add(book);

  // Bulk insert books into database Books collection
  await mongoDbContext.Books.BulkWriteAsync(mongoBookTracker.Commit());
}

// Factory method that creates and configures the ModelBuilder with all entity mappings
ModelBuilder Configure()
{
  // Create a new model builder instance that will hold all entity configurations
  var builder = new ModelBuilder();

  // Configure the Author entity
  builder.Entity<Author>(e => { e.Property(a => a.Id).IsIdentifier(); });

  // Configure the Book entity, including nested tracked objects and versioning
  builder.Entity<Book>(e =>
  {
    e.Property(b => b.Id).IsIdentifier();
    e.Property(b => b.Audiobook).IsTrackedObject();
    e.Property(b => b.Authors).IsCollection();
    e.Property(b => b.Chapters).IsTrackedObjectCollection();
    e.Property(b => b.LastUpdate).IsVersion();
  });

  // Configure the BookChapter entity
  builder.Entity<BookChapter>(e => { e.Property(c => c.Footnotes).IsCollection(); });

  // Return the configured builder
  return builder;
}
