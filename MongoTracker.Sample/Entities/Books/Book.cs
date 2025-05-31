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
    private TrackedCollection<Guid, Book> _authors = new();
    private TrackedValueObjectCollection<BookChapter, Book> _chapters  = new();

    /// <summary>
    /// Уникальный идентификатор книги.
    /// </summary>
    public Guid Id
    {
        // Геттер возвращает значение приватного поля _id.
        get => _id;

        // Сеттер отслеживает изменения и обновляет значение.
        set => _id = TrackStructChange(nameof(Id), _id, value);
    }

    /// <summary>
    /// Название книги. Обязательное поле.
    /// </summary>
    public required string Title
    {
        // Геттер возвращает значение приватного поля _title.
        get => _title;

        // Сеттер отслеживает изменения и обновляет значение.
        set => _title = TrackChange(nameof(Title), _title, value)!;
    }

    /// <summary>
    /// Данные аудиокниги. Необязательное поле.
    /// </summary>
    public Audiobook? Audiobook
    {
        // Геттер возвращает значение приватного поля _audiobook.
        get => _audiobook;

        // Сеттер отслеживает изменения и обновляет значение.
        set => _audiobook = TrackValueObject(nameof(Audiobook), _audiobook, value)!;
    }

    /// <summary>
    /// Массив идентификаторов авторов книги. По умолчанию пустой массив.
    /// </summary>
    public List<Guid> Authors
    {
        // Геттер возвращает значение приватного поля _authors или пустой массив, если значение null.
        get => _authors.Collection;

        // Сеттер отслеживает изменения и обновляет значение.
        set => _authors = TrackCollection(nameof(Authors), _authors, value)!;
    }

    /// <summary>
    /// Массив глав книги. По умолчанию пустой массив.
    /// </summary>
    public List<BookChapter> Chapters
    {
        // Геттер возвращает значение приватного поля _chapters или пустой массив, если значение null.
        get => _chapters.Collection;

        // Сеттер отслеживает изменения и обновляет значение.
        set => _chapters = TrackValueObjectCollection(nameof(Chapters), _chapters, value)!;
    }

    /// <inheritdoc/>
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
            var authorsUpdateDefinition = _authors.GetUpdateDefinition(null, nameof(Authors), AddedValueObjects);

            // Получаем определение обновления для списка глав, если он был изменен.
            var chaptersUpdateDefinition = _chapters.GetUpdateDefinition(null, nameof(Chapters), AddedValueObjects);

            // Объединяем все определения обновления в одно и возвращаем результат.
            return Combine(
                baseUpdateDefinition,
                audiobookUpdateDefinition,
                authorsUpdateDefinition,
                chaptersUpdateDefinition
            );
        }
    }
    /// <inheritdoc/>

    /// <summary>
    /// Возвращает текущее состояние сущности, объединяя состояния всех отслеживаемых свойств.
    /// </summary>
    public override EntityState EntityState => Combine(
        
        // Состояние аудиокниги, если она была изменена.
        _audiobook?.IsModified,

        // Состояние списка авторов, если он был изменен.
        _authors.IsModified,

        // Состояние списка глав, если он был изменен.
        _chapters.IsModified
    );

    /// <inheritdoc/>
    /// <summary>
    /// Очищает все изменения в сущности и всех её отслеживаемых свойствах.
    /// </summary>
    public override void ClearChanges()
    {
        // Очищаем изменения в базовой сущности.
        base.ClearChanges();

        // Очищаем изменения в аудиокниге, если она существует.
        _audiobook?.ClearChanges();

        // Очищаем изменения в списке авторов.
        _authors.ClearChanges();

        // Очищаем изменения в списке глав.
        _chapters.ClearChanges();
    }
}