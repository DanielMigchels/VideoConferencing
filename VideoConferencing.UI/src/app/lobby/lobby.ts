import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent } from "@ng-icons/core";
import { VideoConferencingWebSocketService } from '../../services/websocket/video-conferencing-web-socket.service';
import { Room } from '../../data/room';
import { every } from 'rxjs';

@Component({
  selector: 'app-lobby',
  imports: [CommonModule, NgIconComponent],
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
      this.joinedRoom = x.room;
      await this.startVideo();
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





  // Todo: cleanup


  localStream?: MediaStream | null;
  @ViewChild('localVideo') localVideo?: ElementRef<HTMLVideoElement>;
  private localPeerConnection: RTCPeerConnection | null = null;
  private readonly configuration: RTCConfiguration = {};

  async startVideo() {
    if (this.joinedRoom === null) {
      return;
    }
    if (this.localStream) {
      return;
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

    setTimeout(() => {
      if (this.localVideo && this.localStream) {
        this.localVideo.nativeElement.srcObject = videoOnlyStream;
      }
    }, 100);

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
    await this.processPendingIceCandidates();
  }

  processPendingIceCandidates() {

  }

  async stopVideo() {
    console.log('Stopping local video...');

    this.localPeerConnection?.close();
    this.localPeerConnection = null;

    this.localStream?.getTracks().forEach(track => track.stop());
    this.localStream = null;
  }
}
