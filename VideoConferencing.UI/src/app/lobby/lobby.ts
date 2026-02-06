import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent } from "@ng-icons/core";
import { VideoConferencingWebSocketService } from '../../services/websocket/video-conferencing-web-socket.service';
import { Room } from '../../data/room';
import { every } from 'rxjs';
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

  constructor(private ws: VideoConferencingWebSocketService) {

  }

  ngOnInit() {
    this.ws.getRoomListUpdated().subscribe(x => {
      this.rooms = x.rooms;
    });

    this.ws.getRoomUpdated().subscribe(async x => {
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

    console.log('Starting local video...');

    this.localStream = await navigator.mediaDevices.getUserMedia({
      video: {
        width: { ideal: 1280 },
        height: { ideal: 720 },
        frameRate: { ideal: 60 }
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

    this.localPeerConnection.onicecandidate = (event) => {
      if (event.candidate) {
        console.log('New ICE candidate:', event.candidate);
      }
    };

    this.localPeerConnection.ontrack = (event) => {
      console.log('Received remote track:', event.streams);
      const remoteStream = event.streams[0];

      this.remoteParticipant = remoteStream;

      if (this.remoteVideo) {
        this.remoteVideo.nativeElement.srcObject = remoteStream;
      }
    };

    this.localPeerConnection.onconnectionstatechange = () => {
      console.log('Connection state change:', this.localPeerConnection!.connectionState);
    };

    const offer = await this.localPeerConnection!.createOffer();
    await this.localPeerConnection!.setLocalDescription(offer);
    console.log('Send offer...');
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
    console.log('Stopping local video...');

    this.localPeerConnection?.close();
    this.localPeerConnection = null;

    this.localStream?.getTracks().forEach(track => track.stop());
    this.localStream = null;
  }
}
