using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// Two hand slots that open beside the game's bag choice, for what to carry without a bag. The items wait here and move
    /// into the night screen's hands on arriving anywhere that shows them. Closing the menu, coming home from the map or
    /// from a place without hands, or going to bed puts them back in the inventory, since the game doesn't save this window.
    /// </summary>
    internal static class HandsWindow
    {
        private static PixelWindow window;
        private static GameSlotInventory left;
        private static GameSlotInventory right;
        private static bool menuOpen;
        private static bool choosing;

        public static void Start()
        {
            ModHook.OnAfterhourArriveEarly += Arriving;
            ModHook.OnReturningStoreLate += PutBack;
            ModHook.OnGoingSleepEarly += PutBack;
            ModHook.OnShutterOpenedEarly += PutBack;
        }

        public static void Stop()
        {
            ModHook.OnAfterhourArriveEarly -= Arriving;
            ModHook.OnReturningStoreLate -= PutBack;
            ModHook.OnGoingSleepEarly -= PutBack;
            ModHook.OnShutterOpenedEarly -= PutBack;
            PutBack();
        }

        /// <summary>The game's bag choice opened.</summary>
        public static void BagMenuOpened()
        {
            menuOpen = true;
            Show();
        }

        /// <summary>Runs a bag choice, which closes the menu on its way to the map, so the items stay for the trip.</summary>
        public static Action Choice(Action action)
        {
            return () =>
            {
                choosing = true;
                try { action?.Invoke(); }
                finally { choosing = false; }
            };
        }

        /// <summary>Some response window closed. If it was the bag choice and no bag was picked, the trip is off.</summary>
        public static void ResponseClosed()
        {
            if (!menuOpen) return;
            menuOpen = false;
            if (choosing) Hide();
            else PutBack();
        }

        // This has to be the early hook: on the way out the game closes the map through its "back in the store" path,
        // which fires OnReturningStore and would put everything back before the later hooks run.
        private static void Arriving(string locationId)
        {
            try
            {
                AfterhourLocation location = ModHelper.GetAfterhourLocation(locationId);
                if (location != null && location.UsesAfterhourBag) MoveIntoHands();
            }
            catch (Exception ex)
            {
                ModEntry.Log.Warning("Could not put the items in hand: " + ex.Message);
            }
        }

        /// <summary>The items held, without what's inside them.</summary>
        public static List<GameItem> Held()
        {
            return Slots().Select(slot => slot.childItem).Where(item => item != null && GeneralHelper.IsItemOwned(item)).ToList();
        }

        /// <summary>The items held and everything inside them.</summary>
        public static List<GameItem> Contents()
        {
            return Slots().SelectMany(slot => GraphUtils.FindAllChildrenType<GameItem>(slot)).Where(GeneralHelper.IsItemOwned).ToList();
        }

        /// <summary>Moves what's held into the night screen's hands. Anything that won't go goes back to the inventory.</summary>
        public static void MoveIntoHands()
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null || left == null) return;
            Move(left, entry.afterhourPocketSlotInvLeft);
            Move(right, entry.afterhourPocketSlotInvRight);
        }

        private static void Move(GameSlotInventory from, GameSlotInventory to)
        {
            GameItem item = from.childItem;
            if (item == null) return;
            if (to == null || to.childItem != null || GraphUtils.TryAccept(to, item) == -1) EmporiumEntry.Instance.TryAddToPlayerInv(item);
        }

        private static IEnumerable<GameSlotInventory> Slots()
        {
            return new[] { left, right }.Where(slot => slot != null);
        }

        private static void Show()
        {
            try
            {
                if (EmporiumEntry.Instance == null || !PackRegistry.HasAreas) return;
                if (window == null) Build();
                window.Show();
                Place();
                window.Validate();
                window.ToFront();
            }
            catch (Exception ex)
            {
                ModEntry.Log.Warning("Could not show the hands window: " + ex.Message);
            }
        }

        private static void Hide()
        {
            if (window != null) window.Hide();
        }

        private static void PutBack()
        {
            try
            {
                EmporiumEntry entry = EmporiumEntry.Instance;
                if (entry != null)
                {
                    foreach (GameItem item in Slots().Select(slot => slot.childItem).Where(item => item != null).ToList()) entry.TryAddToPlayerInv(item);
                }
            }
            catch (Exception ex)
            {
                ModEntry.Log.Warning("Could not return the items in hand: " + ex.Message);
            }
            Hide();
        }

        private static void Build()
        {
            left = Slot("item_expedition_box_label_pocket1");
            right = Slot("item_expedition_box_label_pocket2");
            window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.Panel(LocHelper.GetLocalizedItem("item_expedition_box_label_pocket1"), left, "pocket1"),
                MachineHelper.Panel(LocHelper.GetLocalizedItem("item_expedition_box_label_pocket2"), right, "pocket2"));
            window.SetTitle(ModEntry.L("ef_hands_title"));
            window.Validate();
        }

        private static GameSlotInventory Slot(string label)
        {
            var slot = new GameSlotInventory();
            slot.SetTooltipName("[" + LocHelper.GetLocalizedItem(label) + "]");
            slot.SetBackgroundFadeSprite("Items/items_container", "pouch");
            ContainerHelper.AllowOnlyOwnedItems(slot);
            return slot;
        }

        // Just under the bag choice, centred on it.
        private static void Place()
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            PixelWindow menu = entry.responseWindowClosable;
            if (menu == null || !menu.IsVisible()) menu = entry.responseWindow;
            if (menu == null) return;
            Vector2 at = menu.rectTransform.anchoredPosition;
            window.rectTransform.anchoredPosition = new Vector2(at.x + menu.widthPixels / 2f - window.widthPixels / 2f, at.y - menu.heightPixels - 8);
            window.FixBounds();
        }
    }
}
