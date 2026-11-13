using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// A trader's cart on the game's own counters and negotiation screen: the trader becomes the current store client,
    /// the cart goes on the front counter, and the player drags items onto the back counter to buy or sell. What is
    /// bought lands on the ground. A cart lasts as long as its request's key: what is sold joins it, what is bought
    /// leaves it, and what is left can be taken.
    /// </summary>
    internal sealed class TradeCounter
    {
        private const string Faction = "FACTION_LOWER_LEVEL";
        private const float BackdropPadding = 10f;
        private static readonly Color BackdropColor = new Color(0.07f, 0.08f, 0.1f, 0.9f);
        private static readonly Color BackdropEdge = new Color(0.9f, 0.76f, 0.42f, 0.6f);

        private readonly List<GameItem> cart = new List<GameItem>();
        private string cartKey;
        private StoreClient client;
        private int cashBefore;
        private HashSet<GameItem> groundBefore;
        private bool creditsWereShown;
        private RectTransform backdrop;

        public bool IsOpen { get; private set; }

        /// <summary>Opens the counters. Returns false, changing nothing, if the store already has a client.</summary>
        public bool Open(TradeRequest request)
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            PlayerStore store = PlayerStore.Instance;
            if (IsOpen || entry == null || store == null || MapUIManager.Instance == null || store.currentClientInstance != null) return false;
            EnsureCart(request);
            IsOpen = true;
            cashBefore = store.GetCash();
            groundBefore = new HashSet<GameItem>(entry.afterhourInventory.childItems);

            // The shop's own back-room stock is set aside, so the back counter is free for the deal.
            MapUIManager.Instance.TransferBackInvToCounter();
            entry.frontInvWindow.Show();
            entry.backInvWindow.Show();
            entry.Validate();

            // One client per cart, so its budget remembers what you already sold.
            if (client == null) client = CreateClient(request);
            store.currentClientInstance = StoreClientInstance.CreateClientInstance(client);
            store.isClientArrived = true;
            StoreUIManager.Instance.HideClient();
            foreach (GameItem item in cart.ToList())
            {
                if (!store.TryAddDirectSellingItemToTable(item)) cart.Remove(item); // The counter was full and the game destroyed it.
            }
            cart.Clear();

            GameObject credits = StoreUIManager.Instance.storeCreditDisplay.creditDisplayText.gameObject;
            creditsWereShown = credits.activeSelf;
            credits.SetActive(true);
            CreateBackdrop(entry);
            return true;
        }

        /// <summary>Keeps the backdrop fitted to the counters and behind them, as they settle or get reordered after opening. Call every frame while open.</summary>
        public void Tick()
        {
            if (!IsOpen || backdrop == null) return;
            EmporiumEntry entry = EmporiumEntry.Instance;
            PixelWindow front = entry.frontInvWindow;
            PixelWindow back = entry.backInvWindow;
            Rect? frontArea = WorldBounds(front);
            Rect? backArea = WorldBounds(back);
            if (!(frontArea is Rect f) || !(backArea is Rect b))
            {
                backdrop.gameObject.SetActive(false);
                return;
            }
            backdrop.gameObject.SetActive(true);
            KeepBehind(front.rectTransform, back.rectTransform);
            var parent = (RectTransform)backdrop.parent;
            float pad = BackdropPadding * parent.lossyScale.x;
            Rect area = Rect.MinMaxRect(Mathf.Min(f.xMin, b.xMin) - pad, Mathf.Min(f.yMin, b.yMin) - pad, Mathf.Max(f.xMax, b.xMax) + pad, Mathf.Max(f.yMax, b.yMax) + pad);
            backdrop.position = new Vector3(area.center.x, area.center.y, backdrop.position.z);
            backdrop.sizeDelta = new Vector2(area.width / parent.lossyScale.x, area.height / parent.lossyScale.y);
        }

        private void CreateBackdrop(EmporiumEntry entry)
        {
            Transform parent = entry.frontInvWindow.rectTransform.parent;
            if (parent == null) return;
            var panel = new GameObject("ExpeditionFramework.TradeBackdrop", typeof(RectTransform), typeof(Image), typeof(Outline));
            backdrop = (RectTransform)panel.transform;
            backdrop.SetParent(parent, false);
            backdrop.anchorMin = backdrop.anchorMax = backdrop.pivot = new Vector2(0.5f, 0.5f);
            panel.GetComponent<Image>().color = BackdropColor;
            panel.GetComponent<Image>().raycastTarget = false;
            Outline edge = panel.GetComponent<Outline>();
            edge.effectColor = BackdropEdge;
            edge.effectDistance = new Vector2(2f, -2f);
            panel.SetActive(false);
            Tick();
        }

        // UI draws later siblings on top, so the backdrop stays on the front counter's parent, before both counters.
        // Windows can be re-parented (docked) or reordered (brought to front) after opening, so this is checked every frame.
        private void KeepBehind(RectTransform front, RectTransform back)
        {
            Transform parent = front.parent;
            if (parent == null) return;
            if (backdrop.parent != parent) backdrop.SetParent(parent, false);
            int target = back.parent == parent ? Mathf.Min(front.GetSiblingIndex(), back.GetSiblingIndex()) : front.GetSiblingIndex();
            if (backdrop.GetSiblingIndex() > target) backdrop.SetSiblingIndex(target);
        }

        // A pixel window's own RectTransform has no size: its contents hang down and right from its pivot, over
        // widthPixels by heightPixels, which is what the game itself lays the counters out with. Measuring the
        // children instead picks up parts that reach well past the counter.
        private static Rect? WorldBounds(PixelWindow window)
        {
            RectTransform node = window?.rectTransform;
            if (node == null || !node.gameObject.activeInHierarchy || window.widthPixels <= 0 || window.heightPixels <= 0) return null;
            Vector3 topLeft = node.TransformPoint(Vector3.zero);
            Vector3 bottomRight = node.TransformPoint(new Vector3(window.widthPixels, -window.heightPixels, 0f));
            return Rect.MinMaxRect(Mathf.Min(topLeft.x, bottomRight.x), Mathf.Min(topLeft.y, bottomRight.y), Mathf.Max(topLeft.x, bottomRight.x), Mathf.Max(topLeft.y, bottomRight.y));
        }

        /// <summary>Closes the counters: anything you own goes to the ground, everything else goes back on the cart.</summary>
        public TradeResult Close()
        {
            var result = new TradeResult();
            if (!IsOpen) return result;
            IsOpen = false;
            if (backdrop != null) Object.Destroy(backdrop.gameObject);
            backdrop = null;
            EmporiumEntry entry = EmporiumEntry.Instance;
            PlayerStore store = PlayerStore.Instance;
            NegociationUIManager.Instance.CloseUI();

            // Bought items already went to the ground (the game puts purchases there while you're outside).
            foreach (GameItem item in entry.afterhourInventory.childItems)
            {
                if (!groundBefore.Contains(item)) result.Bought.Add(item.GetDisplayName());
            }

            foreach (GameGridInventory counter in new[] { entry.frontInvinvElement, entry.backInvinvElement })
            {
                foreach (GameItem item in counter.childItems.ToList())
                {
                    item.CloseContentWindow(includeChildren: true);
                    counter.Expel(item);
                    if (GeneralHelper.IsItemOwned(item)) GraphUtils.TryAcceptAll(entry.afterhourInventory, item);
                    else cart.Add(item);
                }
            }
            entry.ClearFrontInv();
            entry.ClearBackInv();
            MapUIManager.Instance.TransferCounterToBackInv();
            MapUIManager.Instance.DeactivateClientOutsideStore();
            entry.frontInvWindow.rectTransform.gameObject.SetActive(false);
            entry.backInvWindow.rectTransform.gameObject.SetActive(false);
            StoreUIManager.Instance.storeCreditDisplay.creditDisplayText.gameObject.SetActive(creditsWereShown);

            result.CashChange = store.GetCash() - cashBefore;
            groundBefore = null;
            return result;
        }

        /// <summary>Hands over everything on the cart (filling it first if the counter was never opened).</summary>
        public List<GameItem> TakeCart(TradeRequest request)
        {
            EnsureCart(request);
            List<GameItem> taken = cart.ToList();
            cart.Clear();
            // Goods that sat on the counter are marked as the trader's, and the night ground only takes things you own.
            foreach (GameItem item in taken) GeneralHelper.SetItemOwned(item, true);
            return taken;
        }

        /// <summary>Throws away whatever is left on the cart, at the end of the trip.</summary>
        public void Discard()
        {
            foreach (GameItem item in cart) item.Destroy();
            cart.Clear();
            client = null;
            cartKey = null;
        }

        /// <summary>Opens or closes the negotiation screen, for players who don't have it opening by itself.</summary>
        public void ToggleDeal() => NegociationUIManager.Instance.ToggleUI();

        private void EnsureCart(TradeRequest request)
        {
            if (request.Key == cartKey) return;
            Discard();
            cartKey = request.Key;
            foreach (string id in request.Stock)
            {
                GameItem item = ItemSpawner.Spawn(id);
                if (item != null) cart.Add(item);
            }
        }

        private static StoreClient CreateClient(TradeRequest request)
        {
            var created = new StoreClient
            {
                identifier = ModEntry.ModId + "_trader",
                displayName = request.Name,
                spriteName = SpriteDict.GetRandomScavSpriteName(),
                clientFaction = Faction,
                clientIntent = StoreClient.ClientIntent.SELLNBUY,
                nonShootable = true,
                budgetConversionRate = 100,
            };
            // Takes anything off you, as far as the budget goes. What you spend with them they can spend back.
            created.clientBuyingTagList.Add("ALL_TYPE");
            created.SetBudget(request.Budget);
            created.CompleteClientCreation();
            return created;
        }
    }
}
