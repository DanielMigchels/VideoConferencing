using SIPSorcery.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

namespace VideoConferencing.API.Data;

public class Room
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }


    [JsonPropertyName("participantCount")]
    public int ParticipantCount
    {
        get => Participants.Count();
    }

    [JsonPropertyName("participants")]
    public List<Participant> Participants { get; set; } = [];
}

public class Participant
{
    [JsonPropertyName("socketId")]
    public Guid SocketId { get; set; }

    [JsonIgnore]
    public List<MediaStreamTrack> Tracks { get; set; } = [];

    [JsonIgnore]
    public RTCPeerConnection? PeerConnection { get; set; }

    [JsonIgnore]
    public uint Ssrc { get; set; }

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