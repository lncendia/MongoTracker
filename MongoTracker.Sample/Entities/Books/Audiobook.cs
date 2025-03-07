using MongoTracker.Entities;

namespace MongoTracker.Sample.Entities.Books;

/// <summary>
/// Модель аудиокниги для работы с базой данных MongoDB.
/// Наследуется от абстрактного класса UpdatedEntity,
/// который предоставляет функциональность отслеживания изменений и управления состоянием сущности.
/// </summary>
public class Audiobook : UpdatedValueObject<Book>
{
    private Guid _author;
    private double _duration;

    /// <summary>
    /// Идентификатор автора аудиокниги.
    /// </summary>
    public Guid Author
    {
        get => _author; // Геттер возвращает значение приватного поля _author.
        set => _author = TrackStructChange(nameof(Author), _author, value); // Сеттер отслеживает изменения и обновляет значение.
    }

    /// <summary>
    /// Длительность аудиокниги в минутах.
    /// </summary>
    public double Duration
    {
        get => _duration; // Геттер возвращает значение приватного поля _duration.
        set => _duration = TrackStructChange(nameof(Duration), _duration, value); // Сеттер отслеживает изменения и обновляет значение.
    }
}