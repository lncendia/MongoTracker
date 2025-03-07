using MongoDB.Driver;
using MongoTracker.Entities;

namespace MongoTracker.Sample.Entities.Books;

/// <summary>
/// Модель книги для работы с базой данных MongoDB.
/// Наследуется от абстрактного класса UpdatedEntity,
/// который предоставляет функциональность отслеживания изменений и управления состоянием сущности.
/// </summary>
public class Book : UpdatedEntity<Book>
{
    private Guid _id;
    private string _title = null!;
    private Audiobook? _audiobook;
    private TrackedCollection<Guid, Book>? _authors;
    private TrackedValueObjectCollection<BookChapter, Book>? _chapters;

    /// <summary>
    /// Уникальный идентификатор книги.
    /// </summary>
    public Guid Id
    {
        get => _id; // Геттер возвращает значение приватного поля _id.
        set => _id = TrackStructChange(nameof(Id), _id, value); // Сеттер отслеживает изменения и обновляет значение.
    }

    /// <summary>
    /// Название книги. Обязательное поле.
    /// </summary>
    public required string Title
    {
        get => _title; // Геттер возвращает значение приватного поля _title.
        set => _title =
            TrackChange(nameof(Title), _title, value)!; // Сеттер отслеживает изменения и обновляет значение.
    }

    /// <summary>
    /// Данные аудиокниги. Необязательное поле.
    /// </summary>
    public Audiobook? Audiobook
    {
        get => _audiobook; // Геттер возвращает значение приватного поля _audiobook.
        set => _audiobook =
            TrackValueObject(nameof(Audiobook), _audiobook,
                value)!; // Сеттер отслеживает изменения и обновляет значение.
    }

    /// <summary>
    /// Массив идентификаторов авторов книги. По умолчанию пустой массив.
    /// </summary>
    public List<Guid> Authors
    {
        get => _authors?.Collection ??
               []; // Геттер возвращает значение приватного поля _authors или пустой массив, если значение null.
        set => _authors =
            TrackCollection(nameof(Authors), _authors, value); // Сеттер отслеживает изменения и обновляет значение.
    }

    /// <summary>
    /// Массив глав книги. По умолчанию пустой массив.
    /// </summary>
    public List<BookChapter> Chapters
    {
        get => _chapters?.Collection ??
               []; // Геттер возвращает значение приватного поля _chapters или пустой массив, если значение null.
        set => _chapters =
            TrackValueObjectCollection(nameof(Chapters), _chapters,
                value); // Сеттер отслеживает изменения и обновляет значение.
    }

    /// <summary>
    /// Возвращает определение обновления для MongoDB, объединяя изменения из всех отслеживаемых свойств сущности.
    /// </summary>
    public override UpdateDefinition<Book> UpdateDefinition
    {
        get
        {
            // Получаем базовое определение обновления из родительского класса.
            var baseUpdateDefinition = base.UpdateDefinition;

            // Получаем определение обновления для аудиокниги, если она была изменена.
            var audiobookUpdateDefinition = _audiobook?.GetUpdateDefinition(null, nameof(Audiobook), AddedValueObjects);

            // Получаем определение обновления для списка авторов, если он был изменен.
            var authorsUpdateDefinition = _authors?.GetUpdateDefinition();

            // Получаем определение обновления для списка глав, если он был изменен.
            var chaptersUpdateDefinition = _chapters?.GetUpdateDefinition();

            // Объединяем все определения обновления в одно и возвращаем результат.
            return Combine(
                baseUpdateDefinition,
                audiobookUpdateDefinition,
                authorsUpdateDefinition,
                chaptersUpdateDefinition
            );
        }
    }

    /// <summary>
    /// Возвращает текущее состояние сущности, объединяя состояния всех отслеживаемых свойств.
    /// </summary>
    public override EntityState EntityState => Combine(

        // Состояние аудиокниги, если она была изменена.
        _audiobook?.IsModified,

        // Состояние списка авторов, если он был изменен.
        _authors?.IsModified,

        // Состояние списка глав, если он был изменен.
        _chapters?.IsModified
    );

    /// <summary>
    /// Очищает все изменения в сущности и всех её отслеживаемых свойствах.
    /// </summary>
    public override void ClearChanges()
    {
        // Очищаем изменения в базовой сущности.
        base.ClearChanges();

        // Очищаем изменения в аудиокниге, если она существует.
        _audiobook?.ClearChanges();

        // Очищаем изменения в списке глав, если он существует.
        _chapters?.ClearChanges();
    }
}