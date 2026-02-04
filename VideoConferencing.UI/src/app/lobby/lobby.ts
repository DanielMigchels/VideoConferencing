import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent } from "@ng-icons/core";
import { VideoConferencingWebSocketService } from '../../services/websocket/video-conferencing-web-socket.service';
import { Room } from '../../data/room';

@Component({
  selector: 'app-lobby',
  imports: [CommonModule, NgIconComponent],
  templateUrl: './lobby.html',
  styleUrl: './lobby.css',
})
export class Lobby implements OnInit {
  rooms: Room[] = [];
  joinedRoom: Room | null = null;

  localStream?: MediaStream;
  @ViewChild('localVideo') localVideo?: ElementRef<HTMLVideoElement>;

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
  }

  addRoom() {
    this.ws.addRoom();
  }

  joinRoom(roomId: string) {
    this.ws.joinRoom(roomId);
  }

  leaveRoom() {
    if (this.joinedRoom === undefined) {
      return;
    }

    this.ws.leaveRoom();
  }

  deleteRoom(roomId: string) {
    this.ws.deleteRoom(roomId);
  }





  // Todo: cleanup


  private localPeerConnection: RTCPeerConnection | null = null;
  private readonly configuration: RTCConfiguration = {};

  async startVideo() {
    if (this.localStream === undefined) {
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

      const offer = await this.localPeerConnection!.createOffer();
      await this.localPeerConnection!.setLocalDescription(offer);
      this.ws.sendOffer(this.joinedRoom!.id, offer);
    }

    else {
      console.log('Stopping local video...');
      
      this.localPeerConnection?.close();
      this.localPeerConnection = null;
      

      this.localStream?.getTracks().forEach(track => track.stop());
      this.localStream = undefined;
    }
  }
}
