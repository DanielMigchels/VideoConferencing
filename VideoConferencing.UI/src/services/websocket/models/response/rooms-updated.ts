import { Room } from '../../../../data/room';
import { WebsocketMessage } from '../../generic/models/websocket-message';

export interface RoomsUpdated extends WebsocketMessage {
  type: 'roomsUpdated';
  rooms: Room[];
}
