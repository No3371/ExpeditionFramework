using System;
using HarmonyLib;

namespace ExpeditionFramework.Game
{
    // The game has no hook for its night bag choice, so these three patches open the hands window with it.

    [HarmonyPatch(typeof(StoreResponse), nameof(StoreResponse.ShowAfterhourResponse))]
    internal static class BagMenuOpenPatch
    {
        private static void Postfix() => HandsWindow.BagMenuOpened();
    }

    [HarmonyPatch(typeof(ResponseList), nameof(ResponseList.CreateAfterhoursResponse))]
    internal static class BagMenuChoicesPatch
    {
        private static void Postfix(StoreResponse __result)
        {
            if (__result?.choices == null) return;
            for (int i = 0; i < __result.choices.Count; i++)
            {
                (GameItem item, Action action) = __result.choices[i];
                __result.choices[i] = (item, HandsWindow.Choice(action));
            }
        }
    }

    [HarmonyPatch(typeof(EmporiumEntry), nameof(EmporiumEntry.HideResponseWindow))]
    internal static class BagMenuClosePatch
    {
        private static void Postfix() => HandsWindow.ResponseClosed();
    }
}
