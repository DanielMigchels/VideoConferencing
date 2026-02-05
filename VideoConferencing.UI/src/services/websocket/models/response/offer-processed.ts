import { WebsocketMessage } from "../../generic/models/websocket-message";

export interface OfferProcessed extends WebsocketMessage {
  sdp: string;
  answerType: string;
}
