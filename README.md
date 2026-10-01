# Incendia.MongoTracker

[![NuGet](https://img.shields.io/nuget/v/Incendia.MongoTracker.svg)](https://www.nuget.org/packages/Incendia.MongoTracker)

**MongoTracker** is a lightweight change tracker for MongoDB Client. It **does not replace MongoDB.Driver**; instead, it extends it by automatically tracking entity changes, generating atomic update operations, and supporting bulk writes. MongoTracker works seamlessly with nested objects, collections, and versioned fields.

---

## Features

* **Change Tracking:** Automatically detects changes in fields, nested objects, and collections.
* **Atomic Operations:** Generates minimal `$set`, `$push` and `$pull` operations.
* **Collections & Nested Objects:** Supports `IsChild()`, `IsSet()`, `IsTrackedSet()`, `IsCollection()`.
* **Optimistic Concurrency:** Versioned fields (`IsVersion()`) and concurrency tokens (`IsConcurrencyToken()`), with `MongoConcurrencyException` on conflicts.
* **Compatibility:** Works on top of standard `IMongoCollection<T>`.

---

## Installation

```bash
dotnet add package Incendia.MongoTracker
```

Targets `netstandard2.1` and requires `MongoDB.Driver` 3.9.0 or later.

---

## Usage

### 1. Configure your model

```csharp
var config = new ModelBuilder();

config.Entity<Book>(e =>
{
    e.Property(b => b.Id).IsIdentifier();
    e.Property(b => b.Audiobook).IsChild();
    e.Property(b => b.Authors).IsSet();
    e.Property(b => b.Chapters).IsTrackedSet();
    e.Property(b => b.LastUpdate).IsVersion();
});
```

#### Property modes

| Mode | Use for | What is written |
|---|---|---|
| *(none)* | Scalars, strings, enums, immutable values | A changed value is written with `$set` as a whole |
| `IsIdentifier()` | The document key, the key of tracked set elements | Not included in updates; used in the update filter and to match set elements |
| `IsChild()` | A nested object | Changed fields with `$set` on `Parent.Field`; a new object or `null` as a whole |
| `IsSet()` | An unordered collection of values (tags, ids) | Only added: `$push` with `$each`; only removed: `$pullAll`; both: `$set` of the whole array |
| `IsCollection()` | An ordered list of values | Any difference in content or order: `$set` of the whole array |
| `IsTrackedSet()` | A collection of nested objects | Only added: `$push`; only removed: `$pull` by identifier (`$pullAll` by value without one); only modified: `$set` inside the elements; mixed: `$set` of the whole array |
| `IsVersion()` | An `int`, `long` or `DateTime` version | Set to the next value on every update; the original value is checked in the filter |
| `IsConcurrencyToken()` | A value you manage yourself | The original value is checked in the filter; written like a plain property when changed |
| `IsIgnored()` | Data that must not be tracked | Never compared and never included in updates (inserted documents are written as they are) |

* Properties do not have to be configured: everything not listed is tracked as a plain property, and so is
  `Property(x => x.Name)` without a mode.
* A property without a mode is compared with `Equals` to the value captured by `Track`. Changes made in place to a
  list or a nested object (the same instance) are therefore **not** detected; only assigning another value is.
  Configure such properties with `IsChild()`, `IsSet()`, `IsCollection()` or `IsTrackedSet()`.
* Properties ignored by BSON serialization (`[BsonIgnore]` or not mapped in a registered class map) are skipped.
* Update paths use BSON element names and member serializers (`[BsonElement]`, `[BsonRepresentation]`, conventions).
* Concurrency tokens of nested objects (`IsChild()`) are checked in the filter too, by their full path
  (`Child.Token`).
* The version belongs to the root entity. In the rare case when the stored document is a wrapper around the actual
  entity, the version can instead be declared on one nested object (`IsChild()`).

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
tracker.Add(book2);
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

#### Tracked set elements with identifiers

Configure an identifier on the element type of a tracked set, so its elements are addressed by it instead of by
their position in the array. For example, a room whose viewers are changed by several users at once:

```csharp
public class Room
{
    public Guid Id { get; set; }
    public List<Viewer> Viewers { get; set; } = [];
}

public class Viewer
{
    public Guid Id { get; set; }
    public bool OnPause { get; set; }
    public List<string> Tags { get; set; } = [];
}

config.Entity<Room>(e =>
{
    e.Property(r => r.Id).IsIdentifier();
    e.Property(r => r.Viewers).IsTrackedSet();
});

config.Entity<Viewer>(e =>
{
    e.Property(v => v.Id).IsIdentifier();
    e.Property(v => v.Tags).IsSet();
});
```

* modified elements are updated with `$set` on `Viewers.$[i0].OnPause` and the array filter `{ "i0._id": <id> }`;
* nested collections are addressed the same way: `$push` into `Viewers.$[i0].Tags`;
* removed elements are pulled by identifier: `$pull: { Viewers: { _id: { $in: [...] } } }`;
* an element replaced by another instance with the same identifier is tracked as modified;
* several elements with the same identifier in one set are not allowed.

Without an identifier, modified elements are addressed by their position (`Viewers.2.OnPause`) and removed elements
are pulled by their whole value. That breaks when the array is changed concurrently: another write that removes an
element shifts the positions, so the update lands on a different element. Use identifiers for arrays that can be
modified in parallel.

When a single save combines additions, removals or modifications in the same set, the whole array is replaced with `$set`,
because MongoDB does not allow these operations on one field in a single update. Protect such saves with a version
field.

---

### 5. Mark the entity for deletion

```csharp
tracker.Delete(book);
```

---

### 6. Commit changes to MongoDB

```csharp
var result = await tracker.SaveChangesAsync(context.Books);
```

---

### 7. Handle concurrency conflicts

A version field can be `int`, `long` or `DateTime`. Integer versions are incremented, DateTime versions receive the
current UTC time (millisecond precision). The new value is written back into the entity after a successful save.
Prefer integer versions: they cannot collide.

If an update or delete matches fewer documents than expected (the document was changed or removed since it was loaded),
`SaveChanges` throws `MongoConcurrencyException` and the tracked changes are **not** accepted. Operations that did
succeed are already persisted, so pass a session with a transaction when the whole save must be atomic:

```csharp
try
{
    await tracker.SaveChangesAsync(context.Books, session);
}
catch (MongoConcurrencyException)
{
    await session.AbortTransactionAsync();
    // Reload the data and retry the operation
}
```

---


## Sample Project

The repository includes a sample project, `Incendia.MongoTracker.Sample`, demonstrating the library's core features.
For a real-world usage example, see [Incendia.Identity.Mongo](https://github.com/lncendia/Identity.Mongo) or [Overoom](https://github.com/lncendia/Overoom).

## License

MongoTracker is distributed under the MIT License.

## Feedback

If you have questions, suggestions, or encounter issues, please create an [issue](https://github.com/lncendia/MongoTracker/issues) in the project repository.

---

**MongoTracker** is a simple yet powerful tool for MongoDB that helps you efficiently manage data in your application. Try it in your project and experience its convenience and performance!
