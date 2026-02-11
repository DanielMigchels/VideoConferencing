# VideoConferencing

[![Build](https://github.com/DanielMigchels/VideoConferencing/actions/workflows/dotnet-build.yml/badge.svg)](https://github.com/DanielMigchels/VideoConferencing/actions/workflows/dotnet-build.yml) [![Docker Hub](https://img.shields.io/docker/v/danielmigchels/videoconferencing?label=docker%20hub&logo=docker)](https://hub.docker.com/r/danielmigchels/videoconferencing)

A self-hostable video conferencing solution that does not rely on third-party SFU services or cloud dependencies.

<img style="width: 600px;" src="VideoConferencing.Docs/demo.gif">

# How does it work?

This project provides video calling without an SFU. The backend forwards RTP packets through the server to other clients. Media uses UDP. When running in Docker, publish a UDP range (for example `50000-50100/udp`). Set `HOST` to the IP address that other clients can reach (for example your LAN IP). This is used to advertise a host-reachable address for ICE.

<img style="width: 600px;" src="VideoConferencing.Docs/diagram.png">

## How to Run

Instructions for running the application.

### Docker Run
Pull the image from Docker Hub and run it locally.

Replace the IP address with the address of your host machine.
```bash
docker run -e HOST=192.168.1.100 -p 8443:8443 -p 50000-50100:50000-50100/udp danielmigchels/videoconferencing
```
The app listens on port 8443 and is available over HTTPS at `https://localhost:8443`.
The container uses a self-signed certificate; browser warnings can be ignored.

## Future Improvements

- **SDP renegotiation** for multi-user conversations.
- **Observability** (structured logs, metrics, health checks, basic call diagnostics).
- **Explore an SFU path** (e.g. mediasoup) for scalability and bandwidth efficiency.
