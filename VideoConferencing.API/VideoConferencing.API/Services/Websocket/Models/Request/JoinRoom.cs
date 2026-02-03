using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Models.Request;

public class JoinRoom : WebsocketMessage
{
    [JsonPropertyName("roomId")]
    public required Guid RoomId { get; set; }
}
