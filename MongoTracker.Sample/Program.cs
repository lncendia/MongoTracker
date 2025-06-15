using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoTracker.Sample.Contexts;
using MongoTracker.Sample.Entities.Authors;
using MongoTracker.Sample.Entities.Books;
using MongoTracker.Tracker;


// Register Guid serializer to use standard representation (GuidRepresentation.Standard)
// This ensures proper GUID handling in MongoDB as the default uses a legacy format
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Create MongoDB client for database connection
// Using connection string with login, password, host and port
using var mongoClient = new MongoClient("mongodb://root:example@mongotracker.mongo:27017/admin");

// Initialize database context with MongoDB client and database name
var context = new MongoDbContext(mongoClient, "BookApp");

// Create database collections if they don't exist
await context.EnsureCreatedAsync();

// Initialize database with sample data (authors, books etc.)
await InitializeDatabaseAsync(context);

// Create tracker for monitoring changes in books collection
// Using Guid as identifier and Book as entity type
var tracker = new MongoTracker<Guid, Book>(tk => tk.Id);

// Find author "Jim Dale" ID in Authors collection
var jimDaleId = await context.Authors
    .AsQueryable()
    .Where(a => a.Name == "Jim Dale")
    .Select(a => a.Id)
    .FirstAsync();

// Find "A Game of Thrones" book in Books collection
var gameOfThrones = await context.Books
    .AsQueryable()
    .FirstAsync(b => b.Title == "A Game of Thrones");

// Find "The Shining" book in Books collection
var shining = await context.Books
    .AsQueryable()
    .FirstAsync(b => b.Title == "The Shining");

// Find "Harry Potter and the Philosopher's Stone" book in Books collection
var harryPotter = await context.Books
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

// Remove "Harry Potter and the Philosopher's Stone" from tracker
tracker.Remove(harryPotter.Id);

// Prepare update operations for MongoDB
// The tracker will generate these atomic updates:
// 1. For "A Game of Thrones":
//    - Update Audiobook.Duration to 1800 minutes
//    - Add new chapter to Chapters array
//    - Update LastModified field to current date
//    Example query:
//    db.Books.updateOne(
//        { _id: ObjectId("...") },
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
//        { _id: ObjectId("...") },
//        {
//            $set: { Audiobook: { Author: ObjectId("..."), Duration: 840 }, LastModified: ISODate("...") }
//        }
//    )
//
// 3. For "Harry Potter and the Philosopher's Stone":
//    - Delete document from Books collection
//    Example query:
//    db.Books.deleteOne({ _id: ObjectId("...") })
var update = tracker.Commit();

// Apply all changes (inserts, updates, deletes) to database
await context.Books.BulkWriteAsync(update);

// Exit program
return;


// Async method for database initialization
// Takes MongoDB database context (MongoDbContext) as parameter
async Task InitializeDatabaseAsync(MongoDbContext mongoDbContext)
{
    // Create tracker for monitoring changes in authors collection
    // Using Guid as identifier and Author as entity type
    var mongoAuthorTracker = new MongoTracker<Guid, Author>(tk => tk.Id);

    // Create authors list
    var authors = new List<Author>
    {
        // Initialize J.K. Rowling author with unique ID
        new() { Id = Guid.NewGuid(), Name = "J.K. Rowling" },

        // Initialize George R.R. Martin author with unique ID
        new() { Id = Guid.NewGuid(), Name = "George R.R. Martin" },

        // Initialize Stephen King author with unique ID
        new() { Id = Guid.NewGuid(), Name = "Stephen King" },

        // Initialize narrator Jim Dale with unique ID
        // Famous narrator for Harry Potter books
        new() { Id = Guid.NewGuid(), Name = "Jim Dale" },
        
        // Initialize narrator Roy Dotrice with unique ID
        // Narrator for "A Song of Ice and Fire" books
        new() { Id = Guid.NewGuid(), Name = "Roy Dotrice" },
    };

    // Add each author to tracker for change monitoring
    foreach (var author in authors) mongoAuthorTracker.Add(author);

    // Bulk insert authors into database Authors collection
    await mongoDbContext.Authors.BulkWriteAsync(mongoAuthorTracker.Commit());

    // Create tracker for monitoring changes in books collection
    // Using Guid as identifier and Book as entity type
    var mongoBookTracker = new MongoTracker<Guid, Book>(tk => tk.Id);

    // Create books list
    var books = new List<Book>
    {
        // Initialize "Harry Potter and the Philosopher's Stone" book
        new()
        {
            // Unique book ID
            Id = Guid.NewGuid(),

            // Book title
            Title = "Harry Potter and the Philosopher's Stone",

            // List of author IDs (J.K. Rowling in this case)
            Authors = [authors[0].Id],

            // Initialize audiobook
            Audiobook = new Audiobook
            {
                // Narrator ID (Jim Dale)
                Author = authors[3].Id,

                // Audiobook duration in minutes (8 hours)
                Duration = 480
            },

            // Book chapters list
            Chapters =
            [
                // Chapter "The Boy Who Lived" with start/end pages
                new BookChapter { Name = "The Boy Who Lived", StartPage = 1, EndPage = 15 },

                // Chapter "The Vanishing Glass" with start/end pages
                new BookChapter { Name = "The Vanishing Glass", StartPage = 16, EndPage = 30 }
            ]
        },

        // Initialize "A Game of Thrones" book
        new()
        {
            // Unique book ID
            Id = Guid.NewGuid(),

            // Book title
            Title = "A Game of Thrones",

            // List of author IDs (George R.R. Martin in this case)
            Authors = [authors[1].Id],
            
            // Initialize audiobook
            Audiobook = new Audiobook
            {
                // Narrator ID (Roy Dotrice)
                Author = authors[4].Id,

                // Audiobook duration in minutes (8 hours)
                Duration = 480
            },

            // Book chapters list
            Chapters =
            [
                // Chapter "Prologue" with start/end pages
                new BookChapter { Name = "Prologue", StartPage = 1, EndPage = 10 }
            ]
        },

        // Initialize "The Shining" book
        new()
        {
            // Unique book ID
            Id = Guid.NewGuid(),

            // Book title
            Title = "The Shining",

            // List of author IDs (Stephen King in this case)
            Authors = [authors[2].Id],

            // Book chapters list
            Chapters =
            [
                // Chapter "Prologue" with start/end pages
                new BookChapter { Name = "Prologue", StartPage = 1, EndPage = 5 },

                // Chapter "The Interview" with start/end pages
                new BookChapter { Name = "The Interview", StartPage = 6, EndPage = 20 }
            ]
        }
    };

    // Add each book to tracker for change monitoring
    foreach (var book in books) mongoBookTracker.Add(book);

    // Bulk insert books into database Books collection
    await mongoDbContext.Books.BulkWriteAsync(mongoBookTracker.Commit());
}