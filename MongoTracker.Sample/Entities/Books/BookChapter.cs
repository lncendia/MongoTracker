using MongoTracker.Entities;

namespace MongoTracker.Sample.Entities.Books;

/// <summary>
/// Модель главы книги для работы с базой данных MongoDB.
/// Наследуется от абстрактного класса UpdatedEntity,
/// который предоставляет функциональность отслеживания изменений и управления состоянием сущности.
/// </summary>
public class BookChapter : UpdatedValueObject<Book>
{
    private string _name = null!;
    private int _startPage;
    private int _endPage;

    /// <summary>
    /// Название главы. Обязательное поле.
    /// </summary>
    public required string Name
    {
        get => _name; // Геттер возвращает значение приватного поля _name.
        set => _name = TrackChange(nameof(Name), _name, value)!; // Сеттер отслеживает изменения и обновляет значение.
    }

    /// <summary>
    /// Номер начальной страницы главы. Обязательное поле.
    /// </summary>
    public required int StartPage
    {
        get => _startPage; // Геттер возвращает значение приватного поля _startPage.
        set => _startPage = TrackStructChange(nameof(StartPage), _startPage, value); // Сеттер отслеживает изменения и обновляет значение.
    }

    /// <summary>
    /// Номер конечной страницы главы. Обязательное поле.
    /// </summary>
    public required int EndPage
    {
        get => _endPage; // Геттер возвращает значение приватного поля _endPage.
        set => _endPage = TrackStructChange(nameof(EndPage), _endPage, value); // Сеттер отслеживает изменения и обновляет значение.
    }
}