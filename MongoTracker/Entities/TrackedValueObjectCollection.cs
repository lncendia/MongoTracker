using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <summary>
/// Класс, представляющий отслеживаемую коллекцию объектов-значений (value objects), которые могут быть изменены и отслеживают свои изменения.
/// </summary>
/// <typeparam name="TC">
/// Тип объектов-значений в коллекции. Для корректной работы коллекции рекомендуется,
/// чтобы тип <typeparamref name="TC"/> переопределял метод <see cref="object.Equals(object?)"/>.
/// Это позволит корректно сравнивать объекты-значения и уменьшить количество лишних перезаписей.
/// </typeparam>
/// <typeparam name="TP">Тип родительской сущности, к которой относится коллекция.</typeparam>
public class TrackedValueObjectCollection<TC, TP> where TC : UpdatedValueObject<TP> where TP : UpdatedEntity<TP>
{
    /// <summary>
    /// Исходная (оригинальная) коллекция объектов-значений, хранящая состояние до изменений.
    /// Используется для сравнения с текущей коллекцией для определения изменений.
    /// </summary>
    private List<TC> _originalCollection = [];

    /// <summary>
    /// Текущая коллекция, которая может быть изменена.
    /// </summary>
    public List<TC> Collection { get; set; } = [];

    /// <summary>
    /// Флаг, указывающий, была ли коллекция изменена.
    /// </summary>
    public bool IsModified
    {
        get
        {
            // Проверяем, есть ли элементы в текущей коллекции, которых нет в исходной.
            if (Collection.Except(_originalCollection).Any()) return true;

            // Проверяем, есть ли элементы в исходной коллекции, которых нет в текущей.
            if (_originalCollection.Except(Collection).Any()) return true;

            // Проверяем, были ли изменены элементы, которые есть в обеих коллекциях.
            if (_originalCollection.Intersect(Collection).Any(e => e.IsModified)) return true;

            // Если изменений нет, возвращаем false.
            return false;
        }
    }

    /// <summary>
    /// Очищает все изменения в коллекции и сбрасывает состояние объектов-значений.
    /// </summary>
    public void ClearChanges()
    {
        // Очищаем изменения для всех объектов-значений, которые есть в обеих коллекциях.
        foreach (var updatedValueObject in Collection) updatedValueObject.ClearChanges();

        // Копирование текущей коллекции в оригинальную с использованием spread-оператора
        _originalCollection = [..Collection];
    }

    /// <summary>
    /// Возвращает определение обновления для MongoDB на основе изменений в коллекции.
    /// Метод анализирует различия между текущей коллекцией и исходной (оригинальной) коллекцией,
    /// а также проверяет, были ли изменены элементы, которые присутствуют в обеих коллекциях.
    /// </summary>
    /// <param name="parentPropertyName">Имя родительского свойства (для вложенных документов)</param>
    /// <param name="propertyName">Имя обновляемого свойства коллекции</param>
    /// <param name="blockedParentPropertyNames">Список заблокированных свойств, которые нельзя обновлять частично</param>
    /// <returns>
    /// Определение обновления для MongoDB. Возвращает <c>null</c>, если изменений нет.
    /// </returns>
    public UpdateDefinition<TP>? GetUpdateDefinition(
        string? parentPropertyName,
        string propertyName,
        IReadOnlyCollection<string> blockedParentPropertyNames)
    {
        // Если имя родительского свойства содержится в blockedParentPropertyNames,
        // возвращаем null, так как это свойство не должно быть обновлено, а этот объект будет записан целиком.
        if (blockedParentPropertyNames.Contains(propertyName)) return null;

        // Формирование полного имени свойства (включая родительские свойства)
        var collectionFullName = Combine(parentPropertyName, propertyName);

        // Создаем builder для построения определения обновления.
        var updateBuilder = Builders<TP>.Update;

        // Проверяем, есть ли элементы в текущей коллекции, которых нет в исходной.
        // Это означает, что в коллекцию были добавлены новые элементы.
        var someAdded = Collection.Except(_originalCollection).Any();

        // Проверяем, есть ли элементы в исходной коллекции, которых нет в текущей.
        // Это означает, что из коллекции были удалены элементы.
        var someRemoved = _originalCollection.Except(Collection).Any();

        // Проверяем, есть ли элементы, которые присутствуют в обеих коллекциях и были изменены.
        // Это полезно для случаев, когда элементы коллекции сами по себе могут изменяться.
        var someModified = _originalCollection.Intersect(Collection).Any(e => e.IsModified);

        // Если есть только добавленные элементы и нет удаленных или измененных.
        if (someAdded && !someRemoved && !someModified)
        {
            // Определяем элементы, которые были добавлены в текущую коллекцию.
            var addedItems = Collection.Except(_originalCollection);

            // Создаем операцию PushEach для добавления новых элементов в коллекцию.
            return updateBuilder.PushEach(collectionFullName, addedItems);
        }

        // Если есть только удаленные элементы и нет добавленных или измененных.
        if (someRemoved && !someAdded && !someModified)
        {
            // Определяем элементы, которые были удалены из текущей коллекции.
            var removedItems = _originalCollection.Except(Collection).ToArray();

            // Создаем операцию PullAll для удаления элементов из коллекции.
            return updateBuilder.PullAll(collectionFullName, removedItems);
        }

        // Если есть только измененные элементы и нет добавленных или удаленных.
        if (someModified && !someAdded && !someRemoved)
        {
            // Определяем элементы, которые есть в обеих коллекциях и были изменены.
            var updatedItems = _originalCollection.Intersect(Collection)

                // Фильтруем только измененные элементы.
                .Where(e => e.IsModified)

                // Получаем определение обновления для каждого измененного элемента.
                .Select((item, index) => item.GetUpdateDefinition(collectionFullName, index.ToString(), []));

            // Объединяем все определения обновления для измененных элементов в одно.
            return updateBuilder.Combine(updatedItems);
        }

        // Если есть как добавленные, так и удаленные элементы, или измененные элементы.
        if (someAdded || someRemoved || someModified)
        {
            // В этом случае проще всего полностью заменить коллекцию новым значением.
            return updateBuilder.Set(collectionFullName, Collection);
        }

        // Если изменений в коллекции нет, возвращаем null.
        return null;
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
    private static string Combine(string? parentPropertyName, string propertyName)
    {
        // Если имя родительского свойства отсутствует, возвращаем только имя текущего свойства.
        if (parentPropertyName == null) return propertyName;

        // Возвращаем объединенный путь, разделяя имена свойств точкой.
        return $"{parentPropertyName}.{propertyName}";
    }
}