# MongoTracker

[![NuGet](https://img.shields.io/nuget/v/MongoTracker.Core.svg)](https://www.nuget.org/packages/MongoTracker.Core)

**MongoTracker** is a lightweight ORM (Object-Relational Mapping) library for MongoDB that automatically tracks changes in entities and performs bulk database operations. It simplifies working with MongoDB by providing a convenient interface for managing entities and their changes.

## Key Features

1. **Change Tracking**:
   - Automatic tracking of entity changes (insert, update, delete).
   - Support for bulk write operations (BulkWrite).
   - Granular updates for complex documents, including nested objects and collections.

2. **User-Friendly Interface**:
   - Easy entity registration in the tracker.
   - Seamless integration with existing projects.

3. **Flexibility**:
   - Supports any entity type.
   - Configurable entity identifiers.

4. **Performance**:
   - Minimizes database queries through bulk operations.
   - Updates only modified fields, reducing database load.

## Installation

To use MongoTracker in your project, add it as a NuGet dependency:

```bash  
dotnet add package MongoTracker.Core
```  

## Usage

### 1. Initializing the Tracker

To start using MongoTracker, create an instance of the tracker by specifying the entity type and its identifier.

```csharp  
var mongoTracker = new MongoTracker<Guid, Book>(tk => tk.Id);  
```  

### 2. Adding Entities to the Tracker

Entities can be added individually or as a list. The tracker automatically monitors changes.

```csharp  
var book = new Book { Id = Guid.NewGuid(), Title = "Sample Book" };  
mongoTracker.Add(book);  

var anotherBook = await mongoDbContext.Books  
    .AsQueryable()  
    .FirstOrDefaultAsync(b => b.Id == idToFind);  
anotherBook = mongoTracker.Track(anotherBook);  
```  

### 3. Bulk Writing Changes

After adding entities, changes can be written to the database in a single operation.

```csharp  
await mongoDbContext.Books.BulkWriteAsync(mongoTracker.Commit());  
```  

### 4. Granular Change Updates

MongoTracker tracks changes even in complex documents, including nested objects and collections. With proper entity configuration, the library detects modifications and updates only the changed fields.

Example:

```csharp  
var book = new Book  
{  
    Id = Guid.NewGuid(),  
    Title = "Sample Book",  
    Chapters = new List<BookChapter>  
    {  
        new BookChapter { Name = "Chapter 1", StartPage = 1, EndPage = 10 }  
    }  
};  

mongoTracker.Add(book);  

// Modify the chapter name  
book.Chapters[0].Name = "Updated Chapter 1";  

// The tracker detects the change and updates only the 'Name' field in the 'Chapters' collection.  
await mongoDbContext.Books.BulkWriteAsync(mongoTracker.Commit());  
```  

## Sample Project (MongoTracker.Sample)

The repository includes a sample project, `MongoTracker.Sample`, demonstrating the library's core features. The example covers:

1. **Database Initialization**:
   - Creating collections for authors and books.
   - Seeding the database with test data.

2. **Change Tracking**:
   - Adding, updating, and deleting entities.
   - Bulk writing changes to the database.

3. **Working with Different Entity Types**:
   - Authors, books, chapters, and audiobooks.

## License

MongoTracker is distributed under the MIT License.

## Feedback

If you have questions, suggestions, or encounter issues, please create an [issue](https://github.com/lncendia/MongoTracker/issues) in the project repository.

---  

**MongoTracker** is a simple yet powerful tool for MongoDB that helps you efficiently manage data in your application. Try it in your project and experience its convenience and performance!