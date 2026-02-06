using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Models.Request;

public class CreatePeerConnection : WebsocketMessage
{
    [JsonPropertyName("sessionDescription")]
    public required object SessionDescription { get; set; }

    [JsonPropertyName("roomId")]
    public Guid RoomId { get; set; }
}
