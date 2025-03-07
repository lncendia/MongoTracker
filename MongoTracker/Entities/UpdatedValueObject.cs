using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <summary>
/// Абстрактный класс, представляющий объект-значение (value object), который может быть изменен и отслеживает свои изменения.
/// Объект-значение встроен в родительскую сущность и не имеет собственного идентификатора.
/// </summary>
/// <typeparam name="TP">Тип родительской сущности, в которую встроен этот объект-значение.</typeparam>
public abstract class UpdatedValueObject<TP> where TP : UpdatedEntity<TP>
{
    /// <summary>
    /// Флаг, указывающий, была ли сущность изменена.
    /// </summary>
    [BsonIgnore]
    public virtual bool IsModified => _changes.Count > 0 || _structChanges.Count > 0;

    /// <summary>
    /// Словарь для хранения изменений свойств сущности, где значения являются ссылочными типами (reference types) или nullable.
    /// Ключ — имя свойства, значение — новое значение свойства.
    /// Используется для хранения изменений объектов, строк и других ссылочных типов.
    /// </summary>
    private readonly Dictionary<string, object?> _changes = new();

    /// <summary>
    /// Словарь для хранения изменений свойств сущности, где значения являются типами-значениями (value types).
    /// Ключ — имя свойства, значение — новое значение свойства.
    /// Используется для хранения изменений типов-значений (например, int, DateTime, bool) без выполнения боксингa.
    /// </summary>
    private readonly Dictionary<string, ValueType> _structChanges = new();
    
    /// <summary>
    /// Коллекция имен свойств, которые представляют добавленные (а не измененные) объекты-значения.
    /// Если объект-значение был добавлен (например, из null стал не null), его нужно обновить целиком,
    /// а не по отдельным свойствам. Это предотвращает конфликты при обновлении в MongoDB.
    /// </summary>
    protected readonly HashSet<string> AddedValueObjects = [];

    /// <summary>
    /// Отслеживает изменения свойства и возвращает новое значение.
    /// </summary>
    /// <typeparam name="TV">Тип значения свойства.</typeparam>
    /// <param name="propertyName">Имя свойства.</param>
    /// <param name="currentValue">Текущее значение свойства.</param>
    /// <param name="value">Новое значение свойства.</param>
    /// <returns>Новое значение свойства.</returns>
    protected TV? TrackChange<TV>(string propertyName, TV? currentValue, TV? value)
    {
        // Если и текущее значение, и новое значение равны null, возвращаем новое значение.
        if (currentValue == null && value == null) return value;

        // Если текущее значение и новое значение равны (с учетом null), возвращаем текущее значение.
        if (currentValue?.Equals(value) ?? false) return currentValue;

        // Сохраняем новое значение свойства в словарь изменений.
        _changes[propertyName] = value;

        // Возвращаем новое значение.
        return value;
    }

    /// <summary>
    /// Отслеживает изменения свойства для типов-значений (структур) и возвращает новое значение.
    /// </summary>
    /// <typeparam name="TV">Тип значения свойства (структура).</typeparam>
    /// <param name="propertyName">Имя свойства.</param>
    /// <param name="currentValue">Текущее значение свойства.</param>
    /// <param name="value">Новое значение свойства.</param>
    /// <returns>Новое значение свойства.</returns>
    protected TV TrackStructChange<TV>(string propertyName, TV currentValue, TV value) where TV : struct
    {
        // Если текущее значение и новое значение равны, возвращаем текущее значение.
        if (currentValue.Equals(value)) return currentValue;

        // Сохраняем новое значение свойства в словарь изменений.
        _structChanges[propertyName] = value;

        // Возвращаем новое значение.
        return value;
    }

    /// <summary>
    /// Отслеживает изменения объекта-значения (value object) и возвращает новое значение.
    /// Если объект-значение был добавлен (из null стал не null), он добавляется в коллекцию <see cref="AddedValueObjects"/>.
    /// Это позволяет обновлять добавленные объекты целиком, а не по отдельным свойствам.
    /// </summary>
    /// <typeparam name="TV">Тип объекта-значения.</typeparam>
    /// <param name="propertyName">Имя свойства, которое изменяется.</param>
    /// <param name="currentValue">Текущее значение объекта-значения.</param>
    /// <param name="value">Новое значение объекта-значения.</param>
    /// <returns>Новое значение объекта-значения.</returns>
    protected TV? TrackValueObject<TV>(string propertyName, TV? currentValue, TV? value)
        where TV : UpdatedValueObject<TP>
    {
        // Если текущее значение равно null, а новое значение не равно null,
        // удаляем запись об изменении для этого свойства из словаря изменений.
        if (currentValue == null && value != null)
        {
            // Записываем новое значение в словарь изменений.
            _changes[propertyName] = value;
            
            // Добавляем имя свойства в коллекцию AddedValueObjects,
            // чтобы указать, что этот объект нужно обновить целиком.
            AddedValueObjects.Add(propertyName);
        }

        // Если текущее значение не равно null, а новое значение равно null,
        // это означает, что объект-значение был удален.
        else if (currentValue != null && value == null)
        {
            // Записываем null в словарь изменений.
            _changes[propertyName] = value;
        }

        // Возвращаем новое значение объекта-значения.
        return value;
    }


    /// <summary>
    /// Отслеживает изменения коллекции и возвращает новый или обновленный экземпляр TrackedCollection.
    /// </summary>
    /// <typeparam name="TV">Тип элементов коллекции.</typeparam>
    /// <typeparam name="TP">Тип родительской модели, к которой относится коллекция.</typeparam>
    /// <param name="propertyName">Имя свойства.</param>
    /// <param name="currentValue">Текущий экземпляр TrackedCollection.</param>
    /// <param name="newValue">Новая коллекция.</param>
    /// <returns>Новый или обновленный экземпляр TrackedCollection.</returns>
    protected TrackedCollection<TV, TP> TrackCollection<TV>(string propertyName,
        TrackedCollection<TV, TP>? currentValue, List<TV> newValue)
    {
        // Если текущий экземпляр TrackedCollection равен null, создаем новый.
        if (currentValue == null) return new TrackedCollection<TV, TP>(newValue, propertyName);

        // Обновляем текущую коллекцию в существующем экземпляре TrackedCollection.
        currentValue.Collection = newValue;

        // Возвращаем обновленный экземпляр TrackedCollection.
        return currentValue;
    }

    /// <summary>
    /// Отслеживает изменения коллекции объектов и возвращает новый или обновленный экземпляр TrackedObjectCollection.
    /// </summary>
    /// <typeparam name="TV">Тип элементов коллекции (должен быть наследником UpdatedValueObject).</typeparam>
    /// <typeparam name="TP">Тип модели, к которой относится коллекция.</typeparam>
    /// <param name="propertyName">Имя свойства.</param>
    /// <param name="currentValue">Текущий экземпляр TrackedObjectCollection.</param>
    /// <param name="newValue">Новая коллекция.</param>
    /// <returns>Новый или обновленный экземпляр TrackedObjectCollection.</returns>
    protected TrackedValueObjectCollection<TV, TP> TrackValueObjectCollection<TV>(string propertyName,
        TrackedValueObjectCollection<TV, TP>? currentValue, List<TV> newValue) where TV : UpdatedValueObject<TP>
    {
        // Если текущий экземпляр TrackedObjectCollection равен null, создаем новый.
        if (currentValue == null) return new TrackedValueObjectCollection<TV, TP>(newValue, propertyName);

        // Обновляем текущую коллекцию в существующем экземпляре TrackedObjectCollection.
        currentValue.Collection = newValue;

        // Возвращаем обновленный экземпляр TrackedObjectCollection.
        return currentValue;
    }

    /// <summary>
    /// Очищает все изменения и сбрасывает состояние сущности до начального.
    /// Этот метод удаляет все отслеживаемые изменения в свойствах сущности
    /// и сбрасывает её состояние до значения по умолчанию (EntityState.Default).
    /// </summary>
    public virtual void ClearChanges()
    {
        // Очищаем словарь изменений для ссылочных типов и nullable значений.
        // Все изменения, связанные с объектами, строками и другими ссылочными типами, удаляются.
        _changes.Clear();

        // Очищаем словарь изменений для типов-значений (value types).
        // Все изменения, связанные с типами-значениями (например, int, DateTime), удаляются.
        _structChanges.Clear();
        
        // Очищаем коллекцию имен свойств, которые представляют добавленные (а не измененные) объекты-значения.
        AddedValueObjects.Clear();
    }

    /// <summary>
    /// Возвращает определение обновления для MongoDB на основе изменений в сущности.
    /// Если свойство было добавлено, оно не включается в определение обновления, так как должно быть обновлено целиком.
    /// </summary>
    /// <param name="parentPropertyName"></param>
    /// <param name="propertyName"></param>
    /// <param name="blockedParentPropertyNames">Коллекция имен свойств, которые не должны быть обновлены.</param>
    /// <returns>Определение обновления для MongoDB или <c>null</c>, если изменений нет.</returns>
    public virtual UpdateDefinition<TP>? GetUpdateDefinition(string? parentPropertyName, string propertyName,
        IEnumerable<string> blockedParentPropertyNames)
    {
        // Если имя родительского свойства содержится в blockedParentPropertyNames,
        // возвращаем null, так как это свойство не должно быть обновлено, а этот объект будет записан целиком.
        if (blockedParentPropertyNames.Contains(propertyName)) return null;

        // Создаем builder для построения определения обновления.
        var updateBuilder = Builders<TP>.Update;

        // Для каждого измененного свойства (хранятся в словаре _changes)
        // создаем операцию Set, которая указывает, какое поле нужно обновить.
        var updates = _changes

            // Создаем операцию Set для каждого изменения.
            .Select(change => updateBuilder.Set($"{Combine(parentPropertyName,propertyName)}.{change.Key}", change.Value))

            // Добавляем операции Set для изменений из словаря _structChanges.
            // _structChanges содержит типы-значения (value types), чтобы избежать боксингa.
            .Concat(_structChanges.Select(change =>
                updateBuilder.Set($"{Combine(parentPropertyName,propertyName)}.{change.Key}", change.Value)))

            // Преобразуем результат в массив.
            .ToArray();

        // Если массив операций обновления пуст, возвращаем null.
        // В противном случае объединяем все операции в одно определение обновления.
        return updates.Length == 0 ? null : updateBuilder.Combine(updates);
    }

    /// <summary>
    /// Объединяет имя родительского свойства и имя текущего свойства для формирования пути в MongoDB.
    /// Если имя родительского свойства отсутствует (null), возвращается только имя текущего свойства.
    /// </summary>
    /// <param name="parentPropertyName">Имя родительского свойства. Может быть null, если свойство не вложено.</param>
    /// <param name="propertyName">Имя текущего свойства.</param>
    /// <returns>
    /// Строка, представляющая путь к свойству в MongoDB.
    /// Например, если parentPropertyName = "Parent", а propertyName = "Child", метод вернет "Parent.Child".
    /// Если parentPropertyName = null, метод вернет "Child".
    /// </returns>
    protected static string Combine(string? parentPropertyName, string propertyName)
    {
        // Если имя родительского свойства отсутствует, возвращаем только имя текущего свойства.
        if (parentPropertyName == null) return propertyName;
    
        // Возвращаем объединенный путь, разделяя имена свойств точкой.
        return $"{parentPropertyName}.{propertyName}";
    }

    /// <summary>
    /// Объединяет несколько определений обновления в одно.
    /// </summary>
    /// <typeparam name="TP">Тип документа MongoDB.</typeparam>
    /// <param name="updates">Массив определений обновления. Если элемент равен null, он игнорируется.</param>
    /// <returns>Объединенное определение обновления или null, если все элементы массива равны null.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, если массив updates равен null.</exception>
    protected static UpdateDefinition<TP>? Combine(params UpdateDefinition<TP>?[] updates)
    {
        // Фильтруем массив, удаляя все null-значения.
        var nonNullUpdates = updates.Where(u => u != null).ToArray();

        // Используем метод Combine из Builders<T>.Update для объединения всех определений обновления.
        return nonNullUpdates.Length == 0 ? null : Builders<TP>.Update.Combine(nonNullUpdates);
    }
}