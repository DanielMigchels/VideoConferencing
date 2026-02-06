import { WebsocketMessage } from "../../generic/models/websocket-message";

export interface Renegotiation extends WebsocketMessage {
  sdp: string;
  answerType: string;
}
