namespace VideoConferencing.API.Services.Room;

public class RoomService : IRoomService
{
    private List<Data.Room> rooms = [];

    public List<Data.Room> Rooms 
    { 
        get 
        { 
            return rooms; 
        }
    }

    public event EventHandler<List<Data.Room>>? OnRoomsUpdated;

    public Data.Room AddRoom()
    {
        var room = new Data.Room()
        {
            Id = Guid.NewGuid(),
        };

        rooms.Add(room);
        OnRoomsUpdated?.Invoke(this, rooms);
        return room;
    }

    public void DeleteRoom(Guid RoomId)
    {
        var room = rooms.FirstOrDefault(r => r.Id == RoomId);
        if (room != null)
        {
            rooms.Remove(room);
            OnRoomsUpdated?.Invoke(this, rooms);
        }
    }
}
