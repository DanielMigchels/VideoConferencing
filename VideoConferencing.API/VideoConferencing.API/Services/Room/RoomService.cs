using SIPSorcery.Net;
using System.Text.Json;
using System.Collections.Concurrent;

namespace VideoConferencing.API.Services.Room;

public class RoomService : IRoomService
{
    private ConcurrentDictionary<Guid, Data.Room> rooms = new();
    private readonly ILogger<RoomService> logger;

    public List<Data.Room> Rooms
    {
        get
        {
            return [.. rooms.Values];
        }
    }

    public event EventHandler<List<Data.Room>>? OnRoomsListUpdated;
    public event EventHandler<Data.Room>? OnRoomUpdated;
    public event EventHandler<Guid>? OnClientRoomLeft;
    public event EventHandler<(Guid SocketId, RTCSessionDescriptionInit Answer)>? OnRenegotiation;

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

        rooms[room.Id] = room;
        OnRoomsListUpdated?.Invoke(this, Rooms);
        return room;
    }

    public void DeleteRoom(Guid RoomId)
    {
        if (!rooms.TryGetValue(RoomId, out var room))
        {
            return;
        }

        var participants = room.Participants.ToList();

        foreach (var participant in participants)
        {
            if (participant.PeerConnection != null)
            {
                participant.PeerConnection.onicecandidate -= participant.OnIceCandidateHandler;
                participant.PeerConnection.onsignalingstatechange -= participant.OnSignalingStateChangeHandler;
                participant.PeerConnection.OnRtpPacketReceived -= participant.OnRtpPacketReceivedHandler;
                participant.PeerConnection.OnReceiveReport -= participant.OnReceiveReportHandler;
                participant.PeerConnection.OnTimeout -= participant.OnTimeoutHandler;

                participant.PeerConnection.Close("Room left");
                participant.PeerConnection.Dispose();
                participant.PeerConnection = null;

                participant.OnIceCandidateHandler = null;
                participant.OnSignalingStateChangeHandler = null;
                participant.OnRtpPacketReceivedHandler = null;
                participant.OnReceiveReportHandler = null;
                participant.OnTimeoutHandler = null;
            }

            room.Participants.Remove(participant);

            OnClientRoomLeft?.Invoke(this, participant.SocketId);
        }

        rooms.TryRemove(RoomId, out _);
        OnRoomsListUpdated?.Invoke(this, Rooms);
    }

    public void JoinRoom(Guid roomId, Guid socketId)
    {
        LeaveRoom(socketId);

        if (!rooms.TryGetValue(roomId, out var room))
        {
            return;
        }

        if (room.Participants.Any(x => x.SocketId == socketId))
        {
            return;
        }

        room.Participants.Add(new()
        {
            SocketId = socketId,
            PeerConnection = null
        });
        OnRoomsListUpdated?.Invoke(this, Rooms);
        OnRoomUpdated?.Invoke(this, room);
    }

    public void LeaveRoom(Guid socketId)
    {
        var affectedRooms = rooms.Values.Where(x => x.Participants.Any(p => p.SocketId == socketId)).ToList();

        if (affectedRooms.Count == 0)
        {
            return;
        }

        foreach (var room in affectedRooms)
        {
            var participants = room.Participants.Where(x => x.SocketId == socketId).ToList();

            foreach (var participant in participants)
            {
                if (participant.PeerConnection != null)
                {
                    participant.PeerConnection.onicecandidate -= participant.OnIceCandidateHandler;
                    participant.PeerConnection.onsignalingstatechange -= participant.OnSignalingStateChangeHandler;
                    participant.PeerConnection.OnRtpPacketReceived -= participant.OnRtpPacketReceivedHandler;
                    participant.PeerConnection.OnReceiveReport -= participant.OnReceiveReportHandler;
                    participant.PeerConnection.OnTimeout -= participant.OnTimeoutHandler;

                    try
                    {
                        participant.PeerConnection.Close("Room left");
                    }
                    catch
                    {

                    }


                    participant.PeerConnection.Dispose();
                    participant.PeerConnection = null;

                    participant.OnIceCandidateHandler = null;
                    participant.OnSignalingStateChangeHandler = null;
                    participant.OnRtpPacketReceivedHandler = null;
                    participant.OnReceiveReportHandler = null;
                    participant.OnTimeoutHandler = null;
                }

                room.Participants.Remove(participant);
            }

            OnRoomUpdated?.Invoke(this, room);
        }

        OnRoomsListUpdated?.Invoke(this, Rooms);
        OnClientRoomLeft?.Invoke(this, socketId);
    }

    public async Task CreatePeerConnection(Guid roomId, Guid socketId, string offerJson)
    {
        if (!rooms.TryGetValue(roomId, out var room))
        {
            throw new Exception("Room was not found.");
        }

        var participant = room.Participants.FirstOrDefault(x => x.SocketId == socketId);
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

        participant.PeerConnection = new RTCPeerConnection(config);

        for (int i = 0; i < 5; i++)
        {
            var opusFormat = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 111, "opus", 48000, 2);
            var pcmuFormat = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 0, "PCMU", 8000);
            var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, new List<SDPAudioVideoMediaFormat> { opusFormat, pcmuFormat }, MediaStreamStatusEnum.SendRecv);
            participant.Tracks.Add(audioTrack);
            participant.PeerConnection.addTrack(audioTrack);

            var vp8Format = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000);
            var videoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { vp8Format }, MediaStreamStatusEnum.SendRecv);
            participant.Tracks.Add(videoTrack);
            participant.PeerConnection.addTrack(videoTrack);
        }

        var result = participant.PeerConnection.setRemoteDescription(offer);
        if (result != SIPSorcery.Net.SetDescriptionResultEnum.OK)
        {
            throw new Exception("Could not set remote description");
        }

        var answer = participant.PeerConnection.createAnswer();
        await participant.PeerConnection.setLocalDescription(answer);

        participant.OnIceCandidateHandler = candidate => Pc_onicecandidate(candidate, roomId, socketId, participant.PeerConnection);
        participant.OnSignalingStateChangeHandler = () => Pc_onsignalingstatechange(roomId, socketId, participant.PeerConnection);
        participant.OnRtpPacketReceivedHandler = (remoteEP, mediaType, rtpPacket) => Pc_OnRtpPacketReceived(remoteEP, mediaType, rtpPacket, roomId, socketId, participant.PeerConnection);
        participant.OnReceiveReportHandler = (remoteEP, mediaType, rtcpReport) => Pc_OnReceiveReport(remoteEP, mediaType, rtcpReport, roomId, socketId, participant.PeerConnection);
        participant.OnTimeoutHandler = mediaType => Pc_OnTimeout(mediaType, roomId, socketId, participant.PeerConnection);

        participant.PeerConnection.onicecandidate += participant.OnIceCandidateHandler;
        participant.PeerConnection.onsignalingstatechange += participant.OnSignalingStateChangeHandler;
        participant.PeerConnection.OnRtpPacketReceived += participant.OnRtpPacketReceivedHandler;
        participant.PeerConnection.OnReceiveReport += participant.OnReceiveReportHandler;
        participant.PeerConnection.OnTimeout += participant.OnTimeoutHandler;

        OnRenegotiation?.Invoke(this, (participant.SocketId, answer));

        foreach (var roomParticipant in room.Participants)
        {
            if (roomParticipant.PeerConnection == null)
            {
                continue;
            }
            
            var existingOffer = roomParticipant.PeerConnection.createOffer();
            await roomParticipant.PeerConnection.setLocalDescription(existingOffer);
            
            OnRenegotiation?.Invoke(this, (roomParticipant.SocketId, existingOffer));
        }            
    }

    private void Pc_OnTimeout(SDPMediaTypesEnum mediaType, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        // logger.LogWarning($"Timeout on for {mediaType} | RoomId: {roomId} | SocketId: {socketId}");
    }

    private void Pc_OnReceiveReport(System.Net.IPEndPoint remoteEP, SDPMediaTypesEnum mediaType, RTCPCompoundPacket rtcpReport, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        // logger.LogInformation($"Received RTCP report from type: {mediaType} | RoomId: {roomId} | SocketId: {socketId}");
    }

    private void Pc_OnRtpPacketReceived(System.Net.IPEndPoint remoteEP, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        if (!rooms.TryGetValue(roomId, out var room))
        {
            return;
        }

        var participants = room.Participants;
        if (participants == null || participants.Count() == 0)
        {
            return;
        }

        if (mediaType == SDPMediaTypesEnum.video)
        {
            participants.First(x => x.SocketId == socketId).Ssrc = rtpPacket.Header.SyncSource;
        }

        var payload = rtpPacket.Payload;
        var timestamp = rtpPacket.Header.Timestamp;
        var markerBit = rtpPacket.Header.MarkerBit;
        var payloadType = rtpPacket.Header.PayloadType;

        for (int i = 0; i < participants.Count(); i++)
        {
            var target = participants.ElementAt(i);

            if (target.SocketId == socketId)
            {
                continue;
            }

            var targetPc = target.PeerConnection;
            if (targetPc == null)
            {
                continue;
            }

            if (targetPc.connectionState != RTCPeerConnectionState.connected)
            {
                continue;
            }

            targetPc.SendRtpRaw(mediaType, payload, timestamp, markerBit, payloadType);
        }
    }

    private void Pc_onsignalingstatechange(Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        // logger.LogInformation($"Signaling state change for pc | RoomId: {roomId} | SocketId: {socketId}");
    }

    private void Pc_onicecandidate(RTCIceCandidate candidate, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        // logger.LogInformation($"Server ICE candidate: {candidate.candidate} | RoomId: {roomId} | SocketId: {socketId}");
    }

    private void RequestKeyframeFrom(Guid participantId)
    {
        if (!rooms.Values.Any(r => r.Participants.Any(p => p.SocketId == participantId && p.Ssrc != 0)))
        {
            return;
        }

        foreach (var room in rooms.Values)
        {
            var participant = room.Participants.FirstOrDefault(p => p.SocketId == participantId);
            if (participant?.PeerConnection != null && participant.PeerConnection.connectionState == RTCPeerConnectionState.connected)
            {
                try
                {
                    var pli = new RTCPFeedback(participant.Ssrc, participant.Ssrc, PSFBFeedbackTypesEnum.PLI);
                    participant.PeerConnection.SendRtcpFeedback(SDPMediaTypesEnum.video, pli);
                    logger.LogDebug($"Sent PLI keyframe request to participant {participantId} for SSRC {participant.Ssrc}");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, $"Failed to send PLI to participant {participantId}");
                }
                break;
            }
        }
    }

    public void RequestKeyframes(Guid roomId, Guid socketId)
    {
        logger.LogInformation($"Requesting keyframes for new participant | RoomId: {roomId} | SocketId: {socketId}");

        if (!rooms.TryGetValue(roomId, out var room))
        {
            return;
        }

        foreach (var participant in room.Participants)
        {
            if (participant.SocketId != socketId)
            {
                RequestKeyframeFrom(participant.SocketId);
            }
        }
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