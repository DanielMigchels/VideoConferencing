export interface Room {
  id: string;
  participantCount: number;
  participants: Participant[];
}

export interface Participant {
  socketId: string;
}