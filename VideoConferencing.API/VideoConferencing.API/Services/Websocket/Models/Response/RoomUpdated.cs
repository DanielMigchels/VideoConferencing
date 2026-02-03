using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Models.Response;

public class RoomUpdated : WebsocketMessage
{
    [JsonPropertyName("room")]
    public required Data.Room? Room { get; set; }
}
