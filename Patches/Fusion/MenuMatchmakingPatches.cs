/*
using FNPlus.Network;
using HarmonyLib;
using LabFusion.Menu;

namespace FNPlus.Patches
{
    [HarmonyPatch(typeof(MenuMatchmaking))]
    public class MenuMatchmakingPatches
    {
        private static Transform _cachedOptionsTransform;

        [HarmonyPostfix]
        [HarmonyPatch("PopulateOptions")]
        public static void PopulateOptionsPost(Transform optionsTransform)
        {
            _cachedOptionsTransform = optionsTransform;

            if (NetworkLayerDeterminer.LoadedTitle == "Riptide")
                RiptideNetworkLayer.CreateRiptideUIElements();
        }

        internal static void HideOptions()
        {
            if (_cachedOptionsTransform == null)
                return;

            var grid = _cachedOptionsTransform.Find("grid_Options");

            // Gamemode
            grid.Find("button_Gamemode").gameObject.SetActive(false);

            // Sandbox
            grid.Find("button_Sandbox").gameObject.SetActive(false);

            // Browse
            grid.Find("button_Browse").gameObject.SetActive(false);

            // Enter Code
            grid.Find("button_Code").GetComponent<RectTransform>().localPosition = new Vector3(400, -122, 0);
        }

        internal static void ShowOptions()
        {
            if (_cachedOptionsTransform == null)
                return;

            var grid = _cachedOptionsTransform.Find("grid_Options");

            // Gamemode
            grid.Find("button_Gamemode").gameObject.SetActive(true);

            // Sandbox
            grid.Find("button_Sandbox").gameObject.SetActive(true);

            // Browse
            grid.Find("button_Browse").gameObject.SetActive(true);

            // Enter Code
            grid.Find("button_Code").GetComponent<RectTransform>().localPosition = new Vector3(691, -122, 0);
        }
    }
}
*/