# MongoTracker

[![NuGet](https://img.shields.io/nuget/v/MongoTracker.Core.svg)](https://www.nuget.org/packages/MongoTracker.Core)

**MongoTracker** is a lightweight change tracker for MongoDB Client. It **does not replace MongoDB.Driver**; instead, it extends it by automatically tracking entity changes, generating atomic update operations, and supporting bulk writes. MongoTracker works seamlessly with nested objects, collections, and versioned fields.

---

## Features

* **Change Tracking:** Automatically detects changes in fields, nested objects, and collections.
* **Atomic Operations:** Generates minimal `$set`, `$push`, `$pull`, and `$currentDate` operations.
* **Collections & Nested Objects:** Supports `IsTrackedObject()`, `IsCollection()`, `IsTrackedObjectCollection()`.
* **Versioning:** Automatically updates versioned fields (`IsVersion()`).
* **Compatibility:** Works on top of standard `IMongoCollection<T>`.

---

## Installation

```bash
dotnet add package MongoTracker.Core
```

---

## Usage

### 1. Configure your model

```csharp
var config = new ModelBuilder();

config.Entity<Book>(e =>
{
    e.Property(b => b.Id).IsIdentifier();
    e.Property(b => b.Audiobook).IsTrackedObject();
    e.Property(b => b.Authors).IsCollection();
    e.Property(b => b.Chapters).IsTrackedObjectCollection();
    e.Property(b => b.LastUpdate).IsVersion();
});
```

---

### 2. Initialize the tracker

```csharp
var tracker = new MongoTracker<Book>(config);
```

---

### 3. Start tracking existing entities or add new

```csharp
var book = await context.Books.AsQueryable().FirstAsync(b => b.Title == "A Game of Thrones");
book = tracker.Track(book);

var book2 = new Book() { Title = "The Shining" };
tracker.Add(book2)
```

---

### 4. Modify tracked entities

```csharp
// Update a field in a nested object
book.Audiobook.Duration = TimeSpan.FromHours(30).TotalMinutes;

// Update a nested collection inside a tracked object
book.Chapters.First().Footnotes.Add("Important note");

// Add a new element to a collection
book.Chapters.Add(new BookChapter { Name = "Bran", StartPage = 31, EndPage = 50 });
```

---

### 5. Mark the entity for deletion

```csharp
tracker.Delete(book);
```

---

### 6. Commit changes to MongoDB

```csharp
var updates = tracker.Commit();
await context.Books.BulkWriteAsync(updates);
```

---


## Sample Project

The repository includes a sample project, `MongoTracker.Sample`, demonstrating the library's core features.
For a real-world usage example, see [Overoom](https://github.com/lncendia/Overoom).

## License

MongoTracker is distributed under the MIT License.

## Feedback

If you have questions, suggestions, or encounter issues, please create an [issue](https://github.com/lncendia/MongoTracker/issues) in the project repository.

---  

**MongoTracker** is a simple yet powerful tool for MongoDB that helps you efficiently manage data in your application. Try it in your project and experience its convenience and performance!