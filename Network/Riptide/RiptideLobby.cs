namespace FNPlus.Network.Riptide {
    public class RiptideLobby : NetworkLobby {
        public string address;
        public override void SetMetadata(string key, string value) {
        }

        public override bool TryGetMetadata(string key, out string value) {
            value = "";
            return false;
        }

        public override string GetMetadata(string key) {
            return "";
        }

        public override Action CreateJoinDelegate(string lobbyId) {
            if (NetworkLayerManager.Layer is RiptideNetworkLayer layer) {
                return () =>
                {
                    layer.JoinServerByCode(lobbyId);
                };
            }

            return null;
        }
    }
}
