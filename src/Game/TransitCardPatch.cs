using HarmonyLib;
using TMPro;

namespace ExpeditionFramework.Game
{
    // The game writes its fixed "unavailable" label under an unavailable card's picture and offers no hook for the text,
    // so this patch rewrites that line with what the area is missing. Putting it in the title instead overflows the
    // card header and leaks into the run summary, which also reads the title.
    [HarmonyPatch(typeof(TransitUI), "BuildCard")]
    internal static class TransitCardPatch
    {
        private static void Postfix(AfterhourLocation location, TransitCardView __result)
        {
            if (__result == null || __result.available || !(location is ExpeditionLocation area)) return;
            TextMeshProUGUI status = __result.transform.Find("Status")?.GetComponent<TextMeshProUGUI>();
            if (status == null) return;
            string text = area.MissingStatus(status.text);
            if (text == null) return;
            status.text = text;
            WildNetworkUI.AutoSize(status, 12f);
        }
    }
}
