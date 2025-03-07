using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoTracker.Sample.Contexts;
using MongoTracker.Sample.Entities.Authors;
using MongoTracker.Sample.Entities.Books;
using MongoTracker.Tracker;

// Регистрируем сериализатор для типа Guid, чтобы он использовал стандартное представление (GuidRepresentation.Standard).
// Это гарантирует корректную работу с GUID в MongoDB, так как по умолчанию MongoDB использует устаревший формат.
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Создаем клиент MongoDB для подключения к базе данных.
// Используем строку подключения с указанием логина, пароля, хоста и порта.
using var mongoClient = new MongoClient("mongodb://root:example@mongotracker.mongo:27017/admin");

// Инициализируем контекст базы данных, передавая клиент MongoDB и имя базы данных.
var context = new MongoDbContext(mongoClient, "BookApp");

// Создаем коллекции в базе данных, если они еще не существуют.
await context.EnsureCreatedAsync();

// Инициализируем базу данных начальными данными (авторы, книги и т.д.).
await InitializeDatabaseAsync(context);

// Создаем трекер для отслеживания изменений в коллекции книг.
// Используем Guid в качестве идентификатора и Book в качестве типа сущности.
var tracker = new MongoTracker<Guid, Book>(tk => tk.Id);

// Находим идентификатор автора "Jim Dale" в коллекции Authors.
var jimDaleId = await context.Authors
    .AsQueryable()
    .Where(a => a.Name == "Jim Dale")
    .Select(a => a.Id)
    .FirstAsync();

// Находим книгу "A Game of Thrones" в коллекции Books.
var gameOfThrones = await context.Books
    .AsQueryable()
    .FirstAsync(b => b.Title == "A Game of Thrones");

// Находим книгу "The Shining" в коллекции Books.
var shining = await context.Books
    .AsQueryable()
    .FirstAsync(b => b.Title == "The Shining");

// Находим книгу "Harry Potter and the Philosopher's Stone" в коллекции Books.
var harryPotter = await context.Books
    .AsQueryable()
    .FirstAsync(b => b.Title == "Harry Potter and the Philosopher's Stone");

// Начинаем отслеживать изменения для этой книги.
harryPotter = tracker.Track(harryPotter);

// Начинаем отслеживать изменения для этой книги.
shining = tracker.Track(shining);

// Начинаем отслеживать изменения для этой книги.
gameOfThrones = tracker.Track(gameOfThrones);

// Добавляем новую главу в книгу "A Game of Thrones".
gameOfThrones.Chapters.Add(new BookChapter
{
    Name = "Bran",
    StartPage = 31, 
    EndPage = 50
});

// Обновляем длительность аудиокниги для "A Game of Thrones".
gameOfThrones.Audiobook!.Duration = TimeSpan.FromHours(30).TotalMinutes;

// Создаем новую аудиокнигу для "The Shining" и связываем её с автором "Jim Dale".
shining.Audiobook = new Audiobook
{
    Author = jimDaleId,
    Duration = TimeSpan.FromHours(14).TotalMinutes
};

// Удаляем книгу "Harry Potter and the Philosopher's Stone" из трекера.
tracker.Remove(harryPotter.Id);

// Формируем запросы для обновления данных в MongoDB.
// Трекер сформирует следующие точечные обновления:
// 1. Для книги "A Game of Thrones":
//    - Обновление поля Audiobook.Duration на 1800 минут.
//    - Добавление новой главы в массив Chapters.
//    - Обновление поля LastModified на текущую дату.
//    Пример запроса:
//    db.Books.updateOne(
//        { _id: ObjectId("...") },
//        {
//            $set: { "Audiobook.Duration": 1800, LastModified: ISODate("...") },
//            $push: { Chapters: { Name: "Bran", StartPage: 31, EndPage: 50 } }
//        }
//    )
//
// 2. Для книги "The Shining":
//    - Установка нового объекта Audiobook с указанием автора и длительности.
//    - Обновление поля LastModified на текущую дату.
//    Пример запроса:
//    db.Books.updateOne(
//        { _id: ObjectId("...") },
//        {
//            $set: { Audiobook: { Author: ObjectId("..."), Duration: 840 }, LastModified: ISODate("...") }
//        }
//    )
//
// 3. Для книги "Harry Potter and the Philosopher's Stone":
//    - Удаление документа из коллекции Books.
//    Пример запроса:
//    db.Books.deleteOne({ _id: ObjectId("...") })
var update = tracker.Commit();

// Применяем все изменения (добавления, обновления, удаления) в базе данных.
await context.Books.BulkWriteAsync(update);

// Завершаем выполнение программы.
return;


// Асинхронный метод для инициализации базы данных.
// Принимает контекст базы данных MongoDB (MongoDbContext) в качестве параметра.
async Task InitializeDatabaseAsync(MongoDbContext mongoDbContext)
{
    // Создаем трекер для отслеживания изменений в коллекции авторов.
    // Используем Guid в качестве идентификатора и Author в качестве типа сущности.
    var mongoAuthorTracker = new MongoTracker<Guid, Author>(tk => tk.Id);

    // Создаем список авторов.
    var authors = new List<Author>
    {
        // Инициализация автора J.K. Rowling с уникальным идентификатором.
        new() { Id = Guid.NewGuid(), Name = "J.K. Rowling" },

        // Инициализация автора George R.R. Martin с уникальным идентификатором.
        new() { Id = Guid.NewGuid(), Name = "George R.R. Martin" },

        // Инициализация автора Stephen King с уникальным идентификатором.
        new() { Id = Guid.NewGuid(), Name = "Stephen King" },

        // Инициализация актера озвучки Jim Dale с уникальным идентификатором.
        // Известный актер озвучки для книг о Гарри Поттере.
        new() { Id = Guid.NewGuid(), Name = "Jim Dale" },
        
        // Инициализация актера озвучки Roy Dotrice с уникальным идентификатором.
        // Актер озвучки для книг "Песнь Льда и Пламени".
        new() { Id = Guid.NewGuid(), Name = "Roy Dotrice" },
    };

    // Добавляем каждого автора в трекер для отслеживания изменений.
    foreach (var author in authors) mongoAuthorTracker.Add(author);

    // Выполняем массовую запись авторов в коллекцию Authors базы данных.
    await mongoDbContext.Authors.BulkWriteAsync(mongoAuthorTracker.Commit());

    // Создаем трекер для отслеживания изменений в коллекции книг.
    // Используем Guid в качестве идентификатора и Book в качестве типа сущности.
    var mongoBookTracker = new MongoTracker<Guid, Book>(tk => tk.Id);

    // Создаем список книг.
    var books = new List<Book>
    {
        // Инициализация книги "Harry Potter and the Philosopher's Stone".
        new()
        {
            // Уникальный идентификатор книги.
            Id = Guid.NewGuid(),

            // Название книги.
            Title = "Harry Potter and the Philosopher's Stone",

            // Список идентификаторов авторов книги. В данном случае J.K. Rowling.
            Authors = [authors[0].Id],

            // Инициализация аудиокниги.
            Audiobook = new Audiobook
            {
                // Идентификатор автора озвучки (Jim Dale).
                Author = authors[3].Id,

                // Длительность аудиокниги в минутах (8 часов).
                Duration = 480
            },

            // Список глав книги.
            Chapters =
            [
                // Глава "The Boy Who Lived" с указанием начальной и конечной страницы.
                new BookChapter { Name = "The Boy Who Lived", StartPage = 1, EndPage = 15 },

                // Глава "The Vanishing Glass" с указанием начальной и конечной страницы.
                new BookChapter { Name = "The Vanishing Glass", StartPage = 16, EndPage = 30 }
            ]
        },

        // Инициализация книги "A Game of Thrones".
        new()
        {
            // Уникальный идентификатор книги.
            Id = Guid.NewGuid(),

            // Название книги.
            Title = "A Game of Thrones",

            // Список идентификаторов авторов книги. В данном случае George R.R. Martin.
            Authors = [authors[1].Id],
            
            // Инициализация аудиокниги.
            Audiobook = new Audiobook
            {
                // Идентификатор автора озвучки (Roy Dotrice).
                Author = authors[4].Id,

                // Длительность аудиокниги в минутах (8 часов).
                Duration = 480
            },

            // Список глав книги.
            Chapters =
            [
                // Глава "Prologue" с указанием начальной и конечной страницы.
                new BookChapter { Name = "Prologue", StartPage = 1, EndPage = 10 }
            ]
        },

        // Инициализация книги "The Shining".
        new()
        {
            // Уникальный идентификатор книги.
            Id = Guid.NewGuid(),

            // Название книги.
            Title = "The Shining",

            // Список идентификаторов авторов книги. В данном случае Stephen King.
            Authors = [authors[2].Id],

            // Список глав книги.
            Chapters =
            [
                // Глава "Prologue" с указанием начальной и конечной страницы.
                new BookChapter { Name = "Prologue", StartPage = 1, EndPage = 5 },

                // Глава "The Interview" с указанием начальной и конечной страницы.
                new BookChapter { Name = "The Interview", StartPage = 6, EndPage = 20 }
            ]
        }
    };

    // Добавляем каждую книгу в трекер для отслеживания изменений.
    foreach (var book in books) mongoBookTracker.Add(book);

    // Выполняем массовую запись книг в коллекцию Books базы данных.
    await mongoDbContext.Books.BulkWriteAsync(mongoBookTracker.Commit());
}