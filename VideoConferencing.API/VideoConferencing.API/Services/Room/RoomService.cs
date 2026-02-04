using System.Net.Sockets;

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

    public event EventHandler<List<Data.Room>>? OnRoomsListUpdated;
    public event EventHandler<(List<Guid> SocketIds, Data.Room Room)>? OnRoomUpdated;
    public event EventHandler<Guid>? OnClientRoomLeft;

    public RoomService()
    {
        AddRoom();
        AddRoom();
        AddRoom();
    }

    public Data.Room AddRoom()
    {
        var room = new Data.Room()
        {
            Id = Guid.NewGuid(),
        };

        rooms.Add(room);
        OnRoomsListUpdated?.Invoke(this, Rooms);
        return room;
    }

    public void DeleteRoom(Guid RoomId)
    {
        var room = rooms.FirstOrDefault(r => r.Id == RoomId);

        foreach (var participant in room.Participants)
        {
            OnClientRoomLeft?.Invoke(this, participant);
        }

        if (room != null)
        {
            rooms.Remove(room);
            OnRoomsListUpdated?.Invoke(this, Rooms);
        }
    }

    public void JoinRoom(Guid roomId, Guid socketId)
    {
        LeaveRoom(socketId);

        var room = rooms.FirstOrDefault(r => r.Id == roomId);
        if (room == null)
        {
            return;
        }

        if (room.Participants.Any(x => x == socketId))
        {
            return;
        }

        room.Participants.Add(socketId);
        OnRoomsListUpdated?.Invoke(this, Rooms);
        OnRoomUpdated?.Invoke(this, (room.Participants, room));
    }

    public void LeaveRoom(Guid socketId)
    {
        var rooms = Rooms.Where(x => x.Participants.Any(p => p == socketId)).ToList();

        if (!rooms.Any())
        {
            return;
        }

        foreach (var room in rooms)
        {
            room.Participants.Remove(socketId);
            OnRoomUpdated?.Invoke(this, (room.Participants, room));
        }

        OnRoomsListUpdated?.Invoke(this, Rooms);
        OnClientRoomLeft?.Invoke(this, socketId);
    }
}
