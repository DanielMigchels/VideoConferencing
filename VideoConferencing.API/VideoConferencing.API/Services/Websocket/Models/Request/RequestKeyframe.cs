using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Models.Request;

public class RequestKeyframe : WebsocketMessage
{
    [JsonPropertyName("roomId")]
    public Guid RoomId { get; set; }
}
