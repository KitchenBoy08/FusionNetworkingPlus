using FNPlus.Utilites;
using LabFusion.UI.Popups;
using Riptide.Utils;
using System.Collections.Generic;

namespace FNPlus.Network.Riptide {
    public class RiptideMatchmaker : IMatchmaker {
        public LanDiscovery LanDiscovery { get; private set; } = new LanDiscovery(314, 7778);
        public void Start() {
            LanDiscovery = new(314, 25000);
            LanDiscovery.Mode = BroadcastMode.Idle;
            LanDiscovery.BroadcastPort = 25000;
            LanDiscovery.Bind();
            MelonEvents.OnFixedUpdate.Subscribe(LanDiscovery.Tick);
            LanDiscovery.HostDiscovered += OnHostDiscovered;
        }
        public void Kill() {
            try {
                addresses.Clear();
                callbacks.Clear();
                MelonEvents.OnFixedUpdate.Unsubscribe(LanDiscovery.Tick);
                LanDiscovery.HostDiscovered -= OnHostDiscovered;
                LanDiscovery.Stop();
                LanDiscovery = null;
            }
            catch {

            }
        }
        public void Server() => LanDiscovery.StartListening();

        public void RequestLobbies(Action<IMatchmaker.MatchmakerCallbackInfo> callback) {
            if (LanDiscovery.Mode == BroadcastMode.Broadcasting) {
                Notifier.Send(new() {
                    Message = "Already hosting a lobby, cannot search for lobbies!", });
                return;
            }
            addresses.Clear();
            LanDiscovery.SendBroadcast();
            if (callbacks.Contains(callback))
                return;
            callbacks.Add(callback);
        }
        public List<Action<IMatchmaker.MatchmakerCallbackInfo>> callbacks = new();
        public void RequestLobbies(MatchmakerFilters filters, Action<IMatchmaker.MatchmakerCallbackInfo> callback) {
            RequestLobbies(callback);
            MelonLogger.Error("REQUEST LOBBIES BY FILTERS NOT IMPLEMENTED FOR RIPTIDE");
        }
        public List<string> addresses = new List<string>();
        private void OnHostDiscovered(object sender, HostDiscoveredEventArgs e) {
            addresses.Add(e.HostIP.ToString());
            var netLobbies = new List<IMatchmaker.LobbyInfo>();
            foreach (var address in addresses) {
                var lobby = new IMatchmaker.LobbyInfo() {
                    Lobby = new RiptideLobby() { address = address },
                    Metadata = new()
                };
                netLobbies.Add(lobby);
            }
            var info = new IMatchmaker.MatchmakerCallbackInfo() {
                Lobbies = netLobbies.ToArray(),
            };

            callbacks.ForEach(c => c.Invoke(info));
        }

        public void RequestLobbiesByCode(string code, Action<IMatchmaker.MatchmakerCallbackInfo> callback) {
            RequestLobbies(callback);
            MelonLogger.Error("REQUEST LOBBIES BY CODE NOT IMPLEMENTED FOR RIPTIDE");
        }
    }
}
