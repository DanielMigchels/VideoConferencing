using SIPSorcery.Net;
using System.Text.Json.Serialization;

namespace VideoConferencing.API.Data;

public class Room
{
    [JsonPropertyName("id")]
    public Guid Id { get; internal set; }

    [JsonPropertyName("participantCount")]
    public int ParticipantCount
    {
        get => Participants.Count();
    }

    [JsonPropertyName("participants")]
    public IEnumerable<Guid> Participants
    {
        get
        {
            return RoomParticipants.Select(x => x.SocketId);
        }
    }

    [JsonIgnore]
    public List<RoomParticipant> RoomParticipants { get; set; } = [];
}

public class RoomParticipant
{
    public Guid SocketId { get; set; }
    public RTCPeerConnection? PeerConnection { get; set; }

    [JsonIgnore]
    public Action<RTCIceCandidate>? OnIceCandidateHandler { get; set; }
    [JsonIgnore]
    public Action? OnSignalingStateChangeHandler { get; set; }
    [JsonIgnore]
    public Action<System.Net.IPEndPoint, SDPMediaTypesEnum, RTPPacket>? OnRtpPacketReceivedHandler { get; set; }
    [JsonIgnore]
    public Action<System.Net.IPEndPoint, SDPMediaTypesEnum, RTCPCompoundPacket>? OnReceiveReportHandler { get; set; }
    [JsonIgnore]
    public Action<SDPMediaTypesEnum>? OnTimeoutHandler { get; set; }
}