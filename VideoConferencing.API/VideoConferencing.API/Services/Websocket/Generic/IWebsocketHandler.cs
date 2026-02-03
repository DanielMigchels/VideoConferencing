using System.Net.WebSockets;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Generic;

public interface IWebsocketHandler
{
    public Task HandleWebSocketAsync(WebSocket webSocket);
    public Task SendMessageToAllAsync(WebsocketMessage message);
    public abstract Task ProcessIncomingMessage(Guid socketId, WebsocketMessage message);
}
