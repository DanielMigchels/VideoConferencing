using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Models.Request;

public sealed class DeleteRoom : WebsocketMessage
{
    [JsonPropertyName("roomId")]
    public required Guid RoomId { get; set; }
}
