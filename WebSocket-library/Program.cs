/// <summary>
/// a comprehensive implementation of a WebSocket client and server in C#. It supports secure communication using TcpClient and SslStream, handles WebSocket frames (text, binary, ping, pong, and close), and includes features like custom headers, subprotocols, compression, and keep-alive mechanisms.
/// </summary>
/// <remarks>
/// Author: Bilel Mnasser
/// Contact: personalhiddenmail@duck.com
/// GitHub: https://github.com/attributeyielding
/// website: https://personal-website-resume.netlify.app/#contact
/// Date: January 2025
/// Version: 1.0
/// </remarks>

using Microsoft.Extensions.Logging;



class Program
{



    static async Task Main(string[] args)
    {
        //Choose what to test :)
        // await TestWebsocketServer();
        await TestWebSOcketClient();

    }

    static public async Task TestWebSOcketClient()
    {
        // Configure logging
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });
        var logger = loggerFactory.CreateLogger<WebSocketClient>();
        // Create and connect the WebSocket client
        var ws = new WebSocketClient("wss://echo.websocket.org/", logger);
        // PingTimeout is already 0 by default, so no need to set it explicitly
        await ws.ConnectAsync();
        // Handle received messages
        ws.MessageReceived += (sender, e) =>
        {
            Console.WriteLine($"Received message: {e.Message}");
        };
        // Handle received Ping
        ws.PingReceived += (sender, e) =>
        {
            Console.WriteLine("Ping received.");
        };
        // Handle received Pong
        ws.PongReceived += (sender, e) =>
        {
            Console.WriteLine("Pong received.");
        };
        // Handle Occurred Errors
        ws.ErrorOccurred += (sender, e) =>
        {
            Console.WriteLine($"Error occurred: {e.Exception.Message}");
        };
        // Handle Connection Closed
        ws.ConnectionClosed += (sender, e) =>
        {
            Console.WriteLine($"Connection closed. Code: {e.CloseCode}, Reason: {e.Reason}");
        };

        //loop to send user messages
        string? message = "";
        do
        {
            Console.WriteLine("write your message :");
            message = Console.ReadLine();
            if (message != "")
                await ws.SendAsync(message);


        } while (message != "Exit" || message != "");


        // Close WebSocket Async
        await ws.CloseAsync();
    }
    static public async Task TestWebsocketServer()
    {
        // use post man to create a websocket request at localhost:8080 to connect to this server and test it OR use another websocket client


        // Configure logging
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole(); // Add console logging
        });

        var logger = loggerFactory.CreateLogger<WebSocketServer>();

        // Create and start the WebSocket server
        var server = new WebSocketServer(8080, logger: logger);

        // Handle new connections
        server.ConnectionOpened += (sender, e) =>
        {
            logger.LogInformation("New client connected.");
        };

        // Handle received messages
        server.MessageReceived += (sender, e) =>
        {
            logger.LogInformation("Received message from client: {Message}", e.Message);

            // Echo the message back to the client
            _ = server.SendTextAsync(e.Client, $"Echo: {e.Message}");
        };

        // Handle closed connections
        server.ConnectionClosed += (sender, e) =>
        {
            logger.LogInformation("Client disconnected.");
        };

        await server.StartAsync(); // Start the server




    }
}