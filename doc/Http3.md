# Makes an HTTP/3 Request

To make an HTTP request following the HTTP/3 specification, several steps need to be completed between the client and server once a QUIC connection is established. The sequence diagram below illustrates how to send an HTTP request with the HTTP/3 specification to a server over a QUIC connection.

```mermaid
---
title: HTTP/3 Request
config:
  mirrorActors: false
  actorMargin: 100
---
sequenceDiagram

participant c as HTTP/3 Client
participant s as HTTP/3 Server

c -->> s: Establish a QUIC v1 connection

par negotiating peers settings
note over c,s : apply to an entire HTTP/3 connection
opt unidirectional control stream
c ->> s: sending client SETTINGS frame
end
opt unidirectional control stream
s ->> c: sending server SETTINGS frame
end
end

opt bidirectional request stream

note over c,s : client request
c ->> s: sending HEADERS frame
c ->> s: sending DATA frame

note over c, s: server response
s ->> c: sending HEADERS frame
s ->> c: sending DATA frame
end
```
