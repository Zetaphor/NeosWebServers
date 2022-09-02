using HarmonyLib;
using NeosModLoader;
using BaseX;
using FrooxEngine;
using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.WebSockets;
using System.Threading.Tasks;
using EmbedIO.Files;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using LZ4;
using Swan;
using System.ComponentModel;
using System.Net;
using static BaseX.FileUtil;
using System.IO.Compression;
using static NeosAssets.Graphics.LogiX;

namespace WebsocketServer
{

    /*
     * neosdb:///4443c45c5d292b28ce7af6b49aa9cc9e0be1fcfb07a722ad332b990b8f55a40b.7zbson
     * neosrec:///U-Zetaphor/R-47c73452-ec6e-4809-acdd-4a698cc7a23a
    */

    public class Webservers : NeosMod
    {
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> ENABLE_WEBSERVER = new ModConfigurationKey<bool>("enable_webserver", "Enable the webserver (requires restart)", () => true);
        public static ModConfigurationKey<bool> ENABLE_WEBSOCKET_SERVER = new ModConfigurationKey<bool>("is_enabled", "Enables the Websocket server (requires restart)", () => true);

        public static ModConfiguration config;

        public override string Name => "WebServers";
        public override string Author => "Zetaphor";
        public override string Version => "1.1.0";
        public override string Link => "https://github.com/Zetaphor/NeosWebServers/";

        public static string rootDir = Path.Combine(Directory.GetCurrentDirectory(), "web_apps");
        public static string appsDir = Path.Combine(rootDir, "apps");
        public static string installerCacheDir = Path.Combine(rootDir, "appInstallerCache");

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            Engine.Current.RunPostInit(() => initialize());
            Harmony harmony = new Harmony("net.Zetaphor.Webservers");
            harmony.PatchAll();
        }

        private void initialize()
        {
            if (setupDirectories())
            {
                startWebServer();
            } else
            {
                Msg("Failed to setup directories!");
            }
        }

        private static bool setupDirectories()
        {
            if (!Directory.Exists(rootDir))
            {
                Msg("Missing root directory: " + rootDir);
                return false;
            }

            if (!Directory.Exists(installerCacheDir))
            {
                try
                {
                    Directory.CreateDirectory(installerCacheDir);
                    Msg("Created installer cache directory: " + installerCacheDir);
                }
                catch (Exception ex)
                {
                    Msg("Error encountered creating installer cache directory: " + rootDir);
                    Msg("Error Message: " + ex.Message);
                    return false;
                }
            }

            if (!Directory.Exists(appsDir))
            {
                try
                {
                    Directory.CreateDirectory(appsDir);
                    Msg("Created apps directory: " + appsDir);
                }
                catch (Exception ex)
                {
                    Msg("Error encountered creating apps directory: " + appsDir);
                    Msg("Error Message: " + ex.Message);
                    return false;
                }
            }

            return true;
        }

        private void startWebServer()
        {
            var url = "http://localhost:8080/";

            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithLocalSessionManager()
                .WithModule(new WebSocketsAppServer("/app"))
                .WithModule(new WebSocketsEchoServer("/echo"))
                .WithModule(new WebSocketsChatServer("/chat"))
                .WithModule(new WebSocketsChatEchoServer("/chat-echo"))
                .WithModule(new WebSocketsChatIdServer("/chat-id"))
                .WithStaticFolder("/", rootDir, true, m => m
                    .WithContentCaching(false)) // Add static files after other modules to avoid conflicts
                .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));

            server.StateChanged += (s, e) => Msg($"WebServer New State - {e.NewState}");

            server.RunAsync();
        }

        private static string findDownloadUri(string baseUri) {
            var uri = new Uri(baseUri);

            if (uri.Scheme == "neosdb")
            {
                var ttask = Engine.Current.AssetManager.RequestGather(uri, FrooxEngine.Priority.Normal, null);
                ttask.AsTask().Wait();
                string text = ttask.Result;
                if (text != null && File.Exists(text))
                {
                    DataTreeDictionary node = DataTreeConverter.Load(text, uri);
                    var rootNode = node.TryGetDictionary("Object");
                    if (rootNode.TryGetDictionary("Name").TryGetNode("Data").LoadString() == "Holder")
                    {
                        rootNode = rootNode.TryGetList("Children").Children[0] as DataTreeDictionary;
                        node.Children["Object"] = rootNode;
                    }
                    var topLevel = rootNode.TryGetDictionary("Components").TryGetList("Data");

                    foreach (var dataNode in topLevel.Children)
                    {
                        var dictNode = (dataNode as DataTreeDictionary);
                        var str = dictNode.TryGetNode("Type").LoadString();

                        if (str == typeof(StaticBinary).ToString())
                        {
                            Msg("Found static binary");

                            DataTreeValue urlNode = dictNode.TryGetDictionary("Data").TryGetDictionary("URL").TryGetNode("Data") as DataTreeValue;
                            string url = urlNode.Value as string;
                            if (url.EndsWith(".zip"))
                            {
                                int uriFrom = url.IndexOf("@neosdb:///") + "@neosdb:///".Length;
                                int uriTo = url.LastIndexOf(".zip");

                                return url.Substring(uriFrom, uriTo - uriFrom);
                            }
                        }
                    }
                }
            }

            return "";
        }

        static private bool downloadZipInstall(string uri)
        {
            Msg("Got to the download part: " + uri);
            string filename = uri + ".zip";

            WebClient webClient = new WebClient();
            try
            {
                Msg(installerCacheDir);
                webClient.DownloadFile("https://cloudxstorage.blob.core.windows.net/assets/" + uri, Path.Combine(installerCacheDir, filename));
                Msg("Downloaded " + Path.Combine(installerCacheDir, filename));
                return true;
            }
            catch (Exception ex)
            {
                Msg("Failed to download " + Path.Combine(installerCacheDir, filename));
                Msg("Error Message: " + ex.Message);
                return false;
            }
        }

        static private Dictionary<string,string> previewInstall(string uri)
        {
            var zipContents = new Dictionary<string, string>();
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(Path.Combine(installerCacheDir, uri + ".zip")))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string filesize = "0 Bytes";

                        if (entry.Length >= 1048576)
                        {
                            decimal size = decimal.Divide(entry.Length, 1048576);
                            filesize = string.Format("{0:##.##} MB", size);
                        }
                        else if (entry.Length >= 1024)
                        {
                            decimal size = decimal.Divide(entry.Length, 1024);
                            filesize = string.Format("{0:##.##} KB", size);
                        }
                        else if (entry.Length > 0 & entry.Length < 1024)
  {
                            decimal size = entry.Length;
                            filesize = string.Format("{0:##.##} Bytes", size);
                        }

                        zipContents.Add(entry.FullName, filesize);
                    }
                }
            }
            catch(Exception ex)
            {
                Msg("Failed to preview installer archive: " + Path.Combine(installerCacheDir, uri + ".zip"));
                Msg("Error Message: " + ex.Message);
            }

            return zipContents;
        }

        static private bool installApp(string uri)
        {
            string appInstallDir = Path.Combine(appsDir, uri);
            try
            {
                Directory.CreateDirectory(appInstallDir);
                Msg("Created app directory: " + appInstallDir);
            }
            catch (Exception ex)
            {
                Msg("Error encountered creating app install directory: " + appInstallDir);
                Msg("Error Message: " + ex.Message);
                return false;
            }

            try
            {
                ZipFile.ExtractToDirectory(Path.Combine(installerCacheDir, uri + ".zip"), appInstallDir);
                return true;
            }
            catch (Exception ex)
            {
                Msg("Failed to unzip app to directory:" + appInstallDir);
                Msg("Error Message: " + ex.Message);
                return false;
            }
        }

        public class WebSocketsAppServer : WebSocketModule
        {
            public WebSocketsAppServer(string urlPath)
                : base(urlPath, true)
            {
                // placeholder
            }

            protected override Task OnMessageReceivedAsync(
                IWebSocketContext context,
                byte[] rxBuffer,
                IWebSocketReceiveResult rxResult)
            {
                string data = Encoding.GetString(rxBuffer);
                Msg("Received: " + data);
                string output = "";

                string downloadUri = findDownloadUri("neosdb:///85dcc22ba65d972ed7c7996d10b7abbc317517013d4d8e2c54d1693b76f0a9cd.7zbson");
                if (downloadUri.Length != 0)
                {
                    Msg("Found Download URI: " + downloadUri);
                    if (downloadZipInstall(downloadUri))
                    {
                        var zipContents = previewInstall(downloadUri);
                        Msg(zipContents.ToJson().ToString());
                        //installApp(downloadUri);
                    } 
                    else
                    {
                        output = "Failed to download asset, check logs";
                    }

                }
                else
                {
                    output = "Failed to locate asset";
                }

                return SendAsync(context, output);
            }
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

