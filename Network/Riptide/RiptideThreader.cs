using System.Net;
using System.Threading;
using FNPlus.Utilites;
using LabFusion.Senders;
using LabFusion.UI.Popups;
using Riptide;
using Riptide.Utils;

namespace FNPlus.Network
{
    internal class RiptideThreader
    {
        private static Thread _riptideThread;

        /// <summary>
        /// Starts the Riptide Thread and begins polling messages/actions.
        /// </summary>
        internal static void StartThread()
        {
            _riptideThread = new Thread(RiptideThread);

            if (_riptideThread.IsAlive)
                return;

            _isThreadAlive = true;

            _riptideThread.IsBackground = true;
            _riptideThread.Start();
        }

        /// <summary>
        /// Stops the Riptide Thread from running and clears all events in the action queue.
        /// </summary>
        internal static void KillThread()
        {
            _isThreadAlive = false;
        }

        // ANYTIME these are accessed outside of the Riptide Thread, please, please, please lock it, future people. (You shouldn't do that anyway but still)
        private static Client _riptideClient;
        private static Server _riptideServer;

        internal static readonly ConcurrentQueue<Tuple<byte[], MessageSendMode>> ClientSendQueue = new();
        internal static readonly ConcurrentQueue<Tuple<byte[], MessageSendMode, ushort, bool>> ServerSendQueue = new();

        private static bool _isThreadAlive = false;
        private static void RiptideThread()
        {
            InitalizeRiptide();

            int tickCounter = 0;

            while (_isThreadAlive)
            {
                try
                {
                    _riptideClient.Update();
                    _riptideServer.Update();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to update Riptide with exception: {ex}");
                }

                while (!ClientSendQueue.IsEmpty)
                {
                    if (ClientSendQueue.TryDequeue(out Tuple<byte[], MessageSendMode> clientMessageTuple))
                    {
                        byte[] messageData = clientMessageTuple.Item1;
                        MessageSendMode sendMode = clientMessageTuple.Item2;

                        try
                        {
                            Message message = Message.Create(sendMode, 0);
                            message.AddBytes(messageData);

                            _riptideClient.Send(message);
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"Failed to send message with exception: {ex}");
                        }
                    }
                }

                while (!ServerSendQueue.IsEmpty)
                {
                    if (ServerSendQueue.TryDequeue(out Tuple<byte[], MessageSendMode, ushort, bool> serverMessageTuple))
                    {
                        byte[] messageData = serverMessageTuple.Item1;
                        MessageSendMode sendMode = serverMessageTuple.Item2;
                        ushort ID = serverMessageTuple.Item3;
                        bool isBroadcast = serverMessageTuple.Item4;

                        try
                        {
                            Message message = Message.Create(sendMode, 0);
                            message.AddBytes(messageData);

                            if (isBroadcast && IsServerRunning)
                                _riptideServer.SendToAll(message);
                            else if (isBroadcast && !IsServerRunning)
                                _riptideClient.Send(message);
                            else
                                _riptideServer.Send(message, ID);
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"Failed to send message with exception: {ex}");
                        }
                    }
                }

                // Poll less important things
                tickCounter++;
                if (tickCounter >= 300)
                {
                    tickCounter = 0;

                    short ping = 0;
                    if (IsClientConnected)
                        ping = _riptideClient.RTT;
                    RiptideNetworkLayer.ActionQueue.Enqueue(new Action(() =>
                    {
                        // Hide if not in server
                        if (ping <= 0)
                        {
                            RiptideNetworkLayer.PingDisplayTMP.text = "";
                            return;
                        }

                        // Set ping
                        RiptideNetworkLayer.PingDisplayTMP.text = $"PING: {ping}";

                        // Connection quality colors
                        if (ping < 100)
                            RiptideNetworkLayer.PingDisplayTMP.color = Color.cyan;
                        else if (ping < 200)
                            RiptideNetworkLayer.PingDisplayTMP.color = Color.yellow;
                        else
                            RiptideNetworkLayer.PingDisplayTMP.color = Color.red;
                    }));
                }
            }

            DeinitializeRiptide();
        }

        private static void InitalizeRiptide()
        {
#if DEBUG
            RiptideLogger.Initialize(MelonLogger.Msg, true);
#endif
            // Will a couple voice chat packets be lost? Maybe. I don't care, it's better than nothing!
            Message.MaxPayloadSize = 60000;

            _riptideClient = new("RIPTIDE CLIENT");
            _riptideServer = new("RIPTIDE SERVER");

            _riptideClient.MessageReceived += OnClientReceives;
            _riptideServer.MessageReceived += OnServerReceives;

            _riptideClient.Disconnected += OnDisconnected;
            _riptideServer.ClientDisconnected += OnClientDisconnected;
            _riptideServer.ClientConnected += OnClientConnected;
        }

        private static void OnDisconnected(object sender, global::Riptide.DisconnectedEventArgs e)
        {
            RiptideNetworkLayer.ActionQueue.Enqueue(new Action(() => NetworkHelper.Disconnect()));
        }

        private static void OnClientConnected(object sender, ServerConnectedEventArgs e)
        {
            e.Client.CanQualityDisconnect = false;
        }

        private static void OnClientDisconnected(object sender, ServerDisconnectedEventArgs e)
        {
            ushort ID = e.Client.Id;

            RiptideNetworkLayer.ActionQueue.Enqueue(new Action(() =>
            {
                if (ID == PlayerIDManager.LocalID)
                    return;

                // Make sure the user hasn't previously disconnected
                if (PlayerIDManager.HasPlayerID(ID.ToString()))
                {
                    // Update the mod so it knows this user has left
                    InternalServerHelpers.OnPlayerLeft(ID.ToString());

                    // Send disconnect notif to everyone
                    ConnectionSender.SendDisconnect(ID.ToString());
                }
            }));
        }

        private static void DeinitializeRiptide()
        {
            _riptideClient = null;
            _riptideServer = null;
        }

        internal static bool IsServerRunning { get; private set; }
        internal static bool IsClientConnected { get; private set; }

        public static void StartServer()
        {
            lock (_riptideClient)
            {
                lock (_riptideServer)
                {
                    _riptideServer.Start(7777, 256, 0, false);

                    _riptideClient.Connected += OnConnect;
                    _riptideClient.Connect("127.0.0.1:7777", 5, 0, null, false);

                    void OnConnect(object sender, EventArgs args)
                    {
                        _riptideClient.Connected -= OnConnect;

                        _riptideClient.TimeoutTime = 30000;
                        _riptideClient.Connection.CanQualityDisconnect = false;
                        _riptideServer.TimeoutTime = 30000;

                        IsServerRunning = true;
                        IsClientConnected = true;

                        RiptideNetworkLayer.ActionQueue.Enqueue(() =>
                        {
                            PlayerIDManager.SetPlatformID(_riptideClient.Id.ToString());

                            InternalServerHelpers.OnStartServer();
                        });
                    }
                }
            }
        }

        public static void StopServer()
        {
            lock (_riptideServer)
            {
                _riptideServer.Stop();

                IsServerRunning = false;
                IsClientConnected = false;
            }
        }

        private static bool _isConnecting;
        public static void ConnectToServer(string serverCode)
        {
            if (_isConnecting)
            {
                Notifier.Send(new Notification()
                {
                    Message = "Client is still connecting! Please wait until connection is finalized or failed.",
                    PopupLength = 5,
                    SaveToMenu = false,
                    Type = NotificationType.WARNING,
                });
                return;
            }

            if (IsClientConnected)
            {
                Notifier.Send(new Notification()
                {
                    Message = "Disconnect from the current server before connecting to another!",
                    PopupLength = 5,
                    SaveToMenu = false,
                    Type = NotificationType.WARNING,
                });
                return;
            }

            string ipString;

            if (IPAddress.TryParse(serverCode, out IPAddress ipAddress))
                ipString = serverCode;
            else if (IPUtils.DecodeIPAddress(serverCode) != string.Empty)
                ipString = IPUtils.DecodeIPAddress(serverCode);
            else
            {
                Notifier.Send(new Notification()
                {
                    Message = "Server Code or IP Address incorrect! Make sure you used the right code/IP!",
                    PopupLength = 5,
                    SaveToMenu = false,
                    Type = NotificationType.WARNING,
                });
                return;
            }

            lock (_riptideClient)
            {
                _riptideClient.Connected += OnConnect;
                _riptideClient.ConnectionFailed += OnConnectionFailed;

                _riptideClient.Connect($"{ipString}:7777", 5, 0, null, false);
                _isConnecting = true;

                void OnConnect(object sender, EventArgs args)
                {
                    _riptideClient.Connected -= OnConnect;
                    _riptideClient.ConnectionFailed -= OnConnectionFailed;

                    _isConnecting = false;

                    _riptideClient.TimeoutTime = 30000;
                    _riptideClient.Connection.CanQualityDisconnect = false;

                    IsClientConnected = true;

                    RiptideNetworkLayer.ActionQueue.Enqueue(() =>
                    {
                        PlayerIDManager.SetPlatformID(_riptideClient.Id.ToString());

                        ConnectionSender.SendConnectionRequest();
                    });
                }

                void OnConnectionFailed(object sender, ConnectionFailedEventArgs args)
                {
                    _riptideClient.Connected -= OnConnect;
                    _riptideClient.ConnectionFailed -= OnConnectionFailed;

                    _isConnecting = false;

                    Notifier.Send(new Notification()
                    {
                        Title = "Failed to Connect!",
                        Message = $"REASON: {args.Reason}",
                        SaveToMenu = false,
                        PopupLength = 5,
                        Type = NotificationType.ERROR,
                    });
                }
            }
        }

        public static void Disconnect(string reason = "")
        {
            lock (_riptideClient)
            {
                lock (_riptideServer)
                {
                    if (!IsClientConnected)
                        return;

                    _riptideServer.Stop();
                    _riptideClient.Disconnect();

                    IsClientConnected = false;
                    IsServerRunning = false;

                    InternalServerHelpers.OnDisconnect(reason);
                }
            }
        }

        private static void OnServerReceives(object sender, MessageReceivedEventArgs e)
        {
            RiptideNetworkLayer.MessageQueue.Enqueue(new Tuple<byte[], bool>(e.Message.GetBytes(), true));
        }

        private static void OnClientReceives(object sender, MessageReceivedEventArgs e)
        {
            RiptideNetworkLayer.MessageQueue.Enqueue(new Tuple<byte[], bool>(e.Message.GetBytes(), false));
        }

        internal static void KickPlayer(string platformID) {
            lock (_riptideServer) {
                _riptideServer.DisconnectClient(ushort.Parse(platformID));
            }
        }
    }
}
