export interface SendOffer {
  type: 'sendOffer';
  offer: RTCSessionDescriptionInit;
  roomId: string;
}
