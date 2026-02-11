import { Injectable } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import { WebsocketMessage } from './generic/models/websocket-message';
import { DeleteRoom } from './models/request/delete-room';
import { CreateRoom } from './models/request/create-room';
import { isMessageOfType } from './generic/type-guards';
import { RoomListUpdated } from './models/response/rooms-list-updated';
import { JoinRoom } from './models/request/join-room';
import { LeaveRoom } from './models/request/leave-room';
import { RoomUpdated } from './models/response/room-updated';
import { SendOffer } from './models/request/send-offer';
import { OfferProcessed } from './models/response/offer-processed';

@Injectable({
  providedIn: 'root',
})
export class VideoConferencingWebSocketService {
  private socket: WebSocket | null = null;
  private connected = new Subject<void>();
  private getRoomListUpdatedSubject = new Subject<RoomListUpdated>();
  private getRoomUpdatedSubject = new Subject<RoomUpdated>();
  private getOfferProcessedSubject = new Subject<OfferProcessed>();

  constructor() {
    this.connect();
  }

  addRoom(): void {
    const message: CreateRoom = {
      type: 'createRoom'
    };
    this.sendMessage(JSON.stringify(message));
  }

  joinRoom(roomId: string) {
    const message: JoinRoom = {
      type: 'joinRoom',
      roomId: roomId
    };
    this.sendMessage(JSON.stringify(message));
  }

  leaveRoom() {
    const message: LeaveRoom = {
      type: 'leaveRoom',
    };
    this.sendMessage(JSON.stringify(message));
  }  
  
  deleteRoom(roomId: string): void {
    const message: DeleteRoom = {
      type: 'deleteRoom',
      roomId: roomId
    };
    this.sendMessage(JSON.stringify(message));
  }

  sendOffer( roomId: string, offer: RTCSessionDescriptionInit) {
    const message: SendOffer = {
      type: 'sendOffer',
      roomId: roomId,
      offer: offer
    };
    this.sendMessage(JSON.stringify(message));
  }

  getConnected(): Observable<void> {
    return this.connected.asObservable();
  }

  getRoomListUpdated(): Observable<RoomListUpdated> {
    return this.getRoomListUpdatedSubject.asObservable();
  }

  getRoomUpdated(): Observable<RoomUpdated> {
    return this.getRoomUpdatedSubject.asObservable();
  }

  getOfferProcessed(): Observable<OfferProcessed> {
    return this.getOfferProcessedSubject.asObservable();
  }

  private connect(): void {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
      const isSecure = window.location.protocol === "https:";
      const wsProtocol = isSecure ? "wss" : "ws";

      const wsUrl = `${wsProtocol}://${window.location.host}/ws`;
      console.debug('Connecting to WebSocket:', wsUrl);
      this.socket = new WebSocket(wsUrl);
  
      this.socket.onopen = () => {
        console.debug('WebSocket connected to', wsUrl);
        this.connected.next();
      };
      this.socket.onmessage = (event) => this.handleMessage(event.data);
      this.socket.onclose = () => {
        console.debug('WebSocket disconnected, refreshing webpage...');
        window.location.reload();
      };
      this.socket.onerror = (error) => console.error('WebSocket error:', error);
    }
  } 
  
  private handleMessage(json: string): void {
    try {
      const message: WebsocketMessage = JSON.parse(json);
      
      if (isMessageOfType<RoomListUpdated>(message, 'roomListUpdated')) {
        this.handleRoomListUpdated(message);
      }
      else if (isMessageOfType<RoomUpdated>(message, 'roomUpdated')) {
        this.handleRoomUpdated(message);
      } 
      else if (isMessageOfType<OfferProcessed>(message, 'offerProcessed')) {
        this.handleOfferProcessed(message);
      }
      else {
        console.warn('Received unknown message type:', message.type);
      }
    } catch (error) {
      console.error('Error parsing WebSocket message:', error);
    }
  }

  handleRoomUpdated(message: RoomUpdated) {
    console.debug('Joined room updated:', message.room);
    this.getRoomUpdatedSubject.next(message);
  }

  private handleRoomListUpdated(message: RoomListUpdated): void {
    console.debug('Room list updated:', message.rooms);
    this.getRoomListUpdatedSubject.next(message);
  }

  handleOfferProcessed(message: OfferProcessed) {
    console.debug('Offer processed:', message);
    this.getOfferProcessedSubject.next(message);
  }

  private sendMessage(message: string): void {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(message);
    } else {
      console.error('WebSocket is not connected. Cannot send message.');
    }
  }
  
  requestKeyframe(id: string) {
    const message = {
      type: 'requestKeyframe',
      roomId: id
    };
    this.sendMessage(JSON.stringify(message));
  }
}
