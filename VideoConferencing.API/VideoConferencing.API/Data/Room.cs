using System.Text.Json.Serialization;

namespace VideoConferencing.API.Data;

public class Room
{
    [JsonPropertyName("id")]
    public Guid Id { get; internal set; }

    [JsonPropertyName("participantCount")]
    public int ParticipantCount
    {
        get => Participants.Count;
    }

    [JsonPropertyName("participants")]
    public List<Guid> Participants { get; set; } = [];
}
