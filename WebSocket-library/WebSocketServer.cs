

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



using System;
using System.Buffers; // For ArrayPool for efficient buffer management
using System.Collections.Concurrent; // For thread-safe data structures
using System.Collections.Generic;
using System.IO;
using System.IO.Compression; // For compression support
using System.Net;
using System.Net.Sockets;
using System.Net.Security; // For SslStream
using System.Security.Authentication;
using System.Security.Cryptography; // For SHA1 hashing
using System.Security.Cryptography.X509Certificates; // For SSL/TLS certificates
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // For logging

/// <summary>
/// A WebSocket server implementation that supports secure communication, compression, and custom headers.
/// </summary>
public class WebSocketServer : IDisposable
{
    private TcpListener _tcpListener; // Listens for incoming TCP connections
    private readonly ILogger<WebSocketServer> _logger; // Logger for tracking server events
    private readonly X509Certificate2 _certificate; // SSL/TLS certificate for secure connections
    private readonly CancellationTokenSource _cancellationTokenSource = new(); // Manages cancellation of server operations
    private readonly ConcurrentDictionary<Guid, TcpClient> _activeConnections = new(); // Tracks active client connections

    /// <summary>
    /// Gets or sets the list of supported subprotocols.
    /// </summary>
    public List<string> Subprotocols { get; set; } = new();

    /// <summary>
    /// Gets or sets whether WebSocket compression (permessage-deflate) is enabled.
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// Event triggered when a new WebSocket connection is established.
    /// </summary>
    public event EventHandler<WebSocketConnectionEventArgs> ConnectionOpened;

    /// <summary>
    /// Event triggered when a WebSocket connection is closed.
    /// </summary>
    public event EventHandler<WebSocketConnectionEventArgs> ConnectionClosed;

    /// <summary>
    /// Event triggered when a text or binary message is received from a client.
    /// </summary>
    public event EventHandler<WebSocketServerMessageEventArgs> MessageReceived;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class.
    /// </summary>
    /// <param name="port">The port on which the server will listen for incoming connections.</param>
    /// <param name="certificate">Optional SSL/TLS certificate for secure connections.</param>
    /// <param name="logger">Optional logger for tracking server events and errors.</param>
    public WebSocketServer(int port, X509Certificate2 certificate = null, ILogger<WebSocketServer> logger = null)
    {
        _tcpListener = new TcpListener(IPAddress.Any, port); // Listen on all network interfaces
        _certificate = certificate; // Set the SSL/TLS certificate
        _logger = logger; // Set the logger
    }

    /// <summary>
    /// Starts the WebSocket server and begins accepting incoming connections.
    /// </summary>
    public async Task StartAsync()
    {
        _tcpListener.Start(); // Start the TCP listener
        _logger?.LogInformation("WebSocket server started on port {Port}.", ((IPEndPoint)_tcpListener.LocalEndpoint).Port);

        while (!_cancellationTokenSource.Token.IsCancellationRequested) // Run until cancellation is requested
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync(); // Accept incoming client connections
                _ = HandleClientAsync(tcpClient); // Handle the client in a separate task
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error accepting client connection."); // Log errors during connection acceptance
            }
        }
    }

    /// <summary>
    /// Handles a WebSocket client connection, including the handshake and message processing.
    /// </summary>
    /// <param name="tcpClient">The TCP client representing the connection.</param>
    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        var connectionId = Guid.NewGuid(); // Generate a unique ID for the connection
        _activeConnections.TryAdd(connectionId, tcpClient); // Track the connection

        Stream stream = tcpClient.GetStream(); // Get the network stream for the client

        // Wrap the stream in an SslStream if a certificate is provided
        if (_certificate != null)
        {
            var sslStream = new SslStream(stream, false); // Create an SslStream for secure communication
            await sslStream.AuthenticateAsServerAsync(_certificate, false, SslProtocols.Tls12, false); // Perform SSL/TLS handshake
            stream = sslStream; // Use the SslStream for further communication
        }

        try
        {
            // Perform the WebSocket handshake
            var handshakeRequest = await ReadHandshakeRequestAsync(stream); // Read the client's handshake request
            var handshakeResponse = GenerateHandshakeResponse(handshakeRequest); // Generate the server's handshake response
            await stream.WriteAsync(Encoding.UTF8.GetBytes(handshakeResponse)); // Send the handshake response to the client

            // Notify subscribers that a new connection has been established
            ConnectionOpened?.Invoke(this, new WebSocketConnectionEventArgs { Client = tcpClient });

            // Start receiving data from the client
            await ReceiveAsync(tcpClient, stream);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling client connection."); // Log errors during connection handling
        }
        finally
        {
            // Notify subscribers that the connection has been closed
            ConnectionClosed?.Invoke(this, new WebSocketConnectionEventArgs { Client = tcpClient });

            tcpClient.Close(); // Close the client connection
            _activeConnections.TryRemove(connectionId, out _); // Remove the connection from tracking
        }
    }

    /// <summary>
    /// Reads the WebSocket handshake request from the client.
    /// </summary>
    /// <param name="stream">The network stream to read from.</param>
    /// <returns>The handshake request as a string.</returns>
    private async Task<string> ReadHandshakeRequestAsync(Stream stream)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096); // Rent a buffer from the array pool
        try
        {
            var bytesRead = await stream.ReadAsync(buffer); // Read data from the stream
            return Encoding.UTF8.GetString(buffer, 0, bytesRead); // Convert the data to a string
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer); // Return the buffer to the pool
        }
    }

    /// <summary>
    /// Generates the WebSocket handshake response based on the client's request.
    /// </summary>
    /// <param name="handshakeRequest">The client's handshake request.</param>
    /// <returns>The handshake response as a string.</returns>
    private string GenerateHandshakeResponse(string handshakeRequest)
    {
        var headers = new Dictionary<string, string>(); // Parse the request headers
        foreach (var line in handshakeRequest.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains(":"))
            {
                var parts = line.Split(new[] { ':' }, 2); // Split header into key and value
                headers[parts[0].Trim()] = parts[1].Trim();
            }
        }

        var secWebSocketKey = headers["Sec-WebSocket-Key"]; // Extract the WebSocket key
        var secWebSocketAccept = CalculateWebSocketAccept(secWebSocketKey); // Calculate the accept key

        var response = "HTTP/1.1 101 Switching Protocols\r\n" + // Build the handshake response
                       "Upgrade: websocket\r\n" +
                       "Connection: Upgrade\r\n" +
                       $"Sec-WebSocket-Accept: {secWebSocketAccept}\r\n";

        // Add compression extension if requested and supported
        if (EnableCompression && headers.ContainsKey("Sec-WebSocket-Extensions") && headers["Sec-WebSocket-Extensions"].Contains("permessage-deflate"))
        {
            response += "Sec-WebSocket-Extensions: permessage-deflate\r\n";
        }

        // Add subprotocols if requested
        if (headers.ContainsKey("Sec-WebSocket-Protocol") && Subprotocols.Count > 0)
        {
            response += $"Sec-WebSocket-Protocol: {string.Join(", ", Subprotocols)}\r\n";
        }

        // Add custom headers if present
        if (headers.ContainsKey("X-Custom-Header"))
        {
            response += $"X-Custom-Header: {headers["X-Custom-Header"]}\r\n";
        }

        response += "\r\n"; // End of response
        return response;
    }

    /// <summary>
    /// Calculates the Sec-WebSocket-Accept value for the handshake.
    /// </summary>
    /// <param name="secWebSocketKey">The Sec-WebSocket-Key from the client.</param>
    /// <returns>The Sec-WebSocket-Accept value.</returns>
    private string CalculateWebSocketAccept(string secWebSocketKey)
    {
        var magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"; // WebSocket magic string
        var combined = secWebSocketKey + magicString; // Combine key and magic string
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(combined)); // Compute SHA1 hash
        return Convert.ToBase64String(hash); // Convert hash to Base64
    }

    /// <summary>
    /// Receives data from the WebSocket client and processes incoming frames.
    /// </summary>
    /// <param name="tcpClient">The TCP client representing the connection.</param>
    /// <param name="stream">The network stream for the connection.</param>
    private async Task ReceiveAsync(TcpClient tcpClient, Stream stream)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096); // Rent a buffer from the array pool
        try
        {
            while (tcpClient.Connected) // Continue receiving data while the client is connected
            {
                var bytesRead = await stream.ReadAsync(buffer); // Read data from the stream
                if (bytesRead == 0) break; // If no data is read, the connection is closed
                ProcessReceivedData(buffer, bytesRead, tcpClient); // Process the received data
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error receiving data from client."); // Log errors during data reception
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer); // Return the buffer to the pool
        }
    }

    /// <summary>
    /// Processes received WebSocket frames.
    /// </summary>
    /// <param name="buffer">The buffer containing the received data.</param>
    /// <param name="bytesRead">The number of bytes read.</param>
    /// <param name="tcpClient">The TCP client representing the connection.</param>
    private void ProcessReceivedData(byte[] buffer, int bytesRead, TcpClient tcpClient)
    {
        int index = 0;

        // Read FIN and opcode
        var fin = (buffer[index] & 0x80) != 0; // Check if the FIN bit is set
        var opcode = buffer[index] & 0x0F; // Extract the opcode
        index++;

        // Read mask and payload length
        var mask = (buffer[index] & 0x80) != 0; // Check if the mask bit is set
        var payloadLength = buffer[index] & 0x7F; // Extract the payload length
        index++;

        // Handle extended payload lengths
        if (payloadLength == 126) // 16-bit length
        {
            payloadLength = (buffer[index] << 8) | buffer[index + 1];
            index += 2;
        }
        else if (payloadLength == 127) // 64-bit length
        {
            payloadLength = (int)((buffer[index] << 56) | (buffer[index + 1] << 48) | (buffer[index + 2] << 40) | (buffer[index + 3] << 32) |
                                 (buffer[index + 4] << 24) | (buffer[index + 5] << 16) | (buffer[index + 6] << 8) | buffer[index + 7]);
            index += 8;
        }

        // Read masking key if present
        byte[] maskingKey = null;
        if (mask)
        {
            maskingKey = new byte[4]; // Read the 4-byte masking key
            Buffer.BlockCopy(buffer, index, maskingKey, 0, 4);
            index += 4;
        }

        // Read payload data
        var payload = new byte[payloadLength];
        Buffer.BlockCopy(buffer, index, payload, 0, payloadLength);

        // Unmask the payload if a mask is present
        if (mask)
        {
            for (int i = 0; i < payloadLength; i++)
            {
                payload[i] ^= maskingKey[i % 4]; // XOR each byte with the masking key
            }
        }

        // Handle the frame based on its opcode
        switch (opcode)
        {
            case 1: // Text frame
                MessageReceived?.Invoke(this, new WebSocketServerMessageEventArgs { Message = Encoding.UTF8.GetString(payload), Client = tcpClient });
                break;
            case 2: // Binary frame
                MessageReceived?.Invoke(this, new WebSocketServerMessageEventArgs { BinaryData = payload, Client = tcpClient });
                break;
            case 8: // Close frame
                var closeCode = (payload.Length >= 2) ? (payload[0] << 8) | payload[1] : 1005; // Extract close code
                var closeReason = (payload.Length > 2) ? Encoding.UTF8.GetString(payload, 2, payload.Length - 2) : string.Empty; // Extract close reason
                _ = SendCloseFrameAsync(tcpClient, closeCode, closeReason); // Send a close frame in response
                break;
            case 9: // Ping frame
                _ = SendPongFrameAsync(tcpClient, payload); // Respond with a pong frame
                break;
            case 10: // Pong frame
                // No action needed for pong frames
                break;
            default:
                _logger?.LogWarning("Unsupported opcode: {Opcode}", opcode); // Log unsupported opcodes
                break;
        }
    }

    /// <summary>
    /// Sends a text message to the WebSocket client.
    /// </summary>
    /// <param name="tcpClient">The TCP client representing the connection.</param>
    /// <param name="message">The text message to send.</param>
    public async Task SendTextAsync(TcpClient tcpClient, string message)
    {
        var payload = Encoding.UTF8.GetBytes(message); // Convert the message to bytes
        await SendFrameAsync(tcpClient, payload, opcode: 1); // Send as a text frame
    }

    /// <summary>
    /// Sends binary data to the WebSocket client.
    /// </summary>
    /// <param name="tcpClient">The TCP client representing the connection.</param>
    /// <param name="data">The binary data to send.</param>
    public async Task SendBinaryAsync(TcpClient tcpClient, byte[] data)
    {
        await SendFrameAsync(tcpClient, data, opcode: 2); // Send as a binary frame
    }

    /// <summary>
    /// Sends a WebSocket frame to the client.
    /// </summary>
    /// <param name="tcpClient">The TCP client representing the connection.</param>
    /// <param name="payload">The payload data to send.</param>
    /// <param name="opcode">The WebSocket frame opcode.</param>
    private async Task SendFrameAsync(TcpClient tcpClient, byte[] payload, int opcode)
    {
        var stream = tcpClient.GetStream(); // Get the network stream
        var frame = new List<byte>(); // Build the frame
        frame.Add((byte)(0x80 | opcode)); // Set FIN bit and opcode

        // Add payload length
        if (payload.Length < 126)
        {
            frame.Add((byte)payload.Length); // Single-byte length
        }
        else if (payload.Length <= 65535)
        {
            frame.Add(126); // 16-bit length
            frame.Add((byte)(payload.Length >> 8));
            frame.Add((byte)(payload.Length & 0xFF));
        }
        else
        {
            frame.Add(127); // 64-bit length
            for (int i = 7; i >= 0; i--)
            {
                frame.Add((byte)(payload.Length >> (8 * i)));
            }
        }

        frame.AddRange(payload); // Add the payload
        await stream.WriteAsync(frame.ToArray()); // Send the frame
    }

    /// <summary>
    /// Sends a close frame to the WebSocket client.
    /// </summary>
    /// <param name="tcpClient">The TCP client representing the connection.</param>
    /// <param name="closeCode">The WebSocket close code.</param>
    /// <param name="reason">The close reason.</param>
    private async Task SendCloseFrameAsync(TcpClient tcpClient, int closeCode, string reason)
    {
        var payload = new byte[2 + (reason?.Length ?? 0)]; // Create the close frame payload
        payload[0] = (byte)(closeCode >> 8); // Close code (high byte)
        payload[1] = (byte)(closeCode & 0xFF); // Close code (low byte)
        if (!string.IsNullOrEmpty(reason)) // Add close reason if provided
        {
            var reasonBytes = Encoding.UTF8.GetBytes(reason);
            Buffer.BlockCopy(reasonBytes, 0, payload, 2, reasonBytes.Length);
        }
        await SendFrameAsync(tcpClient, payload, opcode: 8); // Send the close frame
    }

    /// <summary>
    /// Sends a pong frame to the WebSocket client.
    /// </summary>
    /// <param name="tcpClient">The TCP client representing the connection.</param>
    /// <param name="data">The payload data to send.</param>
    private async Task SendPongFrameAsync(TcpClient tcpClient, byte[] data)
    {
        await SendFrameAsync(tcpClient, data, opcode: 10); // Send as a pong frame
    }

    /// <summary>
    /// Stops the WebSocket server and releases resources.
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource.Cancel(); // Cancel ongoing operations
        _tcpListener.Stop(); // Stop the TCP listener
        _logger?.LogInformation("WebSocket server stopped."); // Log server stop
    }

    /// <summary>
    /// Disposes of the WebSocket server and releases resources.
    /// </summary>
    public void Dispose()
    {
        Stop(); // Stop the server
        _tcpListener = null; // Release the TCP listener
    }
}

/// <summary>
/// Event arguments for WebSocket connection events.
/// </summary>
public class WebSocketConnectionEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the TCP client associated with the event.
    /// </summary>
    public TcpClient Client { get; set; }
}

/// <summary>
/// Event arguments for WebSocket message events.
/// </summary>
public class WebSocketServerMessageEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the text message (if the frame is a text frame).
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the binary data (if the frame is a binary frame).
    /// </summary>
    public byte[] BinaryData { get; set; }

    /// <summary>
    /// Gets or sets the TCP client that sent the message.
    /// </summary>
    public TcpClient Client { get; set; }
}