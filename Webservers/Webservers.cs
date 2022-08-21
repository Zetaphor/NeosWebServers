using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using EmbedIO;
using EmbedIO.WebApi;
using System;
using EmbedIO.Actions;
using EmbedIO.WebSockets;
using System.Threading.Tasks;
using EmbedIO.Files;
using Swan.Logging;
using System.Runtime.Remoting.Contexts;
using System.Collections.Generic;

namespace WebsocketServer
{

    public class Webservers : NeosMod
    {
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> ENABLE_WEBSERVER = new ModConfigurationKey<bool>("enable_webserver", "Enable the webserver (requires restart)", () => true);
        public static ModConfigurationKey<bool> ENABLE_WEBSOCKET_SERVER = new ModConfigurationKey<bool>("is_enabled", "Enables the Websocket server (requires restart)", () => true);

        public static ModConfiguration config;

        public override string Name => "WebServers";
        public override string Author => "Zetaphor";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/Zetaphor/NeosWebServers/";

        // Need to copy in EmbedIO an SwanLite manually with build

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            Engine.Current.RunPostInit(() => startWebServer());
            Harmony harmony = new Harmony("net.Zetaphor.Webservers");
            harmony.PatchAll();
        }

        private void startWebServer()
        {
            var url = "http://localhost:8080/";
            var cwd = System.IO.Directory.GetCurrentDirectory();
            Msg(cwd);

            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                // First, we will configure our web server by adding Modules.
                .WithLocalSessionManager()
                .WithModule(new WebSocketsEchoServer("/echo"))
                .WithModule(new WebSocketsChatServer("/chat"))
                .WithModule(new WebSocketsChatEchoServer("/chat-echo"))
                .WithModule(new WebSocketsChatIdServer("/chat-id"))
                .WithStaticFolder("/", cwd + "\\Webserver", true, m => m
                    .WithContentCaching(false)) // Add static files after other modules to avoid conflicts
                .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));

            server.StateChanged += (s, e) => Msg($"WebServer New State - {e.NewState}");

            server.RunAsync();
        }

        public class WebSocketsEchoServer : WebSocketModule
        {
            public WebSocketsEchoServer(string urlPath)
                : base(urlPath, true)
            {
                // placeholder
            }

            protected override Task OnMessageReceivedAsync(
                IWebSocketContext context,
                byte[] rxBuffer,
                IWebSocketReceiveResult rxResult)
                => SendAsync(context, Encoding.GetString(rxBuffer));
        }

        public class WebSocketsChatServer : WebSocketModule
        {
            public WebSocketsChatServer(string urlPath)
                : base(urlPath, true)
            {
                // placeholder
            }

            protected override Task OnClientConnectedAsync(IWebSocketContext context)
                => SendAsync(context, "connected");

            protected override Task OnMessageReceivedAsync(
                IWebSocketContext context,
                byte[] rxBuffer,
                IWebSocketReceiveResult rxResult)
                => BroadcastAsync(Encoding.GetString(rxBuffer), c => c != context);
        }

        public class WebSocketsChatEchoServer : WebSocketModule
        {
            public WebSocketsChatEchoServer(string urlPath)
                : base(urlPath, true)
            {
                // placeholder
            }

            protected override Task OnMessageReceivedAsync(
                IWebSocketContext context,
                byte[] rxBuffer,
                IWebSocketReceiveResult rxResult)
                => Task.WhenAll(
                    SendAsync(context, Encoding.GetString(rxBuffer)),
                    BroadcastAsync(Encoding.GetString(rxBuffer), c => c != context));
        }

        public class WebSocketsChatIdServer : WebSocketModule
        {
            public WebSocketsChatIdServer(string urlPath)
                : base(urlPath, true)
            {
                // placeholder
            }

            protected override Task OnClientConnectedAsync(IWebSocketContext context)
                => Task.WhenAll(
                    SendAsync(context, context.Id + "|connected"),
                    BroadcastAsync(context.Id + "|connected", c => c != context));

            protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
                => BroadcastAsync(context.Id + "|disconnected", c => c != context);

            protected override Task OnMessageReceivedAsync(
                IWebSocketContext context,
                byte[] rxBuffer,
                IWebSocketReceiveResult rxResult)
                    => Task.WhenAll(
                    SendAsync(context, context.Id + "|" + Encoding.GetString(rxBuffer)), 
                    BroadcastAsync(context.Id + "|" + Encoding.GetString(rxBuffer), c => c != context));
        }
    }
}

