import { CommonModule } from "@angular/common";
import { Component, ElementRef, OnInit, QueryList, ViewChild, ViewChildren } from "@angular/core";
import { NgIconComponent } from "@ng-icons/core";
import { Loader } from "../../components/loader/loader";
import { Room } from "../../data/room";
import { VideoConferencingWebSocketService } from "../../services/websocket/video-conferencing-web-socket.service";

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

    this.ws.getRoomUpdated().subscribe(async (x) => {
      const wasNotJoined = this.joinedRoom === null;
      this.joinedRoom = x.room;

      if (wasNotJoined && this.joinedRoom !== null) {
        this.startVideo();
      }
    });

    this.ws.getRenegotiation().subscribe(async x => {
      await this.handleRenegotiation(x.sdp, x.answerType as RTCSdpType);
    });
  }

  addRoom() {
    this.ws.addRoom();
  }

  deleteRoom(roomId: string) {
    this.ws.deleteRoom(roomId);
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

  localMediaStream?: MediaStream | null;
  @ViewChild('localVideo') localVideoElement?: ElementRef<HTMLVideoElement>;

  remoteMediaStreams?: MediaStream[] = [];
  @ViewChildren('remoteVideo') remoteVideoElements!: QueryList<ElementRef<HTMLVideoElement>>;

  private peerConnection: RTCPeerConnection | null = null;

  async startVideo() {
    if (this.joinedRoom === null) {
      return;
    }

    if (this.peerConnection) {
      return
    }

    this.localMediaStream = await navigator.mediaDevices.getUserMedia({
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

    this.peerConnection = new RTCPeerConnection({});
    this.localVideoElement!.nativeElement.srcObject = new MediaStream(this.localMediaStream.getVideoTracks());;

    this.localMediaStream.getTracks().forEach(track => {
      this.peerConnection!.addTrack(track, this.localMediaStream!);
    });

    this.peerConnection.ontrack = (event) => {
      console.log('Received remote track:', event.track);

      const videoElementIndex = Math.floor(this.remoteMediaStreams!.length / 2);

      this.remoteMediaStreams?.push(event.streams[0]);

      const videoElementsArray = this.remoteVideoElements!.toArray();
      const element = videoElementsArray[videoElementIndex];
      element.nativeElement.srcObject = event.streams[0];
      
      event.track.onended = () => {
        this.remoteMediaStreams = this.remoteMediaStreams?.filter(stream => stream.id !== event.streams[0].id);
        console.log('Remote track ended:', event.track);
      };
    };

    const offer = await this.peerConnection!.createOffer();
    await this.peerConnection!.setLocalDescription(offer);

    this.ws.createPeerConnection(this.joinedRoom!.id, offer);
  }

  async handleRenegotiation(sdp: string, answerType: RTCSdpType) {
    if (this.peerConnection === null) {
      return;
    }

    const description: RTCSessionDescriptionInit = {
      sdp,
      type: answerType,
    };

    await this.peerConnection.setRemoteDescription(description);

    console.log('Set remote description for renegotiation:', description);

    this.ws.requestKeyframe(this.joinedRoom!.id);
  }

  stopVideo() {
    this.localMediaStream?.getTracks().forEach(track => track.stop());
    this.localMediaStream = null;

    this.peerConnection?.close();
    this.peerConnection = null;


    // this.remoteMediaStream?.getTracks().forEach(track => track.stop());
    // this.remoteMediaStream = null;
  }
}
