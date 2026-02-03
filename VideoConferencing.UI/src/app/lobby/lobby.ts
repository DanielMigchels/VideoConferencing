import { Component, OnInit } from '@angular/core';
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

  constructor(private ws: VideoConferencingWebSocketService) {

  }

  ngOnInit() {
    this.ws.getRoomsUpdated().subscribe(x => {
      this.rooms = x.rooms;
    });

    this.ws.getConnected().subscribe(() => {
      this.ws.getRooms();
    });
  }

  addRoom() {
    this.ws.addRoom();
  }

  deleteRoom(roomId: string) {
    this.ws.deleteRoom(roomId);
  }
}
