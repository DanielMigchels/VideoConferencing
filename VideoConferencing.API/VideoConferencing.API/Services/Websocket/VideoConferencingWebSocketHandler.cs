using VideoConferencing.API.Data;
using VideoConferencing.API.Services.Room;
using VideoConferencing.API.Services.Websocket.Generic;
using VideoConferencing.API.Services.Websocket.Generic.Models;
using VideoConferencing.API.Services.Websocket.Models.Request;
using VideoConferencing.API.Services.Websocket.Models.Response;

namespace VideoConferencing.API.Services.Websocket;

public sealed class VideoConferencingWebSocketHandler : WebsocketHandler
{
    private readonly ILogger<VideoConferencingWebSocketHandler> _logger;
    private readonly IRoomService _roomService;

    public VideoConferencingWebSocketHandler(
        ILogger<VideoConferencingWebSocketHandler> logger,
        IRoomService roomService)
    {
        _logger = logger;
        _roomService = roomService;

        _roomService.OnRoomsUpdated += OnRoomsUpdatedHandler;
    }

    public override async Task ProcessIncomingMessage(Guid socketId, WebsocketMessage message)
    {
        if (message is null)
        {
            _logger.LogWarning("Null message received for socket {SocketId}", socketId);
            return;
        }

        try
        {
            switch (message)
            {
                case GetRooms getRooms:
                    await GetRoomsAsync(socketId, getRooms);
                    break;
                case CreateRoom createRoom:
                    await CreateRoomAsync(socketId, createRoom);
                    break;
                case DeleteRoom deleteRoom:
                    await DeleteRoomAsync(socketId, deleteRoom);
                    break;
                default:
                    _logger.LogWarning("Unknown message type received for socket {SocketId}", socketId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for socket {SocketId}", socketId);
        }
    }

    private async Task GetRoomsAsync(Guid socketId, GetRooms getRooms)
    {
        var message = new RoomsUpdated
        {
            Rooms = _roomService.Rooms
        };

        await SendMessage(socketId, message);
    }

    private async Task CreateRoomAsync(Guid socketId, CreateRoom message)
    {
        if (message is null)
        {
            _logger.LogWarning("CreateRoom message was null for socket {SocketId}", socketId);
            return;
        }

        var room = _roomService.AddRoom();

        await Task.CompletedTask;
    }

    private async Task DeleteRoomAsync(Guid socketId, DeleteRoom message)
    {
        if (message is null)
        {
            _logger.LogWarning("DeleteRoom message was null for socket {SocketId}", socketId);
            return;
        }

        _roomService.DeleteRoom(message.RoomId);

        await Task.CompletedTask;
    }

    private async void OnRoomsUpdatedHandler(object? sender, List<Data.Room> rooms)
    {
        _logger.LogInformation("Broadcasting rooms update to all clients. Total rooms: {RoomCount}", rooms.Count);
        
        var message = new RoomsUpdated
        {
            Rooms = rooms
        };

        await SendMessageToAllAsync(message);
    }
}
