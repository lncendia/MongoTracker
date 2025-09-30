using MongoDB.Driver;

namespace MongoTracker.Entities;

/// <inheritdoc/>
/// <summary>
/// Base abstract class for versioned entities that track the last modification timestamp.
/// Useful for implementing optimistic concurrency and version-aware updates.
/// Inherits from <see cref="UpdatedEntity{T}"/>.
/// </summary>
/// <typeparam name="T">Type of the entity inheriting from <see cref="VersionedUpdatedEntity{T}"/></typeparam>
public abstract class VersionedUpdatedEntity<T> : UpdatedEntity<T> where T : UpdatedEntity<T>
{
    /// <summary>
    /// Internal field storing the timestamp of the last modification.
    /// Initialized to <see cref="DateTime.UtcNow"/>.
    /// </summary>
    private DateTime _lastModified = DateTime.UtcNow;
    
    /// <summary>
    /// The timestamp of the entity's last modification.
    /// </summary>
    public DateTime LastModified {
        
        // Getter returns the value of the private _lastModified field
        get => _lastModified;
        
        // Setter tracks changes and updates the value
        private set => _lastModified = TrackStructChange(nameof(LastModified), _lastModified, value);
    }

    /// <inheritdoc/>
    /// <summary>
    /// Returns the <see cref="UpdateDefinition{T}"/> for this entity.
    /// Automatically updates <see cref="LastModified"/> to the current UTC timestamp.
    /// </summary>
    public override UpdateDefinition<T> UpdateDefinition
    {
        get
        {
            // Ensure that there are changes to apply; otherwise, throw an exception
            if (EntityState != EntityState.Modified) 
                throw new InvalidOperationException("Cannot create UpdateDefinition for an entity that is not modified.");
            
            // Update the LastModified timestamp to the current UTC time
            LastModified = DateTime.UtcNow;
            
            // Return the base update definition which includes all tracked changes
            return base.UpdateDefinition;
        }
    }
}
