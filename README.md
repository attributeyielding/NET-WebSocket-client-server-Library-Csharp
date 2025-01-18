# WebSocket Client Implementation in C#

This repository contains a comprehensive implementation of a WebSocket Client and Server in C#. The client supports secure communication using `TcpClient` and `SslStream`, handles WebSocket frames (text, binary, ping, pong, and close), and includes features like custom headers, subprotocols, compression, and keep-alive mechanisms.

![image](https://github.com/user-attachments/assets/b9530e7a-6d95-4c18-8202-645709407999)

---
# WebSocket Client

## **Key Features**

1. **Secure Communication**:
   - Uses `SslStream` for secure WebSocket connections (`wss://`).
   - Supports server certificate validation (currently accepts all certificates for testing purposes).

2. **WebSocket Handshake**:
   - Generates a random `Sec-WebSocket-Key` and validates the server's `Sec-WebSocket-Accept` response.
   - Supports custom headers, subprotocols, and compression (`permessage-deflate`).

3. **Frame Handling**:
   - Supports text, binary, ping, pong, and close frames.
   - Masks client-to-server frames as required by the WebSocket protocol.

4. **Keep-Alive Mechanism**:
   - Sends periodic ping frames to keep the connection alive.
   - Monitors pong responses and closes the connection if a pong timeout occurs.

5. **Event-Driven Architecture**:
   - Provides events for connection closure, errors, ping/pong frames, and incoming messages.

6. **Thread Safety**:
   - Uses locks (`_pongLock`) to ensure thread-safe access to shared resources.

7. **Graceful Shutdown**:
   - Properly closes the connection by sending a close frame and disposing of resources.

---

## **Key Classes and Methods**

1. **`WebSocketClient`**:
   - Main class for managing the WebSocket connection.
   - Handles connection, handshake, frame sending/receiving, and error handling.

2. **`ConnectAsync`**:
   - Establishes a TCP connection, performs the SSL/TLS handshake, and initiates the WebSocket handshake.

3. **`HandshakeAsync`**:
   - Sends the WebSocket handshake request and validates the server's response.

4. **`SendAsync` and `SendBinaryAsync`**:
   - Sends text and binary messages to the server.

5. **`SendPingAsync`**:
   - Sends a ping frame to the server.

6. **`ReceiveAsync`**:
   - Listens for incoming data and processes WebSocket frames.

7. **`ProcessReceivedData`**:
   - Parses incoming frames and triggers appropriate events (e.g., `MessageReceived`, `PingReceived`).

8. **`CloseAsync`**:
   - Sends a close frame and gracefully shuts down the connection.

9. **`StartKeepAliveAsync`**:
   - Sends periodic ping frames to maintain the connection.

10. **`MonitorPongTimeoutAsync`**:
    - Monitors pong responses and closes the connection if a timeout occurs.

---

## **Events**

1. **`ConnectionClosed`**:
   - Triggered when the WebSocket connection is closed.
   - Provides the close code and reason.

2. **`ErrorOccurred`**:
   - Triggered when an error occurs (e.g., network issues, invalid frames).

3. **`PingReceived`**:
   - Triggered when a ping frame is received from the server.

4. **`PongReceived`**:
   - Triggered when a pong frame is received from the server.

5. **`MessageReceived`**:
   - Triggered when a text or binary message is received.

---


<!-- Doc 2 is in language en-US. Optimizing Doc 2 for scanning, using lists and bold where appropriate, but keeping language en-US, and adding id attributes to every HTML element: --><h1 id="jmh1rkf">WebSocket Server in C#</h1>
<p id="gzjspdzx">This is a comprehensive implementation of a <strong>WebSocket server</strong> in C# that supports <strong>secure communication</strong> using <code>TcpListener</code> and <code>SslStream</code>. The server handles various WebSocket frames, custom headers, subprotocols, and includes features like compression and keep-alive mechanisms.</p>

![image](https://github.com/user-attachments/assets/f41c15fd-5746-4914-bc45-ec94bd62e34c)


<hr id="z80m8fh">
<h2 id="k41dhm">Features</h2>
<ul id="k41dhm">
<li id="3mdlb86"><strong>Secure Communication</strong>: Supports SSL/TLS encryption using <code>SslStream</code> and an optional <code>X509Certificate2</code>.</li>
<li id="irk3x"><strong>WebSocket Protocol</strong>: Handles WebSocket handshake, text/binary frames, ping/pong frames, and close frames.</li>
<li id="q8csxxw"><strong>Custom Headers and Subprotocols</strong>: Allows custom headers and supports subprotocols during the handshake.</li>
<li id="d0vdzvg"><strong>Compression</strong>: Placeholder for WebSocket compression (permessage-deflate).</li>
<li id="qfdw80q"><strong>Keep-Alive Mechanism</strong>: Automatically responds to ping frames with pong frames.</li>
<li id="c9bskgd"><strong>Event-Driven Architecture</strong>: Provides events for connection opened, connection closed, and message received.</li>
<li id="6qxtcdiz"><strong>Logging</strong>: Integrates with <code>Microsoft.Extensions.Logging</code> for tracking server activity and errors.</li>
</ul>

---

## Usage

### Prerequisites
- .NET 5.0 or later.
- An SSL/TLS certificate (optional for secure communication).


<!-- Doc 2 is in language en-US. Optimizing Doc 2 for scanning, using lists and bold where appropriate, but keeping language en-US, and adding id attributes to every HTML element: --><h2 id="k5dam2o">Key Methods</h2>
<ul id="a26u7zf">
<li id="odh0p9"><strong id="tk8c5q">StartAsync</strong>: Starts the server and begins accepting incoming connections.</li>
<li id="fg1nik"><strong id="exgkoeq">Stop</strong>: Stops the server and cancels any ongoing tasks.</li>
<li id="d57v0bm"><strong id="ywz7omu">SendTextAsync</strong>: Sends a text message to a specific client.</li>
<li id="q4l5wyc"><strong id="c0hondy">SendBinaryAsync</strong>: Sends binary data to a specific client.</li>
<li id="l0qc9ay"><strong id="nst2qp">SendCloseFrameAsync</strong>: Sends a close frame to gracefully terminate the connection.</li>
<li id="xnnn6tg"><strong id="l7zivro">SendPongFrameAsync</strong>: Responds to a ping frame with a pong frame.</li>
</ul>
<hr id="i06aq">
<h2 id="lf1rrie">Event Handling</h2>
<ul id="9wpgxc">
<li id="j08zzg"><strong id="4mqrymq">ConnectionOpened</strong>: Triggered when a new WebSocket connection is established.</li>
<li id="4uqhiz"><strong id="odieey">ConnectionClosed</strong>: Triggered when a WebSocket connection is closed.</li>
<li id="0jh22t"><strong id="bys6xr9">MessageReceived</strong>: Triggered when a text or binary message is received from a client.</li>
</ul>
<hr id="vq8t1hj">
<h2 id="31bg7k">Security Considerations</h2>
<ul id="vbmx7tqj">
<li id="re3n3c"><strong>Ensure</strong> the SSL/TLS certificate is valid and properly configured.</li>
<li id="f8r42ff"><strong>Validate</strong> client input during the handshake to prevent injection attacks.</li>
<li id="lct45p"><strong>Use</strong> secure protocols (e.g., <code id="4bkeq8">Tls12</code> or <code id="u0mrlh">Tls13</code>) for encryption.</li>
</ul>
<hr id="jdcr7g">
<h2 id="sarm8ta">testing WebSocket server</h2>
      <li id="w0xRRR"> use POSTMAN to create a websocket request at localhost:8080 to connect to this server and test it OR use another websocket client (build client app first from this repos)</li>

<hr id="rkb95fi">
<h2 id="fnj8u0h">Future Enhancements</h2>
<ol start="1" id="dup9c3">
<li id="w0xwrr"><strong id="o9i3f5o">Compression</strong>: Implement permessage-deflate compression for reduced bandwidth usage.</li>
<li id="dbl1c9"><strong id="dxypmo6">Concurrency</strong>: Improve scalability by using asynchronous I/O and connection pooling.</li>
<li id="qxkdyik"><strong id="hdhsxvo">Custom Headers</strong>: Add support for custom headers during the WebSocket handshake.</li>
<li id="tg0j1li"><strong id="bo87tb">Testing</strong>: Add unit and integration tests to ensure reliability.</li>
</ol>
<hr id="ca72gs9">
<h2 id="o9fctyn">Author</h2>
<ul id="vxb9af">
<li id="eer90t"><strong id="jlc6pah">Bilel Mnasser</strong></li>
<li id="jxhnp7"><strong>Contact:</strong> <a href="mailto:personalhiddenmail@duck.com" target="_blank" rel="noreferrer" id="a6p8tfs">personalhiddenmail@duck.com</a></li>
<li id="vwwkexu"><strong>GitHub:</strong> <a href="https://github.com/attributeyielding" target="_blank" rel="noreferrer" id="akbslbj">attributeyielding</a></li>
<li id="n92wuk"><strong>Website:</strong> <a href="https://personal-website-resume.netlify.app/#contact" target="_blank" rel="noreferrer" id="71crpm">Personal Website/Resume</a></li>
</ul>
<hr id="298wtyi">
<h2 id="qkrmjx">License</h2>
<p id="alme41o">This project is licensed under the MIT License. See the <a href="https://chat.deepseek.com/a/chat/s/LICENSE" target="_blank" rel="noreferrer" id="x6ky1gc">LICENSE</a> file for details.</p>
<hr id="jgieglm">
<h2 id="c168bkb">Version</h2>
<ul id="fb3pr2f">
<li id="ndywkjk"><strong id="dslbicu">Version</strong>: 1.0</li>
<li id="yrqveg8"><strong id="ntz7oy">Date</strong>: January 2025</li>
</ul>






<!-- Doc 2 is in language en-US. Optimizing Doc 2 for scanning, using lists and bold where appropriate, but keeping language en-US, and adding id attributes to every HTML element: --><h2 id="r04lqw"><strong id="tv54p8t">Dependencies</strong></h2>
<p id="r04lqw">The following dependencies are required:</p>
<ul id="3sk75ti">
<li id="dqc0jd"><strong id="5ko12i">.NET Core/5+</strong>: The code uses <code id="tffssxr">Microsoft.Extensions.Logging</code> for logging and <code id="rx9f5of">System.Security.Cryptography</code> for hashing.</li>
<li id="ujjq9xh"><strong id="u5yqgir">SSL/TLS</strong>: Requires a valid SSL/TLS certificate for secure connections (<code id="bgjet6r">wss://</code>).</li>
</ul>
<hr id="us8fwm">
<h2 id="5b4jg1a"><strong id="buldwb">License</strong></h2>
<p id="9x84324">This project is licensed under the <strong>MIT License</strong>. See the <a href="https://chat.deepseek.com/a/chat/s/LICENSE" target="_blank" rel="noreferrer" id="4yfgatk">LICENSE</a> file for details.</p>
<hr id="gwqq1uv">
<h2 id="81miuuc"><strong id="gs1c8sf">Contributing</strong></h2>
<p id="h8ph407"><strong>Contributions are welcome!</strong> Please open an issue or submit a pull request for any improvements or bug fixes.</p>








