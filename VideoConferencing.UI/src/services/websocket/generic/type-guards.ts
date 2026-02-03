import { WebsocketMessage } from './models/websocket-message';

export function isMessageOfType<T extends WebsocketMessage>(
  message: WebsocketMessage,
  type: T['type']
): message is T {
  return message.type === type;
}
