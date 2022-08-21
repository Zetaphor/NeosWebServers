# Neos Web Servers

A [NeosModLoader](https://github.com/zkxs/NeosModLoader) mod for [Neos VR](https://neos.com/) 

This mod implements an HTTP web server and a Websockets server into the Neos client. This can be used to serve local copy of web apps rather than relying on a third party host and removes the need for a hosted websocket server.
This mod is largely targeted at developers who are interested in ensuring the backends for their web-enabled objects in-world can stand the test of time. Eventually domains go offline and free web hosts go down.
The web server serves content from a `Webserver` folder in the NeosVR installation root. There are also three websocket endpoints to enable a variety of different application needs.

## Installation
1. Install [NeosModLoader](https://github.com/zkxs/NeosModLoader).
2. Place [Webservers.dll](https://github.com/Zetaphor/NeosWebServers/releases/download/1.0/Webservers.dll), [EmbedIO.dll](https://github.com/Zetaphor/NeosWebServers/releases/download/1.0/EmbedIO.dll), and [Swan.Lite.dll](https://github.com/Zetaphor/NeosWebServers/releases/download/1.0/Swan.Lite.dll) into your `nml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\nml_mods` for a default install. You can create it if it's missing, or if you launch the game once with NeosModLoader installed it will create the folder for you.
3. Start the game. If you want to verify that the mod is working you can check your Neos logs.

## Web Server

When the Neos client is started the web server will begin hosting any content in the `Webserver` folder in the NeosVR root folder. 

This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\Webserver` for a default install. You will need to create the folder and add any content you want served.

The server can be accessed by navigating to http://localhost:8080/

## Websocket Server

There are three websocket endpoints, each behaving slightly differently to enable a wide range of applications.

### Echo Server

This is a simple echo server that will repeat back any messages it receives to the sender.

This endpoint can be accessed by connecting your websocket client to `ws://localhost:8080/echo`

### Chat Server

This endpoint is intended to act as a shared messaging bus. Any message sent to it will broadcast to all other connected clients. The sender does not receive a copy of the sent message.

Additionally all clients will receive a `connected` and `disconnected` message whenever a client opens or closes a connection. The `connected` message is sent to the connecting client as well.

This endpoint can be accessed by connecting your websocket client to `ws://localhost:8080/chat`

### Chat ID Server

This endpoint is similar to the Chat Server, except each client is assigned a unique identifier which is valid for the duration of the websocket connection.

This allows you to distinguish between different connected clients to enable more of a direct messaging system.

All clients will receive a `connected` and `disconnected` message whenever a client opens or closes a connection. The `connected` message is sent to the connecting client as well.

Each message is prefixed with the connection ID separated by a pipe operator ( | ) for easy parsing. 

Example:

```
3CqoZKBIWUyjmSITygRFZw|connected (Received on connection and broadcast to all other clients)
3CqoZKBIWUyjmSITygRFZw|Some example data (Received in reply to sending data and broadcast to all other clients)
o5MkHrV34k+5LfY6Q4zHag|I saw your data! (Received in reply to sending data and broadcast to all other clients)
```

This endpoint can be accessed by connecting your websocket client to `ws://localhost:8080/chat-id`
