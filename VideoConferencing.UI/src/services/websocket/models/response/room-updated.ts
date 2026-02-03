import { Room } from "../../../../data/room";
import { WebsocketMessage } from "../../generic/models/websocket-message";

export interface RoomUpdated extends WebsocketMessage  {
  type: 'roomUpdated';
  room: Room | null;
}
