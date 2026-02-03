using System.Text.Json.Serialization;
using VideoConferencing.API.Services.Websocket.Models.Request;
using VideoConferencing.API.Services.Websocket.Models.Response;

namespace VideoConferencing.API.Services.Websocket.Generic.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CreateRoom), typeDiscriminator: "createRoom")]
[JsonDerivedType(typeof(DeleteRoom), typeDiscriminator: "deleteRoom")]
[JsonDerivedType(typeof(GetRooms), typeDiscriminator: "getRooms")]
[JsonDerivedType(typeof(RoomsUpdated), typeDiscriminator: "roomsUpdated")]

public abstract class WebsocketMessage
{
}
