using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using QRCoder;
using StrideLink.Input;
using StrideLink.Networking;
using StrideLink.Security;

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("Warning: keyboard output only works on Windows. The web interface will still run.");
}

var options = StrideLinkOptions.Parse(args);
var lanAddress = options.Address ?? NetworkAddress.FindBestLanAddress();
var controllerHost = $"{lanAddress}.nip.io";
var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
var certificates = LocalCertificateService.LoadOrCreate(lanAddress, controllerHost);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
});

builder.WebHost.ConfigureKestrel(server =>
{
    server.ListenAnyIP(options.HttpPort);
    server.ListenAnyIP(options.HttpsPort, listen => listen.UseHttps(https =>
    {
        https.ServerCertificate = certificates.ServerCertificate;
        https.ServerCertificateChain = new X509Certificate2Collection(certificates.RootCertificate);
    }));
});

builder.Services.AddSingleton<IMovementOutput, KeyboardMovementOutput>();
builder.Services.AddSingleton<ConnectionHub>();

var app = builder.Build();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-store"
});

var httpBase = $"http://{lanAddress}:{options.HttpPort}";
var httpsBase = $"https://{controllerHost}:{options.HttpsPort}";
var setupUrl = $"{httpBase}/setup?token={token}";
var appUrl = $"{httpsBase}/?token={token}";

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/certificate", () => Results.File(
    certificates.RootCertificatePath,
    "application/x-x509-ca-cert",
    "StrideLink-Local-Root-CA.cer"));

app.MapGet("/setup", (HttpContext context) =>
{
    if (!SecureToken.IsValid(context.Request.Query["token"], token))
        return Results.NotFound();

    return Results.Content(SetupPage.Render(httpBase, appUrl), "text/html; charset=utf-8");
});

app.MapGet("/api/status", (HttpContext context, ConnectionHub hub) =>
{
    if (!SecureToken.IsValid(context.Request.Query["token"], token))
        return Results.Unauthorized();

    return Results.Ok(hub.GetStatus());
});

// Map all HTTP methods: HTTP/1.1 WebSockets arrive as GET, while Safari can
// negotiate HTTP/2 WebSockets using the extended CONNECT method (RFC 8441).
app.Map("/ws", async (HttpContext context) =>
{
    var hub = context.RequestServices.GetRequiredService<ConnectionHub>();

    if (!SecureToken.IsValid(context.Request.Query["token"], token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var connectionId = hub.Register();
    Console.WriteLine("Phone connected.");

    try
    {
        await ReceiveMessagesAsync(socket, connectionId, hub, context.RequestAborted);
    }
    catch (OperationCanceledException) { }
    catch (WebSocketException) { }
    finally
    {
        hub.Unregister(connectionId);
        Console.WriteLine("Phone disconnected; movement released.");
    }
});

var hub = app.Services.GetRequiredService<ConnectionHub>();
app.Lifetime.ApplicationStopping.Register(hub.Stop);
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    hub.Stop();
    app.Lifetime.StopApplication();
};

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.Clear();
    Console.WriteLine("StrideLink v0.1.5");
    Console.WriteLine();
    Console.WriteLine("Scan this QR code with your phone:");
    Console.WriteLine();
    using var qrData = QRCodeGenerator.GenerateQrCode(setupUrl, QRCodeGenerator.ECCLevel.Q);
    using var qr = new AsciiQRCode(qrData);
    Console.WriteLine(qr.GetGraphicSmall(drawQuietZones: true));
    Console.WriteLine(setupUrl);
    Console.WriteLine();
    Console.WriteLine("Keep this window open while playing. Press Ctrl+C to stop.");
    Console.WriteLine("Windows may ask you to allow private-network access the first time.");
});

await app.RunAsync();

static async Task ReceiveMessagesAsync(
    WebSocket socket,
    Guid connectionId,
    ConnectionHub hub,
    CancellationToken cancellationToken)
{
    var buffer = new byte[4096];
    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", cancellationToken);
                return;
            }

            if (message.Length + result.Count > 4096)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too large", cancellationToken);
                return;
            }

            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        if (result.MessageType != WebSocketMessageType.Text)
            continue;

        try
        {
            var incoming = JsonSerializer.Deserialize<MotionMessage>(message.ToArray());
            if (incoming is not null)
                hub.Handle(connectionId, incoming);
        }
        catch (JsonException)
        {
            // Ignore malformed client messages.
        }
    }
}

internal sealed record StrideLinkOptions(IPAddress? Address, int HttpPort, int HttpsPort)
{
    public static StrideLinkOptions Parse(string[] args)
    {
        IPAddress? address = null;
        var httpPort = 5187;
        var httpsPort = 5188;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--ip" when i + 1 < args.Length:
                    if (!IPAddress.TryParse(args[++i], out address))
                        throw new ArgumentException("--ip must be a valid IP address.");
                    break;
                case "--http-port" when i + 1 < args.Length:
                    httpPort = ParsePort(args[++i], "--http-port");
                    break;
                case "--https-port" when i + 1 < args.Length:
                    httpsPort = ParsePort(args[++i], "--https-port");
                    break;
            }
        }

        return new StrideLinkOptions(address, httpPort, httpsPort);
    }

    private static int ParsePort(string value, string option) =>
        int.TryParse(value, out var port) && port is > 0 and <= 65535
            ? port
            : throw new ArgumentException($"{option} must be between 1 and 65535.");
}

internal static class SecureToken
{
    public static bool IsValid(string? supplied, string expected)
    {
        if (string.IsNullOrEmpty(supplied) || supplied.Length != expected.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(expected));
    }
}
