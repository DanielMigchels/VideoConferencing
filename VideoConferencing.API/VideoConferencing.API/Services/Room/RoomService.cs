using Microsoft.AspNetCore.Mvc.Formatters;
using SIPSorcery.Net;
using SIPSorcery.Sys;
using System.Net.Sockets;
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
                participant.PeerConnection.onicecandidate -= Pc_onicecandidate;
                participant.PeerConnection.onsignalingstatechange -= Pc_onsignalingstatechange;
                participant.PeerConnection.OnRtpPacketReceived -= Pc_OnRtpPacketReceived;
                participant.PeerConnection.OnReceiveReport -= Pc_OnReceiveReport;
                participant.PeerConnection.OnTimeout -= Pc_OnTimeout;

                participant.PeerConnection.Close("Room left");
                participant.PeerConnection.Dispose();
                participant.PeerConnection = null;
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
                    participant.PeerConnection.onicecandidate -= Pc_onicecandidate;
                    participant.PeerConnection.onsignalingstatechange -= Pc_onsignalingstatechange;
                    participant.PeerConnection.OnRtpPacketReceived -= Pc_OnRtpPacketReceived;
                    participant.PeerConnection.OnReceiveReport -= Pc_OnReceiveReport;
                    participant.PeerConnection.OnTimeout -= Pc_OnTimeout;

                    participant.PeerConnection.Close("Room left");
                    participant.PeerConnection.Dispose();
                    participant.PeerConnection = null;
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

        pc.onicecandidate += Pc_onicecandidate;
        pc.onsignalingstatechange += Pc_onsignalingstatechange;
        pc.OnRtpPacketReceived += Pc_OnRtpPacketReceived;
        pc.OnReceiveReport += Pc_OnReceiveReport;
        pc.OnTimeout += Pc_OnTimeout;

        var result = pc.setRemoteDescription(offer);
        if (result != SIPSorcery.Net.SetDescriptionResultEnum.OK)
        {
            throw new Exception("Could not set remote description");
        }

        var answer = pc.createAnswer();
        pc.setLocalDescription(answer);

        participant.PeerConnection = pc;

        return answer;
    }

    private void Pc_OnTimeout(SDPMediaTypesEnum mediaType)
    {
        logger.LogWarning($"Timeout on for {mediaType}");
    }

    private void Pc_OnReceiveReport(System.Net.IPEndPoint remoteEP, SDPMediaTypesEnum mediaType, RTCPCompoundPacket rtcpReport)
    {
        logger.LogInformation($"Received RTCP report from type: {mediaType}");
    }

    private void Pc_OnRtpPacketReceived(System.Net.IPEndPoint remoteEP, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
    {
        logger.LogInformation($"Received {mediaType} RTP packet, size: {rtpPacket.Payload.Length}");
    }

    private void Pc_onsignalingstatechange()
    {
        logger.LogInformation($"Signaling state change for a pc? No sender here though.");
    }

    private void Pc_onicecandidate(RTCIceCandidate candidate)
    {
        logger.LogInformation($"Server ICE candidate: {candidate.candidate}");
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
