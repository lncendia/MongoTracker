using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <summary>
/// Абстрактный класс, представляющий сущность (entity), которая может быть изменена и отслеживает свои изменения.
/// </summary>
/// <typeparam name="T">Тип данных, который будет использоваться для этой сущности.</typeparam>
public abstract class UpdatedEntity<T> where T : UpdatedEntity<T>
{
    /// <summary>
    /// Время последнего изменения сущности.
    /// </summary>
    public DateTime LastModified { get; private set; }

    /// <summary>
    /// Текущее состояние сущности (Default, Added, Modified, Deleted).
    /// </summary>
    [BsonIgnore]
    public virtual EntityState EntityState
    {
        get => _entityStateValue;
        set => _entityStateValue = value;
    }

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
    /// Текущее состояние сущности (Default, Added, Modified, Deleted).
    /// </summary>
    private EntityState _entityStateValue = EntityState.Default;

    /// <summary>
    /// Возвращает определение обновления для MongoDB.
    /// Это свойство используется для создания запроса обновления в базе данных.
    /// </summary>
    public virtual UpdateDefinition<T> UpdateDefinition
    {
        get
        {
            // Если состояние сущности не является "Modified", значит изменений нет и вызывается InvalidOperationException
            if (EntityState != EntityState.Modified) throw new InvalidOperationException();

            // Создаем builder для построения определения обновления.
            var updateBuilder = Builders<T>.Update;

            // Для каждого измененного свойства (хранятся в словаре _changes и _structChanges)
            // создаем операцию Set, которая указывает, какое поле нужно обновить.
            var updates = _changes

                // Создаем операцию Set для каждого изменения в словаре _changes.
                // _changes содержит ссылочные типы (reference types) или nullable значения.
                .Select(change => updateBuilder.Set(change.Key, change.Value))

                // Добавляем операции Set для изменений из словаря _structChanges.
                // _structChanges содержит типы-значения (value types), чтобы избежать боксингa.
                .Concat(_structChanges.Select(change => updateBuilder.Set(change.Key, change.Value)))

                // Преобразуем результат в массив для дальнейшего объединения.
                .ToArray();

            // Объединяем все операции Set в одно определение обновления.
            // Это позволяет выполнить все изменения в одном запросе к базе данных.
            return updateBuilder.Combine(updates);
        }
    }

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
        // Проверяем, если и текущее значение, и новое значение равны null.
        if (currentValue == null && value == null) return value;

        // Проверяем, если текущее значение и новое значение равны (с учетом null).
        if (currentValue?.Equals(value) ?? false) return currentValue;

        // Сохраняем новое значение свойства в словарь изменений.
        _changes[propertyName] = value;

        // Обновляем время последнего изменения.
        LastModified = DateTime.UtcNow;

        // Добавляем или обновляем время последнего изменения в словаре изменений.
        _structChanges[nameof(LastModified)] = LastModified;

        // Устанавливаем состояние сущности как "Modified".
        _entityStateValue = EntityState.Modified;

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
        // Проверяем, если текущее значение и новое значение равны.
        if (currentValue.Equals(value)) return currentValue;

        // Сохраняем новое значение свойства в словарь изменений.
        _structChanges[propertyName] = value;

        // Обновляем время последнего изменения.
        LastModified = DateTime.UtcNow;

        // Добавляем или обновляем время последнего изменения в словаре изменений.
        _structChanges[nameof(LastModified)] = LastModified;

        // Устанавливаем состояние сущности как "Modified".
        _entityStateValue = EntityState.Modified;

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
        where TV : UpdatedValueObject<T>
    {
        // Если текущее значение равно null, а новое значение не равно null,
        // это означает, что объект-значение был добавлен.
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
    /// Реализует механизм отслеживания изменений для коллекций в рамках единицы работы.
    /// </summary>
    /// <typeparam name="TV">Тип элементов коллекции.</typeparam>
    /// <typeparam name="T">Тип модели, к которой относится коллекция.</typeparam>
    /// <param name="propertyName">Имя отслеживаемого свойства.</param>
    /// <param name="currentValue">Текущее отслеживаемое состояние коллекции.</param>
    /// <param name="value">Новое значение коллекции.</param>
    /// <returns>Обновленный экземпляр TrackedCollection или null, если коллекция была удалена.</returns>
    protected TrackedCollection<TV, T>? TrackCollection<TV>(
        string propertyName,
        TrackedCollection<TV, T>? currentValue,
        List<TV>? value)
    {
        // Сценарий 1: Добавление новой коллекции
        if (currentValue == null && value != null)
        {
            // Фиксируем добавление новой коллекции в журнале изменений
            _changes[propertyName] = value;

            // Отмечаем свойство как полностью новое для последующей обработки
            AddedValueObjects.Add(propertyName);

            // Создаем новую отслеживаемую коллекцию
            return new TrackedCollection<TV, T>
            {
                Collection = value // Инициализируем коллекцию новыми значениями
            };
        }

        // Сценарий 2: Удаление существующей коллекции
        if (currentValue != null && value == null)
        {
            // Фиксируем удаление коллекции (записываем null)
            _changes[propertyName] = value;

            // Возвращаем null, указывая на удаление коллекции
            return null;
        }

        // Сценарий 3: Обновление существующей коллекции
        if (currentValue != null && value != null)
        {
            // Обновляем содержимое отслеживаемой коллекции
            currentValue.Collection = value;

            // Возвращаем обновленный экземпляр
            return currentValue;
        }

        // Сценарий 4: Нет изменений (оба значения null)
        return null;
    }

    /// <summary>
    /// Отслеживает изменения коллекции объектов-значений и возвращает новый или обновленный экземпляр TrackedObjectCollection.
    /// Специализированная версия для работы с наследниками UpdatedValueObject.
    /// </summary>
    /// <typeparam name="TV">Тип элементов коллекции (должен быть наследником UpdatedValueObject).</typeparam>
    /// <typeparam name="T">Тип модели, к которой относится коллекция.</typeparam>
    /// <param name="propertyName">Имя отслеживаемого свойства.</param>
    /// <param name="currentValue">Текущее отслеживаемое состояние коллекции.</param>
    /// <param name="value">Новое значение коллекции.</param>
    /// <returns>Обновленный экземпляр TrackedObjectCollection или null, если коллекция была удалена.</returns>
    protected TrackedValueObjectCollection<TV, T>? TrackValueObjectCollection<TV>(
        string propertyName,
        TrackedValueObjectCollection<TV, T>? currentValue,
        List<TV>? value) where TV : UpdatedValueObject<T>
    {
        // Сценарий 1: Добавление новой коллекции объектов-значений
        if (currentValue == null && value != null)
        {
            // Фиксируем добавление в журнал изменений
            _changes[propertyName] = value;

            // Отмечаем свойство как полностью новое
            AddedValueObjects.Add(propertyName);

            // Создаем новую отслеживаемую коллекцию объектов-значений
            return new TrackedValueObjectCollection<TV, T>
            {
                Collection = value // Инициализируем коллекцию новыми значениями
            };
        }

        // Сценарий 2: Удаление существующей коллекции
        if (currentValue != null && value == null)
        {
            // Фиксируем удаление (записываем null)
            _changes[propertyName] = value;

            // Возвращаем null, указывая на удаление
            return null;
        }

        // Сценарий 3: Обновление существующей коллекции
        if (currentValue != null && value != null)
        {
            // Обновляем содержимое отслеживаемой коллекции
            currentValue.Collection = value;

            // Возвращаем обновленный экземпляр
            return currentValue;
        }

        // Сценарий 4: Нет изменений (оба значения null)
        return null;
    }

    /// <summary>
    /// Очищает все изменения и сбрасывает состояние сущности до начального.
    /// Этот метод удаляет все отслеживаемые изменения в свойствах сущности
    /// и сбрасывает её состояние до значения по умолчанию (EntityState.Default).
    /// </summary>
    public virtual void ClearChanges()
    {
        // Сбрасываем состояние сущности до "Default".
        // Это означает, что сущность больше не считается измененной.
        _entityStateValue = EntityState.Default;

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
    /// Объединяет несколько определений обновления в одно.
    /// </summary>
    /// <typeparam name="T">Тип документа MongoDB.</typeparam>
    /// <param name="updates">Массив определений обновления. Если элемент равен null, он игнорируется.</param>
    /// <returns>Объединенное определение обновления или null, если все элементы массива равны null.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, если массив updates равен null.</exception>
    protected static UpdateDefinition<T> Combine(params UpdateDefinition<T>?[] updates)
    {
        // Фильтруем массив, удаляя все null-значения.
        var nonNullUpdates = updates.Where(u => u != null).ToArray();

        // Используем метод Combine из Builders<T>.Update для объединения всех определений обновления.
        return Builders<T>.Update.Combine(nonNullUpdates);
    }

    /// <summary>
    /// Объединяет состояние сущности с флагами изменений дочерних объектов.
    /// Метод проверяет, были ли изменены дочерние объекты, и обновляет состояние сущности
    /// на основе этих изменений. Если хотя бы один дочерний объект был изменен,
    /// состояние сущности устанавливается как "Modified".
    /// </summary>
    /// <param name="modified">
    /// Массив флагов изменений дочерних объектов. Каждый элемент массива указывает,
    /// был ли изменен соответствующий дочерний объект (true — изменен, false — не изменен, null — неизвестно).
    /// </param>
    /// <returns>Результирующее состояние сущности.</returns>
    protected EntityState Combine(params bool?[] modified)
    {
        // Если текущее состояние сущности уже не "Default", возвращаем его без изменений.
        // Это означает, что состояние сущности уже было изменено ранее, и дальнейшие проверки не требуются.
        if (_entityStateValue != EntityState.Default) return _entityStateValue;

        // Если ни один дочерний объект не был изменен, возвращаем текущее состояние сущности.
        // В данном случае это будет "Default", так как состояние не изменилось.
        if (!modified.Any(m => m.HasValue && m.Value)) return _entityStateValue;

        // Обновляем время последнего изменения сущности.
        // Это полезно для отслеживания, когда сущность была изменена в последний раз.
        LastModified = DateTime.UtcNow;

        // Добавляем или обновляем время последнего изменения в словаре изменений (_structChanges).
        // Это позволяет отслеживать изменения в типе-значении (например, DateTime).
        _structChanges[nameof(LastModified)] = LastModified;

        // Устанавливаем состояние сущности как "Modified".
        // Это указывает, что сущность была изменена и требует обновления в базе данных.
        _entityStateValue = EntityState.Modified;

        // Возвращаем состояние "Modified".
        return _entityStateValue;
    }
}