using SIPSorcery.Net;
using System.Text.Json;

namespace VideoConferencing.API.Services.Room;

public class RoomService : IRoomService
{
    private List<Data.Room> rooms = [];
    private readonly ILogger<RoomService> logger;

    public List<Data.Room> Rooms
    {
        get
        {
            return rooms;
        }
    }

    public event EventHandler<List<Data.Room>>? OnRoomsListUpdated;
    public event EventHandler<(List<Guid> SocketIds, Data.Room Room)>? OnRoomUpdated;
    public event EventHandler<Guid>? OnClientRoomLeft;

    public RoomService(ILogger<RoomService> logger)
    {
        AddRoom();
        this.logger = logger;
    }

    public Data.Room AddRoom()
    {
        var room = new Data.Room()
        {
            Id = Guid.NewGuid(),
        };

        rooms.Add(room);
        OnRoomsListUpdated?.Invoke(this, Rooms);
        return room;
    }

    public void DeleteRoom(Guid RoomId)
    {
        var room = rooms.FirstOrDefault(r => r.Id == RoomId);

        if (room == null)
        {
            return;
        }

        foreach (var participant in room.Participants)
        {
            OnClientRoomLeft?.Invoke(this, participant);
        }

        if (room != null)
        {
            rooms.Remove(room);
            OnRoomsListUpdated?.Invoke(this, Rooms);
        }
    }

    public void JoinRoom(Guid roomId, Guid socketId)
    {
        LeaveRoom(socketId);

        var room = rooms.FirstOrDefault(r => r.Id == roomId);
        if (room == null)
        {
            return;
        }

        if (room.Participants.Any(x => x == socketId))
        {
            return;
        }

        room.Participants.Add(socketId);
        OnRoomsListUpdated?.Invoke(this, Rooms);
        OnRoomUpdated?.Invoke(this, (room.Participants, room));
    }

    public void LeaveRoom(Guid socketId)
    {
        var rooms = Rooms.Where(x => x.Participants.Any(p => p == socketId)).ToList();

        if (!rooms.Any())
        {
            return;
        }

        foreach (var room in rooms)
        {
            room.Participants.Remove(socketId);
            OnRoomUpdated?.Invoke(this, (room.Participants, room));
        }

        OnRoomsListUpdated?.Invoke(this, Rooms);
        OnClientRoomLeft?.Invoke(this, socketId);
    }

    public RTCSessionDescriptionInit HandleOffer(Guid roomId, Guid socketId, string offerJson)
    {
        if (string.IsNullOrWhiteSpace(offerJson))
        {
            throw new Exception("OfferJson was empty");
        }

        var offer = ParseOfferJson(offerJson);

        var config = new RTCConfiguration
        {
            X_UseRtpFeedbackProfile = true
        };
        var pc = new RTCPeerConnection(config);

        var opusFormat = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 111, "opus", 48000, 2);
        var pcmuFormat = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 0, "PCMU", 8000);
        var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, new List<SDPAudioVideoMediaFormat> { opusFormat, pcmuFormat }, MediaStreamStatusEnum.SendRecv);
        pc.addTrack(audioTrack);

        var vp8Format = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000);
        var videoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { vp8Format }, MediaStreamStatusEnum.SendRecv);
        pc.addTrack(videoTrack);

        pc.onicecandidate += async (candidate) =>
        {
            logger.LogInformation($"Server ICE candidate: {candidate.candidate}");
        };

        pc.onsignalingstatechange += () =>
        {
            logger.LogInformation($"Signaling state: {pc.signalingState}");
        };

        pc.OnRtpPacketReceived += (remoteEP, mediaType, rtpPacket) =>
        {
            logger.LogInformation($"Received {mediaType} RTP packet, size: {rtpPacket.Payload.Length}");
        };

        pc.OnReceiveReport += (remoteEP, mediaType, rtcpReport) =>
        {
            logger.LogInformation($"Received RTCP report from type: {mediaType}");
        };

        pc.OnTimeout += (mediaType) =>
        {
            logger.LogWarning($"Timeout on for {mediaType}");
        };


        var result = pc.setRemoteDescription(offer);
        if (result != SIPSorcery.Net.SetDescriptionResultEnum.OK)
        {
            throw new Exception("Could not set remote description");
        }

        var answer = pc.createAnswer();
        pc.setLocalDescription(answer);

        return answer;
    }

    private static RTCSessionDescriptionInit ParseOfferJson(string offerJson)
    {
        using var offerDoc = JsonDocument.Parse(offerJson);
        var root = offerDoc.RootElement;

        var sdp = root.GetProperty("sdp").GetString();
        var typeStr = root.GetProperty("type").GetString();

        var sdpType = typeStr?.ToLower() == "offer" ? RTCSdpType.offer : RTCSdpType.answer;
        var offer = new RTCSessionDescriptionInit
        {
            type = sdpType,
            sdp = sdp
        };

        return offer;
    }
}
