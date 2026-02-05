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
    public event EventHandler<(IEnumerable<Guid> SocketIds, Data.Room Room)>? OnRoomUpdated;
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

        var participants = room.RoomParticipants.ToList();

        foreach (var participant in participants)
        {
            if (participant.PeerConnection != null)
            {
                // Unsubscribe using stored delegates
                if (participant.OnIceCandidateHandler != null)
                    participant.PeerConnection.onicecandidate -= participant.OnIceCandidateHandler;
                if (participant.OnSignalingStateChangeHandler != null)
                    participant.PeerConnection.onsignalingstatechange -= participant.OnSignalingStateChangeHandler;
                if (participant.OnRtpPacketReceivedHandler != null)
                    participant.PeerConnection.OnRtpPacketReceived -= participant.OnRtpPacketReceivedHandler;
                if (participant.OnReceiveReportHandler != null)
                    participant.PeerConnection.OnReceiveReport -= participant.OnReceiveReportHandler;
                if (participant.OnTimeoutHandler != null)
                    participant.PeerConnection.OnTimeout -= participant.OnTimeoutHandler;

                participant.PeerConnection.Close("Room left");
                participant.PeerConnection.Dispose();
                participant.PeerConnection = null;

                // Clear handlers
                participant.OnIceCandidateHandler = null;
                participant.OnSignalingStateChangeHandler = null;
                participant.OnRtpPacketReceivedHandler = null;
                participant.OnReceiveReportHandler = null;
                participant.OnTimeoutHandler = null;
            }

            room.RoomParticipants.Remove(participant);

            OnClientRoomLeft?.Invoke(this, participant.SocketId);
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

        room.RoomParticipants.Add(new()
        {
            SocketId = socketId,
            PeerConnection = null
        });
        OnRoomsListUpdated?.Invoke(this, Rooms);
        OnRoomUpdated?.Invoke(this, (room.Participants, room));
    }

    public void LeaveRoom(Guid socketId)
    {
        var rooms = Rooms.Where(x => x.Participants.Any(p => p == socketId)).ToList();

        if (rooms.Count == 0)
        {
            return;
        }

        foreach (var room in rooms)
        {
            var participants = room.RoomParticipants.Where(x => x.SocketId == socketId).ToList();

            foreach (var participant in participants)
            {
                if (participant.PeerConnection != null)
                {
                    if (participant.OnIceCandidateHandler != null)
                    {
                        participant.PeerConnection.onicecandidate -= participant.OnIceCandidateHandler;
                    }
                        
                    if (participant.OnSignalingStateChangeHandler != null)
                    {
                        participant.PeerConnection.onsignalingstatechange -= participant.OnSignalingStateChangeHandler;
                    }
                        
                    if (participant.OnRtpPacketReceivedHandler != null)
                    {
                        participant.PeerConnection.OnRtpPacketReceived -= participant.OnRtpPacketReceivedHandler;
                    }
                        
                    if (participant.OnReceiveReportHandler != null)
                    {
                        participant.PeerConnection.OnReceiveReport -= participant.OnReceiveReportHandler;
                    }
                        
                    if (participant.OnTimeoutHandler != null)
                    {
                        participant.PeerConnection.OnTimeout -= participant.OnTimeoutHandler;
                    }                       

                    participant.PeerConnection.Close("Room left");
                    participant.PeerConnection.Dispose();
                    participant.PeerConnection = null;

                    participant.OnIceCandidateHandler = null;
                    participant.OnSignalingStateChangeHandler = null;
                    participant.OnRtpPacketReceivedHandler = null;
                    participant.OnReceiveReportHandler = null;
                    participant.OnTimeoutHandler = null;
                }
                
                room.RoomParticipants.Remove(participant);
            }

            OnRoomUpdated?.Invoke(this, (room.Participants, room));
        }

        OnRoomsListUpdated?.Invoke(this, Rooms);
        OnClientRoomLeft?.Invoke(this, socketId);
    }

    public RTCSessionDescriptionInit HandleOffer(Guid roomId, Guid socketId, string offerJson)
    {
        var room = Rooms.Where(x => x.Id == roomId).FirstOrDefault();
        if (room == null)
        {
            throw new Exception("Room was not found.");
        }

        var participant = room.RoomParticipants.Where(x => x.SocketId == socketId).FirstOrDefault();
        if (participant == null)
        {
            throw new Exception("Participant was not found in this room.");
        }

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


        var result = pc.setRemoteDescription(offer);
        if (result != SIPSorcery.Net.SetDescriptionResultEnum.OK)
        {
            throw new Exception("Could not set remote description");
        }

        var answer = pc.createAnswer();
        pc.setLocalDescription(answer);

        participant.OnIceCandidateHandler = candidate => Pc_onicecandidate(candidate, roomId, socketId, pc);
        participant.OnSignalingStateChangeHandler = () => Pc_onsignalingstatechange(roomId, socketId, pc);
        participant.OnRtpPacketReceivedHandler = (remoteEP, mediaType, rtpPacket) => Pc_OnRtpPacketReceived(remoteEP, mediaType, rtpPacket, roomId, socketId, pc);
        participant.OnReceiveReportHandler = (remoteEP, mediaType, rtcpReport) => Pc_OnReceiveReport(remoteEP, mediaType, rtcpReport, roomId, socketId, pc);
        participant.OnTimeoutHandler = mediaType => Pc_OnTimeout(mediaType, roomId, socketId, pc);

        pc.onicecandidate += participant.OnIceCandidateHandler;
        pc.onsignalingstatechange += participant.OnSignalingStateChangeHandler;
        pc.OnRtpPacketReceived += participant.OnRtpPacketReceivedHandler;
        pc.OnReceiveReport += participant.OnReceiveReportHandler;
        pc.OnTimeout += participant.OnTimeoutHandler;

        participant.PeerConnection = pc;

        return answer;
    }

    private void Pc_OnTimeout(SDPMediaTypesEnum mediaType, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        logger.LogWarning($"Timeout on for {mediaType} | RoomId: {roomId} | SocketId: {socketId}");
    }

    private void Pc_OnReceiveReport(System.Net.IPEndPoint remoteEP, SDPMediaTypesEnum mediaType, RTCPCompoundPacket rtcpReport, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        logger.LogInformation($"Received RTCP report from type: {mediaType} | RoomId: {roomId} | SocketId: {socketId}");
    }

    private void Pc_OnRtpPacketReceived(System.Net.IPEndPoint remoteEP, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {       
        logger.LogDebug($"Received {mediaType} RTP packet, size: {rtpPacket.Payload.Length} | RoomId: {roomId} | SocketId: {socketId}");

        var room = rooms.Where(x => x.Id == roomId).FirstOrDefault();
        if (room == null)
        {
            return;
        }

        var targets = room.RoomParticipants.Where(x => x.SocketId != socketId);

        foreach (var target in targets)
        {
            try
            {
                if (target.PeerConnection == null)
                {
                    continue;
                }

                if (target.PeerConnection.connectionState == RTCPeerConnectionState.connected)
                {
                    target.PeerConnection.SendRtpRaw(mediaType, rtpPacket.Payload, rtpPacket.Header.Timestamp, rtpPacket.Header.MarkerBit, rtpPacket.Header.PayloadType);
                    logger.LogInformation("packet forwarded");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not forward message");
            }
        }
    }

    private void Pc_onsignalingstatechange(Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        logger.LogInformation($"Signaling state change for pc | RoomId: {roomId} | SocketId: {socketId}");
    }

    private void Pc_onicecandidate(RTCIceCandidate candidate, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        logger.LogInformation($"Server ICE candidate: {candidate.candidate} | RoomId: {roomId} | SocketId: {socketId}");
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
