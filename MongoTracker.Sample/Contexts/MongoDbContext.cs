using MongoDB.Driver;
using MongoTracker.Sample.Entities.Authors;
using MongoTracker.Sample.Entities.Books;

namespace MongoTracker.Sample.Contexts;

/// <summary>
/// Класс предоставляет упрощенный интерфейс для работы с коллекциями MongoDB.
/// Этот класс является фасадом для доступа к коллекциям базы данных и их управления.
/// </summary>
public class MongoDbContext
{
    /// <summary>
    /// Клиент MongoDB, используемый для взаимодействия с сервером MongoDB.
    /// </summary>
    public IMongoClient Client { get; }

    /// <summary>
    /// Свойство для доступа к коллекции Authors.
    /// Коллекция содержит данные об авторах.
    /// </summary>
    public IMongoCollection<Author> Authors { get; }

    /// <summary>
    /// Свойство для доступа к коллекции Books.
    /// Коллекция содержит данные о книгах.
    /// </summary>
    public IMongoCollection<Book> Books { get; }

    /// <summary>
    /// База данных MongoDB, с которой работает данный контекст.
    /// </summary>
    private readonly IMongoDatabase _database;

    /// <summary>
    /// Конструктор для инициализации контекста работы с MongoDB.
    /// </summary>
    /// <param name="mongoClient">Клиент MongoDB, используемый для подключения к серверу.</param>
    /// <param name="databaseName">Имя базы данных, к которой нужно подключиться.</param>
    public MongoDbContext(IMongoClient mongoClient, string databaseName)
    {
        // Инициализируем свойство Client переданным клиентом MongoDB.
        Client = mongoClient;

        // Получаем базу данных по заданному имени.
        _database = mongoClient.GetDatabase(databaseName);

        // Инициализируем свойство Authors, получая коллекцию "Authors" из базы данных.
        Authors = _database.GetCollection<Author>("Authors");

        // Инициализируем свойство Books, получая коллекцию "Books" из базы данных.
        Books = _database.GetCollection<Book>("Books");
    }

    /// <summary>
    /// Асинхронно создает коллекции в базе данных, если они еще не существуют.
    /// Этот метод гарантирует, что все необходимые коллекции будут созданы перед использованием.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены для асинхронной операции.</param>
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        // Создаем коллекцию "Authors", если она еще не существует.
        await _database.CreateCollectionAsync("Authors", cancellationToken: cancellationToken);
        
        // Создаем коллекцию "Books", если она еще не существует.
        await _database.CreateCollectionAsync("Books", cancellationToken: cancellationToken);
    }
}