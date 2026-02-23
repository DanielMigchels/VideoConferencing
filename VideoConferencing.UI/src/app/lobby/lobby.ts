import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent } from "@ng-icons/core";
import { VideoConferencingWebSocketService } from '../../services/websocket/video-conferencing-web-socket.service';
import { Room } from '../../data/room';
import { Loader } from "../../components/loader/loader";

@Component({
  selector: 'app-lobby',
  imports: [CommonModule, NgIconComponent, Loader],
  templateUrl: './lobby.html',
  styleUrl: './lobby.css',
})
export class Lobby implements OnInit {
  rooms: Room[] = [];
  joinedRoom: Room | null = null;
  showSecondParticipant = false;

  constructor(private ws: VideoConferencingWebSocketService) {

  }

  ngOnInit() {
    this.ws.getRoomListUpdated().subscribe(x => {
      this.rooms = x.rooms;
    });

    this.ws.getRoomUpdated().subscribe(async x => {
      this.showSecondParticipant = x.room?.participantCount! > 1;

      if (this.joinedRoom === null) {
        this.joinedRoom = x.room;
        this.startVideo();
      }
      else {
        this.joinedRoom = x.room;
      }

    });

    this.ws.getOfferProcessed().subscribe(async x => {
      await this.offerProcessed(x.sdp, x.answerType as RTCSdpType);
    });
  }

  addRoom() {
    this.ws.addRoom();
  }

  joinRoom(roomId: string) {
    this.ws.joinRoom(roomId);
  }

  leaveRoom() {
    if (this.joinedRoom === null) {
      return;
    }

    this.stopVideo();
    this.ws.leaveRoom();
  }

  deleteRoom(roomId: string) {
    this.ws.deleteRoom(roomId);
  }




  localStream?: MediaStream | null;
  @ViewChild('localVideo') localVideo?: ElementRef<HTMLVideoElement>;
  @ViewChild('remoteVideo') remoteVideo?: ElementRef<HTMLVideoElement>;
  private localPeerConnection: RTCPeerConnection | null = null;
  private readonly configuration: RTCConfiguration = {};
  remoteParticipant?: MediaStream;

  async startVideo() {
    if (this.joinedRoom === null) {
      return;
    }

    if (this.localStream) {
      this.stopVideo();
    }

    console.debug('Starting local video...');

    this.localStream = await navigator.mediaDevices.getUserMedia({
      video: {
        width: { ideal: 640 },
        height: { ideal: 480 },
        frameRate: { ideal: 24, max: 30 }
      },
      audio: {
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true
      }
    });

    const videoOnlyStream = new MediaStream(
      this.localStream.getVideoTracks()
    );

    if (this.localVideo) {
      this.localVideo.nativeElement.srcObject = videoOnlyStream;
    }

    this.localPeerConnection = new RTCPeerConnection(this.configuration);

    this.localStream.getTracks().forEach(track => {
      this.localPeerConnection!.addTrack(track, this.localStream!);
    });

    await this.applyBandwidthLimits();

    this.localPeerConnection.onicecandidate = (event) => {
      if (event.candidate) {
        console.debug('New ICE candidate:', event.candidate);
      }
    };

    this.localPeerConnection.ontrack = (event) => {
      console.debug('Received remote track:', event.streams);
      const remoteStream = event.streams[0];

      this.remoteParticipant = remoteStream;
      this.remoteVideo!.nativeElement.srcObject = remoteStream;
    };

    this.localPeerConnection.onconnectionstatechange = () => {
      console.debug('Connection state change:', this.localPeerConnection!.connectionState);
    };

    const offer = await this.localPeerConnection!.createOffer();
    await this.localPeerConnection!.setLocalDescription(offer);
    console.debug('Send offer...');
    this.ws.sendOffer(this.joinedRoom!.id, offer);
  }

  async offerProcessed(sdp: string, answerType: RTCSdpType): Promise<void> {
    if (!this.localPeerConnection) {
      throw new Error("PeerConnection not initialized");
    }

    const description: RTCSessionDescriptionInit = {
      sdp,
      type: answerType,
    };

    await this.localPeerConnection.setRemoteDescription(description);

    this.ws.requestKeyframe(this.joinedRoom!.id);
  }

  async stopVideo() {
    console.debug('Stopping local video...');

    this.localPeerConnection?.close();
    this.localPeerConnection = null;

    this.localStream?.getTracks().forEach(track => track.stop());
    this.localStream = null;
  }

  private async applyBandwidthLimits() {
    if (!this.localPeerConnection) return;

    const senders = this.localPeerConnection.getSenders();
    for (const sender of senders) {
      const params = sender.getParameters();
      if (!params.encodings || params.encodings.length === 0) {
        params.encodings = [{}];
      }

      if (sender.track?.kind === 'video') {
        params.encodings[0].maxBitrate = 500_000;
        params.encodings[0].maxFramerate = 30;
      } else if (sender.track?.kind === 'audio') {
        params.encodings[0].maxBitrate = 32_000;
      }

      try {
        await sender.setParameters(params);
      } catch (err) {
        console.warn('Failed to set sender parameters:', err);
      }
    }
  }
}
