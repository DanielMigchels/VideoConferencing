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

        base.OnOpen += VideoConferencingWebSocketHandler_OnOpen;
        base.OnClose += VideoConferencingWebSocketHandler_OnClose;

        _roomService.OnRoomsListUpdated += OnRoomsListUpdatedHandler;
        _roomService.OnRoomUpdated += _roomService_OnRoomUpdated;
        _roomService.OnClientRoomLeft += _roomService_OnClientRoomLeft;
    }

    private void VideoConferencingWebSocketHandler_OnClose(object? sender, Guid socketId)
    {
        _roomService.LeaveRoom(socketId);
    }

    private async void VideoConferencingWebSocketHandler_OnOpen(object? sender, Guid socketId)
    {
        var message = new RoomListUpdated
        {
            Rooms = _roomService.Rooms
        };

        await SendMessage(socketId, message);
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
                case CreateRoom createRoom:
                    await CreateRoomAsync(socketId, createRoom);
                    break;
                case JoinRoom joinRoom:
                    await JoinRoomAsync(socketId, joinRoom);
                    break;
                case LeaveRoom leaveRoom:
                    await LeaveRoomAsync(socketId, leaveRoom);
                    break;
                case DeleteRoom deleteRoom:
                    await DeleteRoomAsync(socketId, deleteRoom);
                    break;
                case SendOffer sendOffer:
                    await SendOfferAsync(socketId, sendOffer);
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

    private async Task LeaveRoomAsync(Guid socketId, LeaveRoom leaveRoom)
    {
        if (leaveRoom is null)
        {
            _logger.LogWarning("LeaveRoom message was null for socket {SocketId}", socketId);
            return;
        }

        _roomService.LeaveRoom(socketId);

        await Task.CompletedTask;
    }

    private async Task JoinRoomAsync(Guid socketId, JoinRoom joinRoom)
    {
        if (joinRoom is null)
        {
            _logger.LogWarning("JoinRoom message was null for socket {SocketId}", socketId);
            return;
        }

        _roomService.JoinRoom(joinRoom.RoomId, socketId);

        await Task.CompletedTask;
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

    private async Task SendOfferAsync(Guid socketId, SendOffer sendOffer)
    {
        var answer = _roomService.HandleOffer(sendOffer.RoomId, socketId, sendOffer.Offer.ToString() ?? string.Empty);

        var message = new OfferProcessed
        {
            Sdp = answer.sdp,
            AnswerType = answer.type.ToString().ToLower()
        };

        _ = SendMessage(socketId, message);

        await Task.CompletedTask;
    }

    private async void OnRoomsListUpdatedHandler(object? sender, List<Data.Room> rooms)
    {
        _logger.LogInformation("Broadcasting rooms update to all clients. Total rooms: {RoomCount}", rooms.Count);
        
        var message = new RoomListUpdated
        {
            Rooms = rooms
        };

        await SendMessageToAllAsync(message);
    }

    private void _roomService_OnRoomUpdated(object? sender, (List<Guid> SocketIds, Data.Room Room) e)
    {
        var (socketIds, room) = e;

        _logger.LogInformation("Sending room update for Room {RoomId} to {SocketCount} clients", room.Id, socketIds.Count);
        var message = new RoomUpdated
        {
            Room = room
        };

        foreach (var socketId in socketIds)
        {
            _ = SendMessage(socketId, message);
        }
    }

    private void _roomService_OnClientRoomLeft(object? sender, Guid socketId)
    {
        var message = new RoomUpdated
        {
            Room = null
        };

        _ = SendMessage(socketId, message);
    }
}
