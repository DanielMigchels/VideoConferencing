import { WebsocketMessage } from '../../generic/models/websocket-message';

export interface DeleteRoom extends WebsocketMessage {
  type: 'deleteRoom';
  roomId: string;
}
