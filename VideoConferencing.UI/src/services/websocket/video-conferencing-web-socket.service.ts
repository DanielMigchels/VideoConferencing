import { Injectable } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import { WebsocketMessage } from './generic/models/websocket-message';
import { RoomsUpdated } from './models/response/rooms-updated';
import { DeleteRoom } from './models/request/delete-room';
import { CreateRoom } from './models/request/create-room';
import { isMessageOfType } from './generic/type-guards';
import { GetRooms } from './models/request/get-rooms';

@Injectable({
  providedIn: 'root',
})
export class VideoConferencingWebSocketService {
  
  private socket: WebSocket | null = null;
  private connected = new Subject<void>();
  private roomsUpdatedSubject = new Subject<RoomsUpdated>();

  constructor() {
    this.connect();
  }

  addRoom(): void {
    const message: CreateRoom = {
      type: 'createRoom'
    };
    this.sendMessage(JSON.stringify(message));
  }

  getRooms(): void {
    const message: GetRooms = {
      type: 'getRooms'
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

  getRoomsUpdated(): Observable<RoomsUpdated> {
    return this.roomsUpdatedSubject.asObservable();
  }

  getConnected(): Observable<void> {
    return this.connected.asObservable();
  }

  private connect(): void {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
      const isSecure = window.location.protocol === "https:";
      const wsProtocol = isSecure ? "wss" : "ws";

      const wsUrl = `${wsProtocol}://${window.location.host}/ws`;
      console.log('Connecting to WebSocket:', wsUrl);
      this.socket = new WebSocket(wsUrl);
  
      this.socket.onopen = () => {
        console.log('WebSocket connected to', wsUrl);
        this.connected.next();
      };
      this.socket.onmessage = (event) => this.handleMessage(event.data);
      this.socket.onclose = () => {
        console.log('WebSocket disconnected, refreshing webpage...');
        window.location.reload();
      };
      this.socket.onerror = (error) => console.error('WebSocket error:', error);
    }
  } 
  
  private handleMessage(json: string): void {
    try {
      const message: WebsocketMessage = JSON.parse(json);
      
      if (isMessageOfType<RoomsUpdated>(message, 'roomsUpdated')) {
        this.handleRoomsUpdated(message);
      } else {
        console.warn('Received unknown message type:', message.type);
      }
    } catch (error) {
      console.error('Error parsing WebSocket message:', error);
    }
  }

  private handleRoomsUpdated(message: RoomsUpdated): void {
    console.log('Rooms updated:', message.rooms);
    this.roomsUpdatedSubject.next(message);
  }

  private sendMessage(message: string): void {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(message);
    } else {
      console.error('WebSocket is not connected. Cannot send message.');
    }
  }
}
