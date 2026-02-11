using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Generic;

public abstract class WebsocketHandler : IWebsocketHandler
{
    public event EventHandler<Guid>? OnClose;
    public event EventHandler<Guid>? OnOpen;
    private readonly ConcurrentDictionary<Guid, WebSocket> sockets = new();

    public async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        var socketId = Guid.NewGuid();
        sockets.TryAdd(socketId, webSocket);

        var buffer = new byte[1024 * 128];

        OnOpen?.Invoke(this, socketId);

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
                catch (OperationCanceledException) { break; }
                catch (WebSocketException) { break; }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var message = JsonSerializer.Deserialize<WebsocketMessage>(json);
                    if (message != null)
                    {
                        await ProcessIncomingMessage(socketId, message);
                    }
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        finally
        {
            OnClose?.Invoke(this, socketId);
            sockets.TryRemove(socketId, out _);

            try
            {
                if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                }
            }
            catch (WebSocketException) { }
            catch (ObjectDisposedException) { }
        }
    }

    public async Task SendMessageToAllAsync(WebsocketMessage message)
    {
        var json = JsonSerializer.Serialize(message);

        var bytes = Encoding.UTF8.GetBytes(json);
        var tasks = sockets.Values
            .Where(socket => socket.State == WebSocketState.Open)
            .Select(socket => socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None));

        await Task.WhenAll(tasks);
    }

    public async Task SendMessage(Guid socketId, WebsocketMessage message)
    {
        if (sockets.TryGetValue(socketId, out var socket) && socket.State == WebSocketState.Open)
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    public abstract Task ProcessIncomingMessage(Guid socketId, WebsocketMessage message);
}
