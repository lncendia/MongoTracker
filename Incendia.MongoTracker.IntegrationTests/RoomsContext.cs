using Microsoft.EntityFrameworkCore;

using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;

namespace Incendia.MongoTracker.IntegrationTests;

/// <summary>
/// EF Core context over the same room model: viewers are an owned collection (embedded array).
/// </summary>
public class RoomsContext(DbContextOptions<RoomsContext> options) : DbContext(options)
{
  public DbSet<Room> Rooms => Set<Room>();

  public static RoomsContext Create(IMongoClient client, string database) =>
    new(new DbContextOptionsBuilder<RoomsContext>().UseMongoDB(client, database).Options);

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Room>(room =>
    {
      room.ToCollection("Rooms");
      room.OwnsMany(r => r.Viewers);
    });
  }
}
