using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Models.Response;

public sealed class RoomsUpdated : WebsocketMessage
{
    [JsonPropertyName("rooms")]
    public required List<Data.Room> Rooms { get; set; }
}
