using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Models.Request;
using VideoConferencing.API.Services.Websocket.Models.Response;

namespace VideoConferencing.API.Services.Websocket.Generic.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CreateRoom), typeDiscriminator: "createRoom")]
[JsonDerivedType(typeof(DeleteRoom), typeDiscriminator: "deleteRoom")]
[JsonDerivedType(typeof(JoinRoom), typeDiscriminator: "joinRoom")]
[JsonDerivedType(typeof(LeaveRoom), typeDiscriminator: "leaveRoom")]
[JsonDerivedType(typeof(RoomListUpdated), typeDiscriminator: "roomListUpdated")]
[JsonDerivedType(typeof(RoomUpdated), typeDiscriminator: "roomUpdated")]
public abstract class WebsocketMessage
{
}
