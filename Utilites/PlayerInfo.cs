using Il2CppOculus.Platform;
using Il2CppOculus.Platform.Models;

using Steamworks;

namespace FNPlus.Utilities
{
    public class PlayerInfo
    {
        public static string PlayerIpAddress { get; private set; } = "Unknown";

        internal static void Initialize()
        {
            PlayerIpAddress = GetPlayerIPAddress();
            SetupPlayerUsername();
        }

        private static void SetupPlayerUsername()
        {
            // Steam
            if (UnityEngine.Application.dataPath.ToLower().Contains("steam"))
            {
                if (!SteamClient.IsValid)
                    SteamClient.Init(250820, false);

                LocalPlayer.Username = SteamClient.Name;
                SteamClient.Shutdown();
                return;
            }

            // Oculus
            if (!PlatformHelper.IsAndroid)
                Core.Initialize("5088709007839657");
            else
                Core.Initialize("4215734068529064");

            var request = Users.GetLoggedInUser();
            request.OnComplete((Message<User>.Callback)OnComplete);

            void OnComplete(Message<User> msg)
            {
                LocalPlayer.Username = msg.Data.OculusID;
            }
        }

        private static string GetPlayerIPAddress()
        {
            try
            {
                string link = "https://api.ipify.org";

                HttpClientHandler handler = new()
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                };

                HttpClient client = new(handler);

                HttpRequestMessage request = new()
                {
                    RequestUri = new Uri(link),
                    Method = HttpMethod.Get,
                };

                var response = client.Send(request);
                string responseString = response.Content.ReadAsStringAsync().Result;

                return responseString;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error when fetching IP address:");
                MelonLogger.Error(e);

                return string.Empty;
            }
        }
    }
}
