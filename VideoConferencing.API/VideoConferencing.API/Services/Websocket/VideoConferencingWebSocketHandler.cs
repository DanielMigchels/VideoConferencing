using VideoConferencing.API.Services.Websocket.Generic;
using VideoConferencing.API.Services.Websocket.Generic.Models;
using VideoConferencing.API.Services.Websocket.Models.Request;

namespace VideoConferencing.API.Services.Websocket;

public sealed class VideoConferencingWebSocketHandler(ILogger<VideoConferencingWebSocketHandler> logger) : WebsocketHandler
{
    public override async Task ProcessIncomingMessage(Guid socketId, WebsocketMessage message)
    {
        if (message is null)
        {
            logger.LogWarning("Null message received for socket {SocketId}", socketId);
            return;
        }

        try
        {
            switch (message)
            {
                case CreateRoom createRoom:
                    await CreateRoomAsync(socketId, createRoom);
                    break;
                case DeleteRoom deleteRoom:
                    await DeleteRoomAsync(socketId, deleteRoom);
                    break;
                default:
                    logger.LogCritical("Unknown message type received: {MessageType}", message.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message {MessageType} for socket {SocketId}", message.Type, socketId);
        }
    }

    private Task CreateRoomAsync(Guid socketId, CreateRoom message)
    {
        if (message is null)
        {
            logger.LogWarning("CreateRoom message was null for socket {SocketId}", socketId);
            return Task.CompletedTask;
        }



        return Task.CompletedTask;
    }

    private Task DeleteRoomAsync(Guid socketId, DeleteRoom message)
    {
        if (message is null)
        {
            logger.LogWarning("DeleteRoom message was null for socket {SocketId}", socketId);
            return Task.CompletedTask;
        }



        return Task.CompletedTask;
    }
}
