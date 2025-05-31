using MongoDB.Driver;
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
    private TrackedCollection<string, Book>? _footnotes;

    /// <summary>
    /// Массив cносок главы.
    /// </summary>
    public List<string>? Footnotes
    {
        // Геттер возвращает значение приватного поля _authors или пустой массив, если значение null.
        get => _footnotes?.Collection;

        // Сеттер отслеживает изменения и обновляет значение.
        set => _footnotes = TrackCollection(nameof(Footnotes), _footnotes, value)!;
    }

    /// <summary>
    /// Название главы. Обязательное поле.
    /// </summary>
    public required string Name
    {
        // Геттер возвращает значение приватного поля _name.
        get => _name;

        // Сеттер отслеживает изменения и обновляет значение.
        set => _name = TrackChange(nameof(Name), _name, value)!;
    }

    /// <summary>
    /// Номер начальной страницы главы. Обязательное поле.
    /// </summary>
    public required int StartPage
    {
        // Геттер возвращает значение приватного поля _startPage.
        get => _startPage;

        // Сеттер отслеживает изменения и обновляет значение.
        set => _startPage = TrackStructChange(nameof(StartPage), _startPage, value);
    }

    /// <summary>
    /// Номер конечной страницы главы. Обязательное поле.
    /// </summary>
    public required int EndPage
    {
        // Геттер возвращает значение приватного поля _endPage.
        get => _endPage;

        // Сеттер отслеживает изменения и обновляет значение.
        set => _endPage = TrackStructChange(nameof(EndPage), _endPage, value);
    }

    /// <inheritdoc/>
    /// <summary>
    /// Возвращает определение обновления для MongoDB, объединяя изменения из всех отслеживаемых свойств сущности.
    /// </summary>
    public override UpdateDefinition<Book>? GetUpdateDefinition(string? parentPropertyName, string propertyName,
        IReadOnlyCollection<string> blockedParentPropertyNames)
    {
        // Если имя родительского свойства содержится в blockedParentPropertyNames,
        // возвращаем null, так как это свойство не должно быть обновлено, а этот объект будет записан целиком.
        if (blockedParentPropertyNames.Contains(propertyName)) return null;

        var footnotesDefinition = _footnotes?
            .GetUpdateDefinition(Combine(parentPropertyName, propertyName), nameof(Footnotes), AddedValueObjects);

        var baseDefinition = base.GetUpdateDefinition(parentPropertyName, propertyName, blockedParentPropertyNames);

        return Combine(footnotesDefinition, baseDefinition);
    }

    /// <inheritdoc/>
    /// <summary>
    /// Возвращает текущее состояние сущности, объединяя состояния всех отслеживаемых свойств.
    /// </summary>
    public override bool IsModified => base.IsModified || (_footnotes?.IsModified ?? false);

    /// <inheritdoc/>
    /// <summary>
    /// Очищает все изменения в сущности и всех её отслеживаемых свойствах.
    /// </summary>
    public override void ClearChanges()
    {
        // Очищаем изменения в списке сносок, если он существует.
        _footnotes?.ClearChanges();
        
        // Очищаем изменения в базовой сущности.
        base.ClearChanges();
    }
}