using System.Text.Json.Serialization;

namespace VideoConferencing.API.Data;

public class Room
{
    [JsonPropertyName("id")]
    public Guid Id { get; internal set; }
}
