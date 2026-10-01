namespace Incendia.MongoTracker.IntegrationTests;

/// <summary>
/// A watch room as stored by Overoom (avatar and notification settings of viewers omitted):
/// the document contains viewers, each viewer contains its tags.
/// </summary>
public class Room
{
  public Guid Id { get; set; }
  public Guid FilmId { get; set; }
  public Guid OwnerId { get; set; }
  public bool IsSerial { get; set; }
  public List<Viewer> Viewers { get; set; } = [];
}

public class Viewer
{
  public Guid Id { get; set; }
  public string UserName { get; set; } = null!;
  public bool Online { get; set; }
  public bool FullScreen { get; set; }
  public bool OnPause { get; set; }
  public double Speed { get; set; }
  public bool Muted { get; set; }
  public TimeSpan TimeLine { get; set; }
  public int? Season { get; set; }
  public int? Episode { get; set; }
  public List<string> Tags { get; set; } = [];
}

/// <summary>
/// Test data.
/// </summary>
public static class Rooms
{
  public static readonly Guid RoomId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

  private static readonly string[] Names = ["Egor", "Anton", "Max", "Kirill"];

  /// <summary>
  /// Identifier of the viewer number <paramref name="number"/> (1-based).
  /// </summary>
  public static Guid ViewerId(int number) => Guid.Parse($"00000000-0000-0000-0000-00000000000{number}");

  /// <summary>
  /// Creates a room watched by the first <paramref name="viewers"/> viewers, all online and playing.
  /// </summary>
  public static Room Create(int viewers) => new()
  {
    Id = RoomId,
    FilmId = Guid.Parse("00000000-0000-0000-0000-0000000000f1"),
    OwnerId = ViewerId(1),
    IsSerial = false,
    Viewers = Enumerable.Range(1, viewers).Select(n => new Viewer
    {
      Id = ViewerId(n),
      UserName = Names[n - 1],
      Online = true,
      Speed = 1,
      TimeLine = TimeSpan.FromMinutes(42)
    }).ToList()
  };
}
