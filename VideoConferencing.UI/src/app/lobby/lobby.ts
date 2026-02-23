import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgIconComponent } from "@ng-icons/core";
import { VideoConferencingWebSocketService } from '../../services/websocket/video-conferencing-web-socket.service';
import { Room } from '../../data/room';
import { Loader } from "../../components/loader/loader";
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-lobby',
  imports: [CommonModule, NgIconComponent, Loader],
  templateUrl: './lobby.html',
  styleUrl: './lobby.css',
})
export class Lobby implements OnInit, OnDestroy {
  rooms: Room[] = [];
  joinedRoom: Room | null = null;
  showSecondParticipant = false;

  private subscriptions: Subscription[] = [];

  constructor(private ws: VideoConferencingWebSocketService) {

  }

  ngOnInit() {
    this.subscriptions.push(
      this.ws.getRoomListUpdated().subscribe(x => {
        this.rooms = x.rooms;
      })
    );

    this.subscriptions.push(
      this.ws.getRoomUpdated().subscribe(async x => {
        const previousParticipantCount = this.joinedRoom?.participantCount ?? 0;
        this.showSecondParticipant = x.room?.participantCount! > 1;

        if (this.joinedRoom === null) {
          this.joinedRoom = x.room;
          this.startVideo();
        }
        else {
          this.joinedRoom = x.room;

          if (x.room?.participantCount! <= 1) {
            this.clearRemoteStream();
          }

          if (x.room?.participantCount! > 1 && previousParticipantCount <= 1) {
            await this.createAndSendOffer();
          }
        }
      })
    );

    this.subscriptions.push(
      this.ws.getOfferProcessed().subscribe(async x => {
        await this.offerProcessed(x.sdp, x.answerType as RTCSdpType);
      })
    );

    this.subscriptions.push(
      this.ws.getDisconnected().subscribe(() => {
        console.debug('WebSocket disconnected — cleaning up WebRTC');
        this.cleanupPeerConnection();
      })
    );

    this.subscriptions.push(
      this.ws.getConnected().subscribe(() => {
        if (this.joinedRoom !== null) {
          console.debug('WebSocket reconnected — rejoining room and renegotiating');
          const roomId = this.joinedRoom.id;
          this.joinedRoom = null;
          this.ws.joinRoom(roomId);
        }
      })
    );
  }

  ngOnDestroy() {
    this.subscriptions.forEach(s => s.unsubscribe());
    this.stopVideo();
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
  private readonly configuration: RTCConfiguration = {
    iceServers: [],
  };
  remoteParticipant?: MediaStream;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private maxRetries = 5;
  private retryCount = 0;

  async startVideo() {
    if (this.joinedRoom === null) {
      return;
    }

    if (this.localStream) {
      this.cleanupPeerConnection();
    }

    console.debug('Starting local video...');

    try {
      this.localStream = await navigator.mediaDevices.getUserMedia({
        video: {
          width: { ideal: 640 },
          height: { ideal: 360 },
          frameRate: { ideal: 20, max: 24 },
        },
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        }
      });
    } catch (err) {
      console.error('Failed to get user media:', err);
      return;
    }

    const videoOnlyStream = new MediaStream(
      this.localStream.getVideoTracks()
    );

    if (this.localVideo) {
      this.localVideo.nativeElement.srcObject = videoOnlyStream;
    }

    await this.createAndSendOffer();
  }

  private async createAndSendOffer() {
    if (!this.localStream || !this.joinedRoom) {
      return;
    }

    this.cleanupPeerConnection();

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
      if (this.remoteVideo) {
        this.remoteVideo.nativeElement.srcObject = remoteStream;
      }
    };

    this.localPeerConnection.onconnectionstatechange = () => {
      const state = this.localPeerConnection?.connectionState;
      console.debug('Connection state change:', state);

      if (state === 'failed') {
        console.warn('WebRTC connection failed — scheduling retry');
        this.scheduleRetry();
      } else if (state === 'disconnected') {
        console.warn('WebRTC connection disconnected — waiting before retry');
        // Wait a few seconds for the connection to recover on its own
        this.retryTimer = setTimeout(() => {
          if (this.localPeerConnection?.connectionState === 'disconnected') {
            console.warn('WebRTC still disconnected — scheduling retry');
            this.scheduleRetry();
          }
        }, 5000);
      } else if (state === 'connected') {
        this.retryCount = 0;
        if (this.retryTimer) {
          clearTimeout(this.retryTimer);
          this.retryTimer = null;
        }
        // Request keyframes to ensure a clean decoder start
        if (this.joinedRoom) {
          this.ws.requestKeyframe(this.joinedRoom.id);
          setTimeout(() => {
            if (this.joinedRoom) this.ws.requestKeyframe(this.joinedRoom.id);
          }, 500);
          setTimeout(() => {
            if (this.joinedRoom) this.ws.requestKeyframe(this.joinedRoom.id);
          }, 1500);
        }
      }
    };

    this.localPeerConnection.oniceconnectionstatechange = () => {
      const state = this.localPeerConnection?.iceConnectionState;
      console.debug('ICE connection state change:', state);

      if (state === 'failed') {
        console.warn('ICE connection failed — attempting ICE restart');
        this.attemptIceRestart();
      }
    };

    try {
      const offer = await this.localPeerConnection.createOffer();
      await this.localPeerConnection.setLocalDescription(offer);
      console.debug('Send offer...');
      this.ws.sendOffer(this.joinedRoom!.id, offer);
    } catch (err) {
      console.error('Failed to create/send offer:', err);
      this.scheduleRetry();
    }
  }

  private async attemptIceRestart() {
    if (!this.localPeerConnection || !this.joinedRoom) {
      return;
    }

    try {
      const offer = await this.localPeerConnection.createOffer({ iceRestart: true });
      await this.localPeerConnection.setLocalDescription(offer);
      console.debug('Sending ICE restart offer...');
      this.ws.sendOffer(this.joinedRoom.id, offer);
    } catch (err) {
      console.error('ICE restart failed:', err);
      this.scheduleRetry();
    }
  }

  private scheduleRetry() {
    if (this.retryCount >= this.maxRetries) {
      console.error('Max WebRTC retries reached');
      return;
    }

    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
    }

    const delay = Math.min(1000 * Math.pow(2, this.retryCount), 15000);
    this.retryCount++;
    console.debug(`Scheduling WebRTC reconnect attempt ${this.retryCount} in ${delay}ms`);

    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      this.createAndSendOffer();
    }, delay);
  }

  async offerProcessed(sdp: string, answerType: RTCSdpType): Promise<void> {
    if (!this.localPeerConnection) {
      console.warn('PeerConnection not initialized when offer was processed');
      return;
    }

    const description: RTCSessionDescriptionInit = {
      sdp,
      type: answerType,
    };

    try {
      await this.localPeerConnection.setRemoteDescription(description);
      this.ws.requestKeyframe(this.joinedRoom!.id);
    } catch (err) {
      console.error('Failed to set remote description:', err);
      this.scheduleRetry();
    }
  }

  private clearRemoteStream() {
    this.remoteParticipant = undefined;
    if (this.remoteVideo) {
      this.remoteVideo.nativeElement.srcObject = null;
    }
  }

  private cleanupPeerConnection() {
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }

    this.clearRemoteStream();

    if (this.localPeerConnection) {
      this.localPeerConnection.onicecandidate = null;
      this.localPeerConnection.ontrack = null;
      this.localPeerConnection.onconnectionstatechange = null;
      this.localPeerConnection.oniceconnectionstatechange = null;
      this.localPeerConnection.close();
      this.localPeerConnection = null;
    }
  }

  async stopVideo() {
    console.debug('Stopping local video...');

    this.cleanupPeerConnection();

    this.localStream?.getTracks().forEach(track => track.stop());
    this.localStream = null;
    this.retryCount = 0;
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
        params.encodings[0].maxBitrate = 250_000;
        params.encodings[0].maxFramerate = 24;
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
