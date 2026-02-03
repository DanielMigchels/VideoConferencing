import { Component, OnInit } from '@angular/core';
import { NgIconComponent } from "@ng-icons/core";
import { VideoConferencingWebSocketService } from '../../services/websocket/video-conferencing-web-socket.service';

@Component({
  selector: 'app-lobby',
  imports: [NgIconComponent],
  templateUrl: './lobby.html',
  styleUrl: './lobby.css',
})
export class Lobby implements OnInit {
  rooms: any;

  constructor(private ws: VideoConferencingWebSocketService) {

  }

  ngOnInit() {
    this.ws.getRoomsUpdatedSubject().subscribe(rooms => {
      this.rooms = rooms;
    });
  }

  addRoom() {
    this.ws.addRoom();
  }

  deleteRoom() {
    this.ws.deleteRoom();
  }
}
