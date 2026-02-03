import { Room } from '../../../../data/room';
import { WebsocketMessage } from '../../generic/models/websocket-message';

export interface RoomListUpdated extends WebsocketMessage {
  type: 'roomListUpdated';
  rooms: Room[];
}
