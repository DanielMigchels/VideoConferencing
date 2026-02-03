import { WebsocketMessage } from '../../generic/models/websocket-message';

export interface CreateRoom extends WebsocketMessage {
  type: 'createRoom';
}
