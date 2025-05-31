using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <summary>
/// Класс, представляющий отслеживаемую коллекцию, которая может быть изменена и отслеживает свои изменения.
/// </summary>
/// <typeparam name="TC">Тип элементов коллекции.</typeparam>
/// <typeparam name="TP">Тип родительской сущности, к которой относится коллекция.</typeparam>
public class TrackedCollection<TC, TP>
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

            // Если различий нет, коллекция не изменена.
            return false;
        }
    }
    
    /// <summary>
    /// Очищает все изменения в коллекции и сбрасывает состояние объектов-значений.
    /// </summary>
    public void ClearChanges()
    {
        // Копирование текущей коллекции в оригинальную с использованием spread-оператора
        _originalCollection = [..Collection];
    }

    /// <summary>
    /// Возвращает определение обновления для MongoDB на основе изменений в коллекции.
    /// Метод анализирует различия между текущей коллекцией и исходной (оригинальной) коллекцией
    /// и возвращает соответствующее определение обновления для MongoDB.
    /// </summary>
    /// <param name="parentPropertyName">Имя родительского свойства (для вложенных документов)</param>
    /// <param name="propertyName">Имя обновляемого свойства коллекции</param>
    /// <param name="blockedParentPropertyNames">Список заблокированных свойств, которые нельзя обновлять частично</param>
    /// <returns>
    /// Определение обновления для MongoDB. Возвращает <c>null</c>, если изменений нет.
    /// </returns>
    public UpdateDefinition<TP>? GetUpdateDefinition(string? parentPropertyName, string propertyName,
        IEnumerable<string> blockedParentPropertyNames)
    {
        // Если имя родительского свойства содержится в blockedParentPropertyNames,
        // возвращаем null, так как это свойство не должно быть обновлено, а этот объект будет записан целиком.
        if (blockedParentPropertyNames.Contains(propertyName)) return null;
        
        //
        var collectionFullName = Combine(parentPropertyName, propertyName);
        
        // Создаем builder для построения определения обновления.
        var updateBuilder = Builders<TP>.Update;

        // Проверяем, есть ли элементы в текущей коллекции, которых нет в исходной.
        // Это означает, что в коллекцию были добавлены новые элементы.
        var someAdded = Collection.Except(_originalCollection).Any();

        // Проверяем, есть ли элементы в исходной коллекции, которых нет в текущей.
        // Это означает, что из коллекции были удалены элементы.
        var someRemoved = _originalCollection.Except(Collection).Any();

        // Если есть добавленные элементы и нет удаленных.
        if (someAdded && !someRemoved)
        {
            // Определяем элементы, которые были добавлены в текущую коллекцию.
            var addedItems = Collection.Except(_originalCollection);

            // Возвращаем операцию PushEach для добавления новых элементов в коллекцию.
            return updateBuilder.PushEach(collectionFullName, addedItems);
        }

        // Если есть удаленные элементы и нет добавленных.
        if (someRemoved && !someAdded)
        {
            // Определяем элементы, которые были удалены из текущей коллекции.
            var removedItems = _originalCollection.Except(Collection).ToArray();

            // Возвращаем операцию PullAll для удаления элементов из коллекции.
            return updateBuilder.PullAll(collectionFullName, removedItems);
        }

        // Если есть как добавленные, так и удаленные элементы.
        if (someAdded && someRemoved)
        {
            // В этом случае проще всего полностью заменить коллекцию новым значением.
            return updateBuilder.Set(collectionFullName, Collection);
        }

        // Если изменений нет, возвращаем null.
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