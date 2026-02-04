using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Generic.Models;

namespace VideoConferencing.API.Services.Websocket.Models.Response;

public class OfferProcessed : WebsocketMessage
{
    [JsonPropertyName("sdp")]
    public required string Sdp { get; set; }
    public required string AnswerType { get; set; }
}
