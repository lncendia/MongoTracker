using System.Linq.Expressions;
using MongoDB.Driver;
using MongoTracker.Entities;

namespace MongoTracker.Tracker;

/// <summary>
/// Класс предоставляет упрощенный интерфейс для работы с коллекциями MongoDB.
/// Этот класс является фасадом для доступа к коллекциям базы данных и их управления.
/// </summary>
public class MongoTracker<TK, T>(Expression<Func<T, TK>> getIdExpression)
    where T : UpdatedEntity<T>
    where TK : notnull
{
    /// <summary>
    /// Компилируем выражение для получения ID сущности в функцию
    /// </summary>
    private readonly Func<T, TK> _getId = getIdExpression.Compile();
    
    /// <summary>
    /// Словарь для отслеживания изменений сущностей.
    /// Ключ - уникальный идентификатор (ID) сущности.
    /// Значение - сама сущность.
    /// </summary>
    private readonly Dictionary<TK, T> _trackedModels = new();

    /// <summary>
    /// Метод для обновления сущности.
    /// Если сущность уже отслеживается, возвращает её из словаря.
    /// В противном случае добавляет её в словарь отслеживаемых моделей.
    /// </summary>
    /// <param name="model">Сущность, которую нужно обновить.</param>
    /// <returns>Обновленная сущность.</returns>
    public T Track(T model)
    {
        // Проверяем, есть ли сущность с таким ID в словаре отслеживаемых моделей
        if (_trackedModels.TryGetValue(_getId(model), out var trackedModel)) return trackedModel;

        // Если сущность не найдена, очищаем все изменения (если они были)
        model.ClearChanges();

        // Добавляем сущность в словарь отслеживаемых моделей
        _trackedModels.Add(_getId(model), model);

        // Возвращаем обновленную сущность
        return model;
    }

    /// <summary>
    /// Метод для удаления сущности из отслеживаемых моделей.
    /// </summary>
    /// <param name="id">Уникальный идентификатор сущности, которую нужно удалить.</param>
    /// <exception cref="KeyNotFoundException">Исключение, если сущность с указанным ID не найдена.</exception>
    public void Remove(TK id)
    {
        // Проверяем, есть ли сущность с таким ID в словаре отслеживаемых моделей
        if (!_trackedModels.TryGetValue(id, out var trackedModel)) throw new KeyNotFoundException();
        
        // Устанавливаем состояние сущности как "Удалено"
        trackedModel.EntityState = EntityState.Deleted;
    }

    /// <summary>
    /// Метод для добавления новой сущности в отслеживаемые модели.
    /// </summary>
    /// <param name="model">Сущность, которую нужно добавить.</param>
    /// <exception cref="ArgumentException">Исключение, если сущность уже помечена как удаленная.</exception>
    public void Add(T model)
    {
        // Проверяем, не помечена ли сущность как удаленная
        if (model.EntityState == EntityState.Deleted) throw new ArgumentException("Model cannot be deleted");

        // Устанавливаем состояние сущности как "Добавлено"
        model.EntityState = EntityState.Added;
        
        // Добавляем сущность в словарь отслеживаемых моделей
        _trackedModels.Add(_getId(model), model);
    }

    /// <summary>
    /// Метод для фиксации изменений и подготовки операций для выполнения в MongoDB.
    /// </summary>
    /// <returns>Коллекция операций для выполнения в MongoDB.</returns>
    public IReadOnlyCollection<WriteModel<T>> Commit()
    {
        // Находим все добавленные сущности (их состояние == Added)
        var added = _trackedModels
            .Where(s => s.Value.EntityState == EntityState.Added) // Фильтруем только добавленные
            .Select(v => v.Value) // Берем саму сущность (без ключа)
            .ToArray(); // Преобразуем в массив

        // Находим все удаленные сущности (их состояние == Deleted)
        var deleted = _trackedModels
            .Where(s => s.Value.EntityState == EntityState.Deleted) // Фильтруем только удаленные
            .Select(v => v.Key) // Берем только ID
            .ToArray(); // Преобразуем в массив

        // Находим все измененные сущности (их состояние == Modified)
        var modified = _trackedModels
            .Where(s => s.Value.EntityState == EntityState.Modified) // Фильтруем только измененные
            .Select(v => v.Value) // Берем саму сущность (без ключа)
            .ToArray(); // Преобразуем в массив

        // Создаем список для хранения всех операций BulkWrite
        var bulkOperations = new List<WriteModel<T>>();

        // Добавляем операции вставки для новых сущностей
        foreach (var entity in added)
        {
            // Для каждой добавленной сущности создаем операцию InsertOne
            bulkOperations.Add(new InsertOneModel<T>(entity));
        }

        // Добавляем операции удаления для удаленных сущностей
        foreach (var id in deleted)
        {
            // Создаем фильтр для поиска сущности по ID
            var filter = Builders<T>.Filter.Eq(getIdExpression, id); // Eq - это "равно"

            // Создаем операцию удаления для найденной сущности
            bulkOperations.Add(new DeleteOneModel<T>(filter));
        }

        // Добавляем операции обновления для измененных сущностей
        foreach (var entity in modified)
        {
            // Создаем фильтр для поиска сущности по ID
            var filter = Builders<T>.Filter.Eq(getIdExpression, _getId(entity)); // Eq - это "равно"

            // Создаем операцию обновления
            // UpdateDefinition - это специальное свойство из интерфейса UpdatedEntity<T>,
            // которое содержит описание того, какие поля нужно обновить
            bulkOperations.Add(new UpdateOneModel<T>(filter, entity.UpdateDefinition));
        }

        return bulkOperations;
    }
}