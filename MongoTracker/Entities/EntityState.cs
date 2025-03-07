namespace MongoTracker.Entities;

/// <summary>
/// Перечисление для представления состояния сущности.
/// </summary>
public enum EntityState
{
    /// <summary>
    /// Состояние по умолчанию (сущность не была изменена, добавлена или удалена).
    /// </summary>
    Default,

    /// <summary>
    /// Сущность была добавлена.
    /// </summary>
    Added,

    /// <summary>
    /// Сущность была изменена.
    /// </summary>
    Modified,

    /// <summary>
    /// Сущность была удалена.
    /// </summary>
    Deleted
}