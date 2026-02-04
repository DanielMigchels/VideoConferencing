using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Models.Request;

public class SendOffer : WebsocketMessage
{
    [JsonPropertyName("offer")]
    public required object Offer { get; set; }

    [JsonPropertyName("roomId")]
    public Guid RoomId { get; set; }
}
