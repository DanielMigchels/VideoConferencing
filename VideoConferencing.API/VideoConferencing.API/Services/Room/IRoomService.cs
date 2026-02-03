namespace VideoConferencing.API.Services.Room;

public interface IRoomService
{
    public event EventHandler<List<Data.Room>>? OnRoomsListUpdated;
    public event EventHandler<(List<Guid> SocketIds, Data.Room Room)>? OnRoomUpdated;
    public event EventHandler<Guid>? OnClientRoomLeft;
    public List<Data.Room> Rooms { get; }
    public Data.Room AddRoom();
    public void DeleteRoom(Guid RoomId);
    public void JoinRoom(Guid roomId, Guid socketId);
    public void LeaveRoom(Guid socketId);
}
