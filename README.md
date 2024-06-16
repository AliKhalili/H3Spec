# H3Spec: Conformance testing tool for HTTP/3

H3Spec is a conformance testing tool for HTTP/3 implementations. It sends various messages to an HTTP/3 server and verifies whether it adheres to the specifications accurately. The tool includes a number of test cases that are executed against different HTTP/3 servers, such as Kestrel, Cloudflare, Facebook, and others.

H3Spec is implemented on top of .NET's `System.Net.Quic`, which interoperates with [MsQuic](https://github.com/microsoft/msquic). It also draws inspiration from the ASP.NET Core Kestrel HTTP/3 implementation.

For more detailed information on how this tool works, you can refer to the blog post [**"Hands-On HTTP/3 with .NET: Creating a Conformance Testing Tool from Scratch"**](https://medium.com/@Alikhalili/hands-on-http-3-with-net-fcd38cf7ad05).

## Quick Start

### Requirement

To build and run H3Spec, you need to first ensure that your system meets the requirements outlined in [the MsQuic documentation](https://github.com/microsoft/msquic/blob/main/docs/Platforms.md).

Additionally, make sure you have installed the .NET 9 SDK on your system. Please note that .NET 9 is still in preview version. If you prefer not to install a preview version, you can manually modify the code to target .NET 8.

### Run Local HTTP/3 Kestrel Server

To test the HTTP/3 implementation in .NET Kestrel, we need to run a lightweight ASP.NET Core application on your local machine. This application supports the HTTP/3 protocol and listens on `localhost:6001`. You can start the application by running the following command:

```bash
cd src\h3server
dotnet run
```

### Run H3Spec

To run the tool, begin by amending the `appsettings.json` file to specify the HTTP/3 servers. It already includes some server configurations from Cloudflare, Facebook, and other well-known HTTP/3 implementations. Then, execute the following command to run the conformance testing tool:

```bash
cd src\h3spec
dotnet run
```

The tool will sequentially execute each test scenario and display its results on the console. Below is an example of the output obtained from running the tool against Kestrel:

```text
----------------   .NET 9.0 Kestrel HTTP/3 server   ----------------
✅  4.1-7: Receipt invalid sequence of frames.
✅  4.3-4: Pseudo-header MUST appear before regular header fields
❌  4.3.1-2.10.1: The authority MUST NOT include the deprecated userinfo subcomponent.
   expected: Http3ErrorCode.MessageError(000000000000010E) is expected.
   actual: An exception is thrown by the connection, but it application protocol error code is
Http3ErrorCode.ProtocolError(0000000000000101).
❌  4.3.1-2.16.1: The path pseudo-header field MUST NOT be empty for "http" or "https" URIs;
   expected: Http3ErrorCode.MessageError(000000000000010E) is expected.
   actual: An exception is thrown by the connection, but it application protocol error code is
Http3ErrorCode.ProtocolError(0000000000000101).
❌  4.4: The CONNECT Method.
   expected: A 2xx series status code is expected.
   actual: The status code is 405.
✅  6.2-7: Unknown stream types MUST NOT be considered a connection error.
✅  6.2.1-2: Missing SETTINGS frame in the first frame of the control stream.
❌  6.2.2-3: Client-initiated push stream received by the server.
   expected: Http3ErrorCode.StreamCreationError(0000000000000103) is expected.
   actual: No exception is thrown by the connection.
❌  7.1-5: Frame payload contains additional bytes after the identified fields.
   expected: Http3ErrorCode.FrameError(0000000000000106) is expected.
   actual: No exception is thrown by the connection.
✅  7.2.4-2: A SETTINGS MUST NOT be sent subsequently.
✅  7.2.4.1-5: Reserved settings MUST NOT be sent.
❌  7.2.8-3: HTTP/2 reserved frame types MUST be treated as a connection error.
   expected: Http3ErrorCode.UnexpectedFrame(0000000000000105) is expected.
   actual: An exception is thrown by the connection, but it application protocol error code is
Http3ErrorCode.MissingSettings(000000000000010A).
 Number of 6 tests passed from total number of 12.
```
