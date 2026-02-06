export interface CreatePeerConnection {
  type: 'createPeerConnection';
  sessionDescription: RTCSessionDescriptionInit;
  roomId: string;
}
