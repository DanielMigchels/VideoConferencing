using SIPSorcery.Net;

namespace VideoConferencing.API.Services.Room;

public interface IRoomService
{
    public event EventHandler<List<Data.Room>>? OnRoomsListUpdated;
    public event EventHandler<Data.Room>? OnRoomUpdated;
    public event EventHandler<Guid>? OnClientRoomLeft;
    public event EventHandler<(Guid SocketId, RTCSessionDescriptionInit Answer)> OnRenegotiation;

    public List<Data.Room> Rooms { get; }
    public Data.Room AddRoom();
    public void DeleteRoom(Guid RoomId);
    public void JoinRoom(Guid roomId, Guid socketId);
    public void LeaveRoom(Guid socketId);
    public void RequestKeyframes(Guid roomId, Guid socketId);
    public Task CreatePeerConnection(Guid roomId, Guid socketId, string offer);
}
