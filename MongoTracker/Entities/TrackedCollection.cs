using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <summary>
/// Класс, представляющий отслеживаемую коллекцию, которая может быть изменена и отслеживает свои изменения.
/// </summary>
/// <typeparam name="TC">Тип элементов коллекции.</typeparam>
/// <typeparam name="TP">Тип родительской сущности, к которой относится коллекция.</typeparam>
/// <param name="originalCollection">Исходная коллекция, с которой сравниваются изменения.</param>
/// <param name="parentPropertyName">Имя свойства родительской сущности, в котором хранится коллекция.</param>
public class TrackedCollection<TC, TP>(List<TC> originalCollection, string parentPropertyName)
{
    /// <summary>
    /// Текущая коллекция, которая может быть изменена.
    /// </summary>
    public List<TC> Collection { get; set; } = [..originalCollection]; // Инициализация текущей коллекции копией исходной.

    /// <summary>
    /// Флаг, указывающий, была ли коллекция изменена.
    /// </summary>
    public bool IsModified
    {
        get
        {
            // Проверяем, есть ли элементы в текущей коллекции, которых нет в исходной.
            if (Collection.Except(originalCollection).Any()) return true;

            // Проверяем, есть ли элементы в исходной коллекции, которых нет в текущей.
            if (originalCollection.Except(Collection).Any()) return true;

            // Если различий нет, коллекция не изменена.
            return false;
        }
    }

    /// <summary>
    /// Возвращает определение обновления для MongoDB на основе изменений в коллекции.
    /// Метод анализирует различия между текущей коллекцией и исходной (оригинальной) коллекцией
    /// и возвращает соответствующее определение обновления для MongoDB.
    /// </summary>
    /// <returns>
    /// Определение обновления для MongoDB. Возвращает <c>null</c>, если изменений нет.
    /// </returns>
    public UpdateDefinition<TP>? GetUpdateDefinition()
    {
        // Создаем builder для построения определения обновления.
        var updateBuilder = Builders<TP>.Update;

        // Проверяем, есть ли элементы в текущей коллекции, которых нет в исходной.
        // Это означает, что в коллекцию были добавлены новые элементы.
        var someAdded = Collection.Except(originalCollection).Any();

        // Проверяем, есть ли элементы в исходной коллекции, которых нет в текущей.
        // Это означает, что из коллекции были удалены элементы.
        var someRemoved = originalCollection.Except(Collection).Any();

        // Если есть добавленные элементы и нет удаленных.
        if (someAdded && !someRemoved)
        {
            // Определяем элементы, которые были добавлены в текущую коллекцию.
            var addedItems = Collection.Except(originalCollection);

            // Возвращаем операцию PushEach для добавления новых элементов в коллекцию.
            return updateBuilder.PushEach(parentPropertyName, addedItems);
        }

        // Если есть удаленные элементы и нет добавленных.
        if (someRemoved && !someAdded)
        {
            // Определяем элементы, которые были удалены из текущей коллекции.
            var removedItems = originalCollection.Except(Collection).ToArray();

            // Возвращаем операцию PullAll для удаления элементов из коллекции.
            return updateBuilder.PullAll(parentPropertyName, removedItems);
        }

        // Если есть как добавленные, так и удаленные элементы.
        if (someAdded && someRemoved)
        {
            // В этом случае проще всего полностью заменить коллекцию новым значением.
            return updateBuilder.Set(parentPropertyName, Collection);
        }

        // Если изменений нет, возвращаем null.
        return null;
    }
}