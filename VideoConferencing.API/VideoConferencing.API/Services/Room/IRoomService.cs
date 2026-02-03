namespace VideoConferencing.API.Services.Room;

public interface IRoomService
{
    public event EventHandler<List<Data.Room>>? OnRoomsUpdated;
    public List<Data.Room> Rooms { get; }
    public Data.Room AddRoom();
    public void DeleteRoom(Guid RoomId);
}
