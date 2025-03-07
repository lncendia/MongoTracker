using MongoTracker.Entities;

namespace MongoTracker.Sample.Entities.Authors;

/// <summary>
/// Модель автора для работы с базой данных MongoDB.
/// Наследуется от абстрактного класса UpdatedEntity,
/// который предоставляет функциональность отслеживания изменений и управления состоянием сущности.
/// </summary>
public class Author : UpdatedEntity<Author>
{
    private Guid _id;
    private string _name = null!;

    /// <summary>
    /// Уникальный идентификатор автора.
    /// </summary>
    public Guid Id
    {
        get => _id; // Геттер возвращает значение приватного поля _id.
        set => _id = TrackStructChange(nameof(Id), _id, value); // Сеттер отслеживает изменения и обновляет значение.
    }

    /// <summary>
    /// Имя автора. Обязательное поле.
    /// </summary>
    public required string Name
    {
        get => _name; // Геттер возвращает значение приватного поля _name.
        set => _name = TrackChange(nameof(Name), _name, value)!; // Сеттер отслеживает изменения и обновляет значение.
    }
}