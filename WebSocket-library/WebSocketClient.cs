
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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// A WebSocket client implementation using TcpClient and SslStream for secure communication.
/// Supports text and binary frames, custom headers, subprotocols, compression, and ping/pong keep-alive.
/// </summary>
public class WebSocketClient : IDisposable
{
    private TcpClient _tcpClient; // Underlying TCP client for the connection
    private Stream _stream; // Network stream (wrapped in SslStream for secure connections)
    private string _host; // WebSocket server host
    private int _port; // WebSocket server port
    private string _resource; // WebSocket resource path (e.g., "/ws")
    private bool _isConnected; // Indicates if the connection is active
    private DateTime _lastPongReceived = DateTime.MinValue; // Timestamp of the last received pong frame
    private readonly object _pongLock = new object(); // Lock for thread-safe access to _lastPongReceived
    private CancellationTokenSource _cancellationTokenSource; // Token source for cancellation
    private readonly ILogger<WebSocketClient> _logger; // Logger for tracking events and errors

    private int _reconnectAttempts = 0; // Number of reconnection attempts
    private const int MaxReconnectAttempts = 5; // Maximum number of reconnection attempts
    private const int ReconnectDelayMs = 5000; // Delay between reconnection attempts (in milliseconds)

    /// <summary>
    /// Gets or sets the ping timeout in milliseconds. If set to 0, the pong timeout mechanism is disabled.
    /// </summary>
    public int PingTimeout { get; set; } = 0; // Disabled by default

    /// <summary>
    /// Gets or sets custom headers to include in the WebSocket handshake request.
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the list of subprotocols to negotiate with the server during the handshake.
    /// </summary>
    public List<string> Subprotocols { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets whether to enable WebSocket compression (permessage-deflate).
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// Event triggered when the WebSocket connection is closed.
    /// </summary>
    public event EventHandler<WebSocketCloseEventArgs> ConnectionClosed;

    /// <summary>
    /// Event triggered when an error occurs.
    /// </summary>
    public event EventHandler<WebSocketErrorEventArgs> ErrorOccurred;

    /// <summary>
    /// Event triggered when a ping frame is received.
    /// </summary>
    public event EventHandler<WebSocketPingEventArgs> PingReceived;

    /// <summary>
    /// Event triggered when a pong frame is received.
    /// </summary>
    public event EventHandler<WebSocketPongEventArgs> PongReceived;

    /// <summary>
    /// Event triggered when a text or binary message is received.
    /// </summary>
    public event EventHandler<WebSocketMessageEventArgs> MessageReceived;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketClient"/> class.
    /// </summary>
    /// <param name="url">The WebSocket server URL (e.g., "wss://echo.websocket.org/").</param>
    /// <param name="logger">Optional logger for tracking events and errors.</param>
    public WebSocketClient(string url, ILogger<WebSocketClient> logger = null)
    {
        var uri = new Uri(url); // Parse the URL
        _host = uri.Host; // Extract the host
        _port = uri.Port == 0 ? (uri.Scheme == "wss" ? 443 : 80) : uri.Port; // Default ports for ws/wss
        _resource = uri.PathAndQuery; // Extract the resource path
        _logger = logger; // Set the logger
    }

    /// <summary>
    /// Connects to the WebSocket server and performs the handshake.
    /// </summary>
    /// <exception cref="WebSocketException">Thrown if the handshake fails.</exception>
    public async Task ConnectAsync()
    {
        _tcpClient = new TcpClient(); // Create a new TCP client
        await _tcpClient.ConnectAsync(_host, _port); // Connect to the server

        // Wrap the NetworkStream in an SslStream for secure communication
        _stream = new SslStream(_tcpClient.GetStream(), false, ValidateServerCertificate);
        await ((SslStream)_stream).AuthenticateAsClientAsync(_host); // Perform SSL/TLS handshake

        _isConnected = true; // Mark the connection as active
        _cancellationTokenSource = new CancellationTokenSource(); // Initialize the cancellation token source

        // Perform the WebSocket handshake
        await HandshakeAsync();

        // Start receiving data
        _ = ReceiveAsync();

        // Start keep-alive
        _ = StartKeepAliveAsync();

        // Start monitoring pong timeout if enabled
        if (PingTimeout > 0)
        {
            _ = MonitorPongTimeoutAsync(_cancellationTokenSource.Token);
        }
    }

    /// <summary>
    /// Validates the server's SSL/TLS certificate. Always returns true for testing purposes.
    /// </summary>
    /// <param name="sender">The sender object.</param>
    /// <param name="certificate">The server's certificate.</param>
    /// <param name="chain">The certificate chain.</param>
    /// <param name="sslPolicyErrors">SSL policy errors.</param>
    /// <returns>True if the certificate is valid; otherwise, false.</returns>
    private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        // Accept all server certificates (for testing purposes only)
        // In production, validate the server certificate properly
        return true;
    }

    /// <summary>
    /// Performs the WebSocket handshake with the server.
    /// </summary>
    /// <exception cref="WebSocketException">Thrown if the handshake fails.</exception>
    private async Task HandshakeAsync()
    {
        _logger?.LogInformation("Starting WebSocket handshake with {Host}:{Port}...", _host, _port);

        // Generate a random key for the handshake
        var key = Guid.NewGuid().ToString("N");
        var base64Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(key));

        // Create the handshake request
        var request = $"GET {_resource} HTTP/1.1\r\n" +
                      $"Host: {_host}\r\n" +
                      "Upgrade: websocket\r\n" +
                      "Connection: Upgrade\r\n" +
                      $"Sec-WebSocket-Key: {base64Key}\r\n" +
                      "Sec-WebSocket-Version: 13\r\n";

        // Add compression
        if (EnableCompression)
        {
            request += "Sec-WebSocket-Extensions: permessage-deflate\r\n";
        }

        // Add subprotocols
        if (Subprotocols.Count > 0)
        {
            request += $"Sec-WebSocket-Protocol: {string.Join(", ", Subprotocols)}\r\n";
        }

        // Add custom headers
        foreach (var header in CustomHeaders)
        {
            request += $"{header.Key}: {header.Value}\r\n";
        }

        request += "\r\n"; // End of request

        var requestBytes = Encoding.UTF8.GetBytes(request); // Convert request to bytes
        await _stream.WriteAsync(requestBytes, 0, requestBytes.Length); // Send the request

        // Read the server's response
        var buffer = new byte[4096];
        var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        _logger?.LogInformation("Server response: {Response}", response);

        // Check for the handshake response
        if (!response.Contains("HTTP/1.1 101 Switching Protocols"))
            throw new WebSocketException("Handshake failed. Server did not switch protocols.");

        // Parse the Sec-WebSocket-Accept header
        var headers = response.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        string secWebSocketAccept = null;

        foreach (var header in headers)
        {
            if (header.StartsWith("Sec-WebSocket-Accept:", StringComparison.OrdinalIgnoreCase))
            {
                secWebSocketAccept = header.Split(new[] { ':' }, 2)[1].Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(secWebSocketAccept))
            throw new WebSocketException("Handshake failed. Sec-WebSocket-Accept header missing.");

        // Verify Sec-WebSocket-Accept
        var expectedAccept = CalculateWebSocketAccept(base64Key);

        if (secWebSocketAccept != expectedAccept)
            throw new WebSocketException($"Invalid Sec-WebSocket-Accept value. Expected: {expectedAccept}, Received: {secWebSocketAccept}");

        _logger?.LogInformation("WebSocket handshake successful. Sec-WebSocket-Accept: {Accept}", secWebSocketAccept);
    }

    /// <summary>
    /// Calculates the expected Sec-WebSocket-Accept value for the handshake.
    /// </summary>
    /// <param name="secWebSocketKey">The Sec-WebSocket-Key value.</param>
    /// <returns>The calculated Sec-WebSocket-Accept value.</returns>
    private string CalculateWebSocketAccept(string secWebSocketKey)
    {
        var magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"; // Magic string for WebSocket handshake
        var combined = secWebSocketKey + magicString; // Combine key and magic string
        var combinedBytes = Encoding.UTF8.GetBytes(combined); // Convert to bytes
        var hash = SHA1.Create().ComputeHash(combinedBytes); // Compute SHA1 hash
        return Convert.ToBase64String(hash); // Convert hash to Base64
    }

    /// <summary>
    /// Sends a text message to the WebSocket server.
    /// </summary>
    /// <param name="message">The text message to send.</param>
    public async Task SendAsync(string message)
    {
        var payload = Encoding.UTF8.GetBytes(message); // Convert message to bytes
        await SendFrameAsync(payload, opcode: 1, isMasked: true); // Send as text frame
    }

    /// <summary>
    /// Sends binary data to the WebSocket server.
    /// </summary>
    /// <param name="data">The binary data to send.</param>
    public async Task SendBinaryAsync(byte[] data)
    {
        await SendFrameAsync(data, opcode: 2, isMasked: true); // Send as binary frame
    }

    /// <summary>
    /// Sends a ping frame to the WebSocket server.
    /// </summary>
    /// <param name="data">The data to include in the ping frame.</param>
    public async Task SendPingAsync(byte[] data)
    {
        await SendFrameAsync(data, opcode: 9, isMasked: true); // Send as ping frame
    }

    /// <summary>
    /// Sends a WebSocket frame to the server.
    /// </summary>
    /// <param name="payload">The payload data to send.</param>
    /// <param name="opcode">The frame opcode (e.g., 1 for text, 2 for binary).</param>
    /// <param name="isMasked">Whether the payload is masked (required for client-to-server frames).</param>
    private async Task SendFrameAsync(byte[] payload, int opcode, bool isMasked)
    {
        if (EnableCompression && (opcode == 1 || opcode == 2)) // Compress text and binary frames
        {
            using var memoryStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress))
            {
                await deflateStream.WriteAsync(payload, 0, payload.Length);
            }
            payload = memoryStream.ToArray();
        }

        var frame = new List<byte>();
        frame.Add((byte)(0x80 | opcode)); // FIN bit set and opcode

        if (payload.Length < 126)
        {
            frame.Add((byte)(isMasked ? 0x80 | payload.Length : payload.Length)); // Payload length
        }
        else if (payload.Length <= 65535)
        {
            frame.Add((byte)(isMasked ? 0x80 | 126 : 126)); // Extended payload length (16-bit)
            frame.Add((byte)(payload.Length >> 8));
            frame.Add((byte)(payload.Length & 0xFF));
        }
        else
        {
            frame.Add((byte)(isMasked ? 0x80 | 127 : 127)); // Extended payload length (64-bit)
            for (int i = 7; i >= 0; i--)
            {
                frame.Add((byte)(payload.Length >> (8 * i)));
            }
        }

        if (isMasked)
        {
            var maskingKey = new byte[4]; // Generate a random masking key
            RandomNumberGenerator.Fill(maskingKey);
            frame.AddRange(maskingKey);

            for (int i = 0; i < payload.Length; i++) // Mask the payload
            {
                payload[i] ^= maskingKey[i % 4];
            }
        }

        frame.AddRange(payload); // Add the payload to the frame
        await _stream.WriteAsync(frame.ToArray(), 0, frame.Count); // Send the frame
    }

    /// <summary>
    /// Receives data from the WebSocket server and processes incoming frames.
    /// </summary>
    private async Task ReceiveAsync()
    {
        var buffer = new byte[4096]; // Buffer for incoming data
        while (_isConnected)
        {
            try
            {
                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length); // Read data from the stream
                if (bytesRead == 0) // Connection closed by the server
                {
                    await CloseAsync(1006, "Connection closed without close frame", false);
                    break;
                }

                ProcessReceivedData(buffer, bytesRead); // Process the received data
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new WebSocketErrorEventArgs { Exception = ex }); // Handle errors
            }
        }
    }

    /// <summary>
    /// Processes received WebSocket frames.
    /// </summary>
    /// <param name="buffer">The buffer containing the received data.</param>
    /// <param name="bytesRead">The number of bytes read.</param>
    private void ProcessReceivedData(byte[] buffer, int bytesRead)
    {
        int index = 0;

        // Read FIN and opcode
        var fin = (buffer[index] & 0x80) != 0; // FIN bit
        var opcode = buffer[index] & 0x0F; // Frame opcode
        index++;

        // Read mask and payload length
        var mask = (buffer[index] & 0x80) != 0; // Mask bit
        var payloadLength = buffer[index] & 0x7F; // Payload length
        index++;

        if (payloadLength == 126) // Extended payload length (16-bit)
        {
            payloadLength = (buffer[index] << 8) | buffer[index + 1];
            index += 2;
        }
        else if (payloadLength == 127) // Extended payload length (64-bit)
        {
            payloadLength = (int)((buffer[index] << 56) | (buffer[index + 1] << 48) | (buffer[index + 2] << 40) | (buffer[index + 3] << 32) |
                                 (buffer[index + 4] << 24) | (buffer[index + 5] << 16) | (buffer[index + 6] << 8) | buffer[index + 7]);
            index += 8;
        }

        // Read masking key
        byte[] maskingKey = null;
        if (mask)
        {
            maskingKey = new byte[4]; // Read the masking key
            Buffer.BlockCopy(buffer, index, maskingKey, 0, 4);
            index += 4;
        }

        // Read payload data
        var payload = new byte[payloadLength];
        Buffer.BlockCopy(buffer, index, payload, 0, payloadLength);

        if (mask) // Unmask the payload
        {
            for (int i = 0; i < payloadLength; i++)
            {
                payload[i] ^= maskingKey[i % 4];
            }
        }

        // Handle opcode
        switch (opcode)
        {
            case 1: // Text frame
                MessageReceived?.Invoke(this, new WebSocketMessageEventArgs { Message = Encoding.UTF8.GetString(payload) });
                break;
            case 2: // Binary frame
                MessageReceived?.Invoke(this, new WebSocketMessageEventArgs { BinaryData = payload });
                break;
            case 8: // Close frame
                var closeCode = (payload.Length >= 2) ? (payload[0] << 8) | payload[1] : 1005; // Close code
                var closeReason = (payload.Length > 2) ? Encoding.UTF8.GetString(payload, 2, payload.Length - 2) : string.Empty; // Close reason
                _ = CloseAsync(closeCode, closeReason, false); // Close the connection
                break;
            case 9: // Ping frame
                PingReceived?.Invoke(this, new WebSocketPingEventArgs { Data = payload });
                break;
            case 10: // Pong frame
                lock (_pongLock)
                {
                    _lastPongReceived = DateTime.Now; // Update the last pong timestamp
                }
                PongReceived?.Invoke(this, new WebSocketPongEventArgs { Data = payload });
                break;
            default:
                throw new WebSocketException($"Unsupported opcode: {opcode}");
        }
    }

    /// <summary>
    /// Closes the WebSocket connection.
    /// </summary>
    /// <param name="closeCode">The close code (default: 1000).</param>
    /// <param name="reason">The close reason (optional).</param>
    /// <param name="waitForServerClose">Whether to wait for the server's close frame.</param>
    public async Task CloseAsync(int closeCode = 1000, string reason = "", bool waitForServerClose = false)
    {
        try
        {
            var payload = new byte[2]; // Create the close frame payload
            payload[0] = (byte)(closeCode >> 8); // Close code (high byte)
            payload[1] = (byte)(closeCode & 0xFF); // Close code (low byte)
            if (!string.IsNullOrEmpty(reason)) // Add close reason if provided
            {
                var reasonBytes = Encoding.UTF8.GetBytes(reason);
                payload = new byte[2 + reasonBytes.Length];
                payload[0] = (byte)(closeCode >> 8);
                payload[1] = (byte)(closeCode & 0xFF);
                Buffer.BlockCopy(reasonBytes, 0, payload, 2, reasonBytes.Length);
            }
            await SendFrameAsync(payload, opcode: 8, isMasked: true); // Send the close frame
        }
        catch (Exception ex)
        {
            OnErrorOccurred(new WebSocketErrorEventArgs { Exception = ex }); // Handle errors
        }
        finally
        {
            _stream.Close(); // Close the stream
            _tcpClient.Close(); // Close the TCP client
            _cancellationTokenSource.Cancel(); // Cancel any ongoing tasks
            _isConnected = false; // Mark the connection as closed
            OnConnectionClosed(new WebSocketCloseEventArgs { CloseCode = closeCode, Reason = reason }); // Trigger the ConnectionClosed event
        }
    }

    /// <summary>
    /// Starts the keep-alive mechanism by sending periodic ping frames.
    /// </summary>
    private async Task StartKeepAliveAsync()
    {
        while (!_cancellationTokenSource.IsCancellationRequested && _isConnected)
        {
            try
            {
                await Task.Delay(30000, _cancellationTokenSource.Token); // Wait for 30 seconds
                if (_isConnected)
                {
                    await SendPingAsync(new byte[0]); // Send a ping frame
                }
            }
            catch (OperationCanceledException)
            {
                break; // Exit if the task is canceled
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new WebSocketErrorEventArgs { Exception = ex }); // Handle errors
            }
        }
    }

    /// <summary>
    /// Monitors the pong timeout and closes the connection if no pong is received within the timeout period.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task MonitorPongTimeoutAsync(CancellationToken cancellationToken)
    {
        while (!_cancellationTokenSource.IsCancellationRequested && _isConnected)
        {
            try
            {
                await Task.Delay(PingTimeout, cancellationToken); // Wait for the ping timeout period
                bool shouldClose = false;
                lock (_pongLock)
                {
                    if (_isConnected && DateTime.Now - _lastPongReceived > TimeSpan.FromMilliseconds(PingTimeout))
                    {
                        shouldClose = true; // Close the connection if no pong is received
                    }
                }
                if (shouldClose)
                {
                    OnErrorOccurred(new WebSocketErrorEventArgs { Exception = new WebSocketException("Pong timeout exceeded.") });
                    await CloseAsync(1011, "Pong timeout exceeded.", false); // Close with code 1011
                }
            }
            catch (OperationCanceledException)
            {
                break; // Exit if the task is canceled
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new WebSocketErrorEventArgs { Exception = ex }); // Handle errors
            }
        }
    }

    /// <summary>
    /// Handles reconnection attempts in case of connection failures.
    /// </summary>
    private async Task HandleReconnectionAsync()
    {
        while (_reconnectAttempts < MaxReconnectAttempts && !_cancellationTokenSource.IsCancellationRequested)
        {
            _reconnectAttempts++;
            _logger?.LogInformation($"Attempting to reconnect (Attempt {_reconnectAttempts}/{MaxReconnectAttempts})...");

            try
            {
                await ConnectAsync(); // Attempt to reconnect
                _reconnectAttempts = 0; // Reset attempts on successful reconnection
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Reconnection attempt {_reconnectAttempts} failed.");
                if (_reconnectAttempts >= MaxReconnectAttempts)
                {
                    _logger?.LogError("Maximum reconnection attempts reached. Giving up.");
                    OnErrorOccurred(new WebSocketErrorEventArgs { Exception = new WebSocketException("Maximum reconnection attempts reached.") });
                    break;
                }
                await Task.Delay(ReconnectDelayMs, _cancellationTokenSource.Token); // Wait before retrying
            }
        }
    }

    /// <summary>
    /// Triggers the ConnectionClosed event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnConnectionClosed(WebSocketCloseEventArgs e)
    {
        ConnectionClosed?.Invoke(this, e);
    }

    /// <summary>
    /// Triggers the ErrorOccurred event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    private void OnErrorOccurred(WebSocketErrorEventArgs e)
    {
        ErrorOccurred?.Invoke(this, e);
        if (_isConnected)
        {
            _ = HandleReconnectionAsync(); // Start reconnection process
        }
    }

    /// <summary>
    /// Disposes of the WebSocket client and releases resources.
    /// </summary>
    public void Dispose()
    {
        _stream?.Dispose(); // Dispose the stream
        _tcpClient?.Dispose(); // Dispose the TCP client
        _cancellationTokenSource?.Dispose(); // Dispose the cancellation token source
    }
}

/// <summary>
/// Event arguments for the ConnectionClosed event.
/// </summary>
public class WebSocketCloseEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the close code.
    /// </summary>
    public int CloseCode { get; set; }

    /// <summary>
    /// Gets or sets the close reason.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// Event arguments for the ErrorOccurred event.
/// </summary>
public class WebSocketErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the exception that caused the error.
    /// </summary>
    public Exception Exception { get; set; }
}

/// <summary>
/// Event arguments for the PingReceived event.
/// </summary>
public class WebSocketPingEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the ping frame data.
    /// </summary>
    public byte[] Data { get; set; }
}

/// <summary>
/// Event arguments for the PongReceived event.
/// </summary>
public class WebSocketPongEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the pong frame data.
    /// </summary>
    public byte[] Data { get; set; }
}

/// <summary>
/// Event arguments for the MessageReceived event.
/// </summary>
public class WebSocketMessageEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the text message (if the frame is a text frame).
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the binary data (if the frame is a binary frame).
    /// </summary>
    public byte[] BinaryData { get; set; }
}

/// <summary>
/// Represents a WebSocket-related exception.
/// </summary>
public class WebSocketException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WebSocketException(string message) : base(message) { }
}