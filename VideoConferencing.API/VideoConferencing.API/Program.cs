using Serilog;
using VideoConferencing.API.Services.Room;
using VideoConferencing.API.Services.Websocket;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

// builder.Host.UseSerilog();

builder.Services.AddSingleton<IRoomService, RoomService>();
builder.Services.AddSingleton<VideoConferencingWebSocketHandler>();

builder.Services.AddSpaStaticFiles(configuration =>
{
    configuration.RootPath = "wwwroot/VideoConferencing.UI/browser/";
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseWebSockets();
var webSocketHandler = app.Services.GetRequiredService<VideoConferencingWebSocketHandler>();
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await webSocketHandler.HandleWebSocketAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.UseRouting();
app.UseSpaStaticFiles();

app.UseEndpoints(x => { });

app.UseSpa(spa =>
{
    if (app.Environment.IsDevelopment())
    {
        spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
    }
});

app.Run();
