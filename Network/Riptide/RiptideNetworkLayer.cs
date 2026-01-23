using FNPlus.Patches;
using FNPlus.Utilites;
using Il2CppTMPro;
using LabFusion.Menu;
using LabFusion.UI.Popups;
using Riptide;

namespace FNPlus.Network
{
    public class RiptideNetworkLayer : NetworkLayer
    {
        private IVoiceManager voiceManager;

        private static Transform _pingDisplayTransform = null;

        internal static TMP_Text PingDisplayTMP = null;

        internal static readonly ConcurrentQueue<Tuple<byte[], bool>> MessageQueue = new ConcurrentQueue<Tuple<byte[], bool>>();

        internal static readonly ConcurrentQueue<Action> ActionQueue = new ConcurrentQueue<Action>();

        public override string Title => "Riptide";

        public override string Platform => "P2P";

        public override IVoiceManager VoiceManager => voiceManager;

        public override bool IsHost => RiptideThreader.IsServerRunning;

        public override bool IsClient => RiptideThreader.IsClientConnected;

        private string ServerCode {
            get; set;
        }
        public override bool CheckSupported() => true;
        public override bool CheckValidation() => true;

        // Riptide doesn't really have a way to add these features out of the box...
        public override string GetUsername(ulong userId) => $"Riptide Enjoyer {userId}";
        public override bool IsFriend(ulong userId) => false;

        public override void LogIn() => InvokeLoggedInEvent();
        public override void LogOut() => InvokeLoggedOutEvent();

        public override void OnInitializeLayer()
        {
            RiptideThreader.StartThread();
            HookRiptideEvents();
            voiceManager = new UnityVoiceManager();
            voiceManager.Enable();
            CreateRiptideUIElements();
        }

        public override void OnDeinitializeLayer()
        {
            voiceManager.Disable();
            voiceManager = null;

            Disconnect();

            RiptideThreader.KillThread();

            UnhookRiptideEvents();

            RemoveRiptideUIElements();
        }

        private void HookRiptideEvents()
        {
            // Add server hooks
            MultiplayerHooking.OnPlayerJoined += OnPlayerJoin;
            MultiplayerHooking.OnPlayerLeft += OnPlayerLeave;
            MultiplayerHooking.OnDisconnected += OnDisconnect;
        }

        private void UnhookRiptideEvents()
        {
            // Remove server hooks
            MultiplayerHooking.OnPlayerJoined -= OnPlayerJoin;
            MultiplayerHooking.OnPlayerLeft -= OnPlayerLeave;
            MultiplayerHooking.OnDisconnected -= OnDisconnect;
        }

        static Transform pingDisplayTransform = null;
        internal static void CreateRiptideUIElements()
        {
            MenuMatchmakingPatches.HideOptions();

            if (!pingDisplayTransform)
            {
                pingDisplayTransform = GameObject.Instantiate(MenuCreator.MenuGameObject.transform.Find("page_Profile/text_Title"), MenuCreator.MenuGameObject.transform);
                PingDisplayTMP = pingDisplayTransform.GetComponent<TMP_Text>();

                pingDisplayTransform.localPosition = new Vector3(-330, 330, 0);
                PingDisplayTMP.text = "PING:";
                pingDisplayTransform.gameObject.SetActive(true);
            }
            else
                pingDisplayTransform.gameObject.SetActive(true);
        }

        internal static void RemoveRiptideUIElements()
        {
            MenuMatchmakingPatches.ShowOptions();

            if (pingDisplayTransform)
                pingDisplayTransform.gameObject.SetActive(false);
        }

        private void OnPlayerJoin(PlayerID id)
        {
            if (VoiceManager == null)
            {
                return;
            }

            if (!id.IsMe)
            {
                VoiceManager.GetSpeaker(id);
            }
        }

        private void OnPlayerLeave(PlayerID id)
        {
            if (VoiceManager == null)
            {
                return;
            }

            VoiceManager.RemoveSpeaker(id);
        }

        private void OnDisconnect()
        {
            VoiceManager.ClearManager();
        }
        public override void OnUpdateLayer()
        {
            for (int i = 0; i < MessageQueue.Count; i++)
            { 
                if (MessageQueue.TryDequeue(out Tuple<byte[], bool> messageTuple))
                {
                    byte[] bytes = messageTuple.Item1;
                    bool isServerHandled = messageTuple.Item2;
                    ReadableMessage message = new();
                    message.Buffer = bytes;
                    message.IsServerHandled = isServerHandled;
                    NativeMessageHandler.ReadMessage(message);
                }
            }

            for (int i = 0; i < ActionQueue.Count; i++)
                if (ActionQueue.TryDequeue(out Action action))
                    action();
        }

        public override void StartServer()
        {
            RiptideThreader.StartServer();
        }

        public override void Disconnect(string reason = "")
        {
            RiptideThreader.Disconnect();
        }

        public override string GetServerCode() => ServerCode;

        public override void RefreshServerCode()
        {
            ServerCode = IPUtils.EncodeIPAddress(Utilities.PlayerInfo.PlayerIpAddress);
            GUIUtility.systemCopyBuffer = ServerCode;

            Notifier.Send(new Notification()
            {
                SaveToMenu = false,
                Message = "Saved Code to Clipboard!",
                Type = NotificationType.INFORMATION,
                PopupLength = 3,
            });
        }

        public override void JoinServerByCode(string code)
        {
            RiptideThreader.ConnectToServer(code);
        }

        private static MessageSendMode GetSendMode(NetworkChannel channel)
        {
            return channel switch
            {
                NetworkChannel.Reliable => MessageSendMode.Reliable,
                NetworkChannel.Unreliable => MessageSendMode.Unreliable,
                _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
            };
        }

        public override void BroadcastMessage(NetworkChannel channel, NetMessage message)
        {
            byte[] data = message.ToByteArray();
            MessageSendMode sendMode = GetSendMode(channel);

            var messageTuple = new Tuple<byte[], MessageSendMode, ushort, bool>(data, sendMode, 0, true);

            RiptideThreader.ServerSendQueue.Enqueue(messageTuple);
        }

        public override void SendFromServer(byte userId, NetworkChannel channel, NetMessage message)
        {
            var id = PlayerIDManager.GetPlayerID(userId);

            if (id != null)
            {
                SendFromServer(id.PlatformID, channel, message);
            }
        }

        public override void SendFromServer(ulong userId, NetworkChannel channel, NetMessage message)
        {
            byte[] data = message.ToByteArray();
            MessageSendMode sendMode = GetSendMode(channel);

            var messageTuple = new Tuple<byte[], MessageSendMode, ushort, bool>(data, sendMode, (ushort)userId, false);

            RiptideThreader.ServerSendQueue.Enqueue(messageTuple);
        }

        public override void SendToServer(NetworkChannel channel, NetMessage message)
        {
            byte[] data = message.ToByteArray();
            MessageSendMode sendMode = GetSendMode(channel);

            var messageTuple = new Tuple<byte[], MessageSendMode>(data, sendMode);
            if (IsHost)
                NativeMessageHandler.ReadMessage(new() {
                    Buffer = data,
                    IsServerHandled =true,
                });
            else
                RiptideThreader.ClientSendQueue.Enqueue(messageTuple);
        }

        public override void DisconnectUser(ulong platformID) {
            RiptideThreader.KickPlayer(platformID);
        }
    }
}
