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
/// <param name="originalCollection">Исходная коллекция объектов-значений, с которой сравниваются изменения.</param>
/// <param name="parentPropertyName">Имя свойства родительской сущности, в котором хранится коллекция.</param>
public class TrackedValueObjectCollection<TC, TP>(List<TC> originalCollection, string parentPropertyName)
    where TC : UpdatedValueObject<TP> where TP : UpdatedEntity<TP>
{
    /// <summary>
    /// Текущая коллекция объектов-значений, которая может быть изменена.
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

            // Проверяем, были ли изменены элементы, которые есть в обеих коллекциях.
            if (originalCollection.Intersect(Collection).Any(e => e.IsModified)) return true;

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
        foreach (var updatedValueObject in originalCollection.Intersect(Collection)) updatedValueObject.ClearChanges();
    }

    /// <summary>
    /// Возвращает определение обновления для MongoDB на основе изменений в коллекции.
    /// Метод анализирует различия между текущей коллекцией и исходной (оригинальной) коллекцией,
    /// а также проверяет, были ли изменены элементы, которые присутствуют в обеих коллекциях.
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

        // Проверяем, есть ли элементы, которые присутствуют в обеих коллекциях и были изменены.
        // Это полезно для случаев, когда элементы коллекции сами по себе могут изменяться.
        var someModified = originalCollection.Intersect(Collection).Any(e => e.IsModified);

        // Если есть только добавленные элементы и нет удаленных или измененных.
        if (someAdded && !someRemoved && !someModified)
        {
            // Определяем элементы, которые были добавлены в текущую коллекцию.
            var addedItems = Collection.Except(originalCollection);

            // Создаем операцию PushEach для добавления новых элементов в коллекцию.
            return updateBuilder.PushEach(parentPropertyName, addedItems);
        }

        // Если есть только удаленные элементы и нет добавленных или измененных.
        if (someRemoved && !someAdded && !someModified)
        {
            // Определяем элементы, которые были удалены из текущей коллекции.
            var removedItems = originalCollection.Except(Collection).ToArray();

            // Создаем операцию PullAll для удаления элементов из коллекции.
            return updateBuilder.PullAll(parentPropertyName, removedItems);
        }

        // Если есть только измененные элементы и нет добавленных или удаленных.
        if (someModified && !someAdded && !someRemoved)
        {
            // Определяем элементы, которые есть в обеих коллекциях и были изменены.
            var updatedItems = originalCollection.Intersect(Collection)

                // Фильтруем только измененные элементы.
                .Where(e => e.IsModified)

                // Получаем определение обновления для каждого измененного элемента.
                .Select((item, index) => item.GetUpdateDefinition(parentPropertyName, index.ToString(), []));

            // Объединяем все определения обновления для измененных элементов в одно.
            return updateBuilder.Combine(updatedItems);
        }

        // Если есть как добавленные, так и удаленные элементы, или измененные элементы.
        if (someAdded || someRemoved || someModified)
        {
            // В этом случае проще всего полностью заменить коллекцию новым значением.
            return updateBuilder.Set(parentPropertyName, Collection);
        }

        // Если изменений в коллекции нет, возвращаем null.
        return null;
    }
}