import { Injectable } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import { WebsocketMessage } from './generic/models/websocket-message';
import { RoomsUpdated } from './models/response/rooms-updated';
import { DeleteRoom } from './models/request/delete-room';
import { CreateRoom } from './models/request/create-room';
import { Room } from '../../data/room';

@Injectable({
  providedIn: 'root',
})
export class VideoConferencingWebSocketService {
  
  private socket: WebSocket | null = null;
  private roomsUpdatedSubject = new Subject<RoomsUpdated>();

  constructor() {
    this.connect();
  }

  addRoom(createRoom: CreateRoom) {
    this.sendMessage(JSON.stringify(createRoom));
  }

  deleteRoom(deleteRoom: DeleteRoom) {
    this.sendMessage(JSON.stringify(deleteRoom));
  }

  getRoomsUpdatedSubject(): Observable<Room> {
    return this.roomsUpdatedSubject.asObservable();
  }

  private connect() {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
      const isSecure = window.location.protocol === "https:";
      const wsProtocol = isSecure ? "wss" : "ws";

      const wsUrl = `${wsProtocol}://${window.location.host}/ws`;
      console.log('Connecting to WebSocket:', wsUrl);
      this.socket = new WebSocket(wsUrl);
  
      this.socket.onopen = () => console.log('WebSocket connected to', wsUrl);
      this.socket.onmessage = (event) => this.handleMessage(event.data);
      this.socket.onclose = () => {
        console.log('WebSocket disconnected, refreshing webpage...');
        window.location.reload();
      };
      this.socket.onerror = (error) => console.error('WebSocket error:', error);
    }
  } 
  
  private handleMessage(json: string) {
    try {
      const message: WebsocketMessage = JSON.parse(json);
          
      // ?? How do I know if it's this event?
      this.roomsUpdatedSubject.next(message);    


    } catch (error) {
      console.error('Error parsing WebSocket message:', error);
    }
  }

  private sendMessage(message: string) {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(message);
    } else {
      console.error('WebSocket is not connected.');
    }
  }
}
