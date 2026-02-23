using SIPSorcery.Net;
using SIPSorcery.Sys;
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
            return rooms.Values.ToList();
        }
    }

    public event EventHandler<List<Data.Room>>? OnRoomsListUpdated;
    public event EventHandler<Data.Room>? OnRoomUpdated;
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
            participant.KeyframeTimer?.Dispose();
            participant.KeyframeTimer = null;

            if (participant.PeerConnection != null)
            {
                participant.PeerConnection.onicecandidate -= participant.OnIceCandidateHandler;
                participant.PeerConnection.onsignalingstatechange -= participant.OnSignalingStateChangeHandler;
                participant.PeerConnection.onconnectionstatechange -= participant.OnConnectionStateChangeHandler;
                participant.PeerConnection.OnRtpPacketReceived -= participant.OnRtpPacketReceivedHandler;
                participant.PeerConnection.OnReceiveReport -= participant.OnReceiveReportHandler;
                participant.PeerConnection.OnTimeout -= participant.OnTimeoutHandler;

                participant.PeerConnection.Close("Room left");
                participant.PeerConnection.Dispose();
                participant.PeerConnection = null;

                participant.OnIceCandidateHandler = null;
                participant.OnSignalingStateChangeHandler = null;
                participant.OnConnectionStateChangeHandler = null;
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
        if (!rooms.TryGetValue(roomId, out var room))
        {
            return;
        }

        if(room.Participants.Count >= 2)
        {
            return;
        }

        LeaveRoom(socketId);

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
                participant.KeyframeTimer?.Dispose();
                participant.KeyframeTimer = null;

                if (participant.PeerConnection != null)
                {
                    participant.PeerConnection.onicecandidate -= participant.OnIceCandidateHandler;
                    participant.PeerConnection.onsignalingstatechange -= participant.OnSignalingStateChangeHandler;
                    participant.PeerConnection.onconnectionstatechange -= participant.OnConnectionStateChangeHandler;
                    participant.PeerConnection.OnRtpPacketReceived -= participant.OnRtpPacketReceivedHandler;
                    participant.PeerConnection.OnReceiveReport -= participant.OnReceiveReportHandler;
                    participant.PeerConnection.OnTimeout -= participant.OnTimeoutHandler;

                    participant.PeerConnection.Close("Room left");
                    participant.PeerConnection.Dispose();
                    participant.PeerConnection = null;

                    participant.OnIceCandidateHandler = null;
                    participant.OnSignalingStateChangeHandler = null;
                    participant.OnConnectionStateChangeHandler = null;
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

    public RTCSessionDescriptionInit HandleOffer(Guid roomId, Guid socketId, string offerJson)
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

        var portRange = new PortRange(50000, 50100);
        var pc = new RTCPeerConnection(config, bindPort: 0, portRange: portRange);

        var advertisedIp = Environment.GetEnvironmentVariable("HOST");
        if (!string.IsNullOrWhiteSpace(advertisedIp) && System.Net.IPAddress.TryParse(advertisedIp, out var advertisedAddress))
        {
            try
            {
                var rtpPort = pc.GetRtpChannel().RTPPort;
                var natCandidate = new RTCIceCandidate(RTCIceProtocol.udp, advertisedAddress, checked((ushort)rtpPort), RTCIceCandidateType.host);
                pc.addLocalIceCandidate(natCandidate);
                logger.LogInformation("Added advertised ICE candidate {AdvertisedIp}:{RtpPort}", advertisedIp, rtpPort);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to add advertised ICE candidate for {AdvertisedIp}", advertisedIp);
            }
        }

        var opusFormat = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 111, "opus", 48000, 2);
        var pcmuFormat = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 0, "PCMU", 8000);
        var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, new List<SDPAudioVideoMediaFormat> { opusFormat, pcmuFormat }, MediaStreamStatusEnum.SendRecv);
        pc.addTrack(audioTrack);

        var h264Format = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 102, "H264", 90000, 0, "level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=42e01f");
        var vp8Format = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000);
        var videoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { h264Format, vp8Format }, MediaStreamStatusEnum.SendRecv);
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

        participant.OnConnectionStateChangeHandler = state => Pc_onconnectionstatechange(state, roomId, socketId, pc);

        pc.onicecandidate += participant.OnIceCandidateHandler;
        pc.onsignalingstatechange += participant.OnSignalingStateChangeHandler;
        pc.onconnectionstatechange += participant.OnConnectionStateChangeHandler;
        pc.OnRtpPacketReceived += participant.OnRtpPacketReceivedHandler;
        pc.OnReceiveReport += participant.OnReceiveReportHandler;
        pc.OnTimeout += participant.OnTimeoutHandler;

        participant.PeerConnection = pc;

        participant.KeyframeTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                if (participant.PeerConnection != null &&
                    participant.PeerConnection.connectionState == RTCPeerConnectionState.connected &&
                    participant.Ssrc != 0)
                {
                    var pli = new RTCPFeedback(participant.Ssrc, participant.Ssrc, PSFBFeedbackTypesEnum.PLI);
                    participant.PeerConnection.SendRtcpFeedback(SDPMediaTypesEnum.video, pli);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Periodic PLI failed for participant {SocketId}", participant.SocketId);
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        return answer;
    }

    private void Pc_OnTimeout(SDPMediaTypesEnum mediaType, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        // logger.LogWarning($"Timeout on for {mediaType} | RoomId: {roomId} | SocketId: {socketId}");
    }

    private void Pc_OnReceiveReport(System.Net.IPEndPoint remoteEP, SDPMediaTypesEnum mediaType, RTCPCompoundPacket rtcpReport, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        if (mediaType == SDPMediaTypesEnum.video && rtcpReport.Feedback != null)
        {
            if (rtcpReport.Feedback.Header.PayloadFeedbackMessageType == PSFBFeedbackTypesEnum.PLI ||
                rtcpReport.Feedback.Header.PayloadFeedbackMessageType == PSFBFeedbackTypesEnum.FIR)
            {
                if (!rooms.TryGetValue(roomId, out var room))
                {
                    return;
                }

                // Thread-safe snapshot
                foreach (var participant in room.Participants.ToList())
                {
                    if (participant.SocketId != socketId)
                    {
                        RequestKeyframeFrom(participant.SocketId);
                    }
                }
            }
        }
    }

    private void Pc_OnRtpPacketReceived(System.Net.IPEndPoint remoteEP, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        if (!rooms.TryGetValue(roomId, out var room))
        {
            return;
        }

        // Take a thread-safe snapshot to avoid collection-modified exceptions
        var participants = room.Participants.ToList();
        if (participants.Count == 0)
        {
            return;
        }

        if (mediaType == SDPMediaTypesEnum.video)
        {
            var sender = participants.FirstOrDefault(x => x.SocketId == socketId);
            if (sender != null)
            {
                sender.Ssrc = rtpPacket.Header.SyncSource;
            }
        }

        var payload = rtpPacket.Payload;
        var timestamp = rtpPacket.Header.Timestamp;
        var markerBit = rtpPacket.Header.MarkerBit;
        var payloadType = rtpPacket.Header.PayloadType;

        for (int i = 0; i < participants.Count; i++)
        {
            var target = participants[i];

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

            try
            {
                targetPc.SendRtpRaw(mediaType, payload, timestamp, markerBit, payloadType);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to forward RTP packet to {SocketId}", target.SocketId);
            }
        }
    }

    private void Pc_onconnectionstatechange(RTCPeerConnectionState state, Guid roomId, Guid socketId, RTCPeerConnection pc)
    {
        logger.LogInformation("Connection state changed to {State} | RoomId: {RoomId} | SocketId: {SocketId}", state, roomId, socketId);

        if (state == RTCPeerConnectionState.connected)
        {
            // Immediately request keyframes from all other participants
            RequestKeyframes(roomId, socketId);

            // Send additional PLI requests after short delays to ensure decoder starts cleanly
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300);
                    RequestKeyframes(roomId, socketId);
                    await Task.Delay(700);
                    RequestKeyframes(roomId, socketId);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Delayed keyframe request failed for {SocketId}", socketId);
                }
            });
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
        // Thread-safe: snapshot rooms.Values and participant lists
        var allRooms = rooms.Values.ToList();

        if (!allRooms.Any(r => r.Participants.ToList().Any(p => p.SocketId == participantId && p.Ssrc != 0)))
        {
            return;
        }

        foreach (var room in allRooms)
        {
            var participant = room.Participants.ToList().FirstOrDefault(p => p.SocketId == participantId);
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
        logger.LogInformation("Requesting keyframes for participant | RoomId: {RoomId} | SocketId: {SocketId}", roomId, socketId);

        if (!rooms.TryGetValue(roomId, out var room))
        {
            return;
        }

        // Thread-safe snapshot
        foreach (var participant in room.Participants.ToList())
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