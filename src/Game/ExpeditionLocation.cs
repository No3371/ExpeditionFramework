using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ExpeditionFramework.Game
{
    /// <summary>Puts one data-defined area on the night map and plays it across three windows: info, log and actions.</summary>
    internal sealed class ExpeditionLocation : AfterhourLocation
    {
        internal const string ClearsCounter = "ef_clears";
        private const string BestDepthCounter = "ef_best_depth";
        private const int LogLinesShown = 80;
        private const float SceneWidthUnits = 9.6f;
        private const float RecheckSeconds = 0.5f;
        private const float ArrangeSeconds = 1f;
        private const string LogWindowId = "expedition_framework_log";
        private const string ActionsWindowId = "expedition_framework_actions";

        private static readonly Dictionary<string, string> ToneColors = new Dictionary<string, string>
        {
            { "good", "#8fc78f" }, { "bad", "#e0806c" }, { "dim", "#8e96a3" }, { "heading", "#e6c36a" }, { "enemy", "#d6b48a" }, { "hint", "#a8b4d0" },
        };

        private readonly Content content;
        private readonly AreaDef area;
        private readonly MapContext map;
        private readonly IntelCards intel;
        private readonly TradeCounter trade = new TradeCounter();
        private readonly GameSounds sounds = new GameSounds();
        private readonly ItemReader bagReader;

        private GameObject stage;
        private ExpeditionRun run;
        private CustomUIWindow window;
        private CustomUIWindow logWindow;
        private CustomUIWindow actionsWindow;
        private RectTransform actionsHost;
        private ScrollRect logScroll;
        private string shownButtons;
        private string shownTitle;
        private int hudBlocks;
        private float flowWidth;
        private float actionsHeight;
        private bool prepared;
        private bool dirty;
        private float nextRecheck;
        private float arrangeUntil;
        private int scrollFrames;
        private Vector2 arrangedFor;

        public ExpeditionLocation(Content content, AreaDef area)
        {
            this.content = content;
            this.area = area;
            bagReader = new ItemReader(content);
            map = new MapContext(area.Id, (pack, key, args) => ModHelper.GetLocalized(pack, key, args), name => GameValues.Get(name, area.Id), new SaveMemory());
            intel = new IntelCards(area, Id, map);
        }

        // Saved state keys and map ids can't rely on ':' being safe.
        public override string Id => LocationId(area.Id);
        internal static string LocationId(string areaId) => areaId.Replace(':', '_');
        public override string Source => area.Pack;
        public override string ImageKey => area.Pack + ":" + ArtId;
        public override string AmbienceId => area.Ambience ?? "dumping_ground";

        private string ArtId => area.Art ?? area.Id.Substring(area.Id.IndexOf(':') + 1);

        public override GameObject StageObject
        {
            get
            {
                if (stage == null)
                {
                    Sprite art = CustomUIManager.Instance.GetModSprite(area.Pack, ArtId);
                    if (art == null) return null;
                    stage = new GameObject("ExpeditionFramework.Stage." + Id);
                    var renderer = stage.AddComponent<SpriteRenderer>();
                    renderer.sortingOrder = BackgroundSortingOrder;
                    renderer.sprite = Sprite.Create(art.texture, art.rect, new Vector2(0.5f, 0.5f), art.rect.width / SceneWidthUnits);
                    stage.SetActive(false);
                }
                return stage;
            }
        }

        public override string GetPanelTitle() => ModHelper.GetLocalized(area.Title.Pack, area.Title.Key);

        public override string GetTitle()
        {
            if (IsDiscovered()) intel.EnsureToday();
            List<string> missing = Missing();
            string title = GetPanelTitle();
            if (missing.Count == 0 || area.Packing?.Card == null) return title;
            return ModHelper.GetLocalized(area.Packing.Card.Pack, area.Packing.Card.Key, title, string.Join(", ", missing));
        }

        public override bool IsDiscovered()
        {
            if (area.HiddenWhen != null && area.HiddenWhen(map.Root)) return false;
            if (area.UnlockedBy == null) return true;
            return GameValues.Clears(area.UnlockedBy) > 0;
        }

        public override bool IsAvailable() => base.IsAvailable() && Missing().Count == 0;

        // What's packed is the chosen bag plus the hands window. Carrying neither counts as having no backpack.
        private List<string> Missing()
        {
            GameItem backpack = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.selectedBackpack : null;
            List<GameItem> held = HandsWindow.Held();
            if (backpack == null && held.Count == 0) return map.Missing(area, null);
            var contents = new List<GameItem>(HandsWindow.Contents());
            if (backpack != null) contents.AddRange(GraphUtils.FindAllChildrenType<GameItem>(backpack).Where(GeneralHelper.IsItemOwned));
            return map.Missing(area, bagReader.Read(contents, held));
        }

        public override void Arrive()
        {
            GameBridge.OpenAmmoBoxes();
            run = new ExpeditionRun(content, area, new GameBridge(content, area, trade, sounds), sides => RNG.GetRandomInt(1, sides),
                (pack, key, args) => ModHelper.GetLocalized(pack, key, args), ModEntry.L("ef_player"), intel.Today());
            run.Begin();
        }

        public override void BuildPanel(CustomUIBuilder builder)
        {
            window = builder.Handle;
            prepared = false;
            shownButtons = null;
            shownTitle = null;
            logScroll = null;
            builder.SetSize(PanelLayout.PanelWidth, PanelLayout.MinPanelHeight * 2f);
            builder.SetDraggable(true);
            // The info scrolls and fills the panel. The buttons live in their own window beside the bag window.
            hudBlocks = 1 + (area.Hud?.Rows.Count(r => r.Separator) ?? 0);
            builder.BeginScrollFlexible(PanelLayout.InfoMinHeight);
            for (int i = 0; i < hudBlocks; i++)
            {
                if (i > 0) builder.AddSeparator();
                builder.AddLabel("", "ef_hud_" + i);
            }
            builder.End();
            dirty = true;
        }

        public override void Tick()
        {
            trade.Tick();
            sounds.Tick();
            if (run == null || window == null || !window.IsAlive)
            {
                EndTrade();
                CloseSideWindows();
                return;
            }
            if (!prepared) Prepare();
            if (logWindow == null || !logWindow.IsAlive) OpenLog();
            if (actionsWindow == null || !actionsWindow.IsAlive) OpenActions();
            // Buttons wrap to the window's width, so lay them out again when that width changes.
            if (actionsHost != null && Mathf.Abs(FlowWidth() - flowWidth) > 1f)
            {
                shownButtons = null;
                dirty = true;
            }
            // The ground window can appear a moment after the panel, so keep placing everything for the first second,
            // and again whenever the screen size changes. After that the player can drag windows where they like.
            if (window.Rect.parent is RectTransform canvas && canvas.rect.size != arrangedFor) arrangeUntil = Time.unscaledTime + ArrangeSeconds;
            if (Time.unscaledTime < arrangeUntil) Arrange();
            if (dirty || Time.unscaledTime >= nextRecheck)
            {
                nextRecheck = Time.unscaledTime + RecheckSeconds;
                dirty = false;
                Refresh();
            }
            // The layout settles a frame after the text changes, so keep pinning the log to its newest line for a few frames.
            if (scrollFrames > 0 && logScroll != null)
            {
                scrollFrames--;
                logScroll.verticalNormalizedPosition = 0f;
            }
        }

        public override void Leave()
        {
            if (run == null) return;
            EndTrade();
            trade.Discard();
            sounds.Clear();
            AfterhourLocationState state = PlayerStore.Instance.GetAfterhourState(Id);
            state.SetCounter(BestDepthCounter, Mathf.Max(state.GetCounter(BestDepthCounter), run.Depth));
            // Packs mark a clear with run.cleared when the player can still walk out afterwards.
            if (run.Outcome == "cleared" || run.Get("cleared") > 0) state.SetCounter(ClearsCounter, state.GetCounter(ClearsCounter) + 1);
            CloseSideWindows();
            run = null;
            window = null;
        }

        private void Arrange()
        {
            if (!(window.Rect.parent is RectTransform canvas) || logWindow == null || actionsWindow == null) return;
            arrangedFor = canvas.rect.size;
            PanelLayout.Arrange(canvas, window.Rect, logWindow.Rect, actionsWindow.Rect, actionsHeight);
        }

        // The vanilla panel adds a free Return button and a close button; leaving has to go through the run instead.
        private void Prepare()
        {
            prepared = true;
            HideCloseButton(window);
            foreach (Button button in window.ContentRoot.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
            arrangeUntil = Time.unscaledTime + ArrangeSeconds;
        }

        private CustomUIBuilder SideWindow(string id, string titleKey)
        {
            CustomUIBuilder builder = CustomUIManager.Instance.CreateWindow(id, run.FrameworkText(titleKey), window.layerId);
            builder.SetDraggable(true);
            builder.SetCloseOnEscape(false);
            return builder;
        }

        private static void HideCloseButton(CustomUIWindow side)
        {
            if (side.frame != null && side.frame.closeButton != null) side.frame.closeButton.gameObject.SetActive(false);
        }

        private void OpenLog()
        {
            CustomUIBuilder builder = SideWindow(LogWindowId, "ef_log");
            builder.SetSize(PanelLayout.LogWidth, PanelLayout.MinPanelHeight);
            builder.BeginScrollFlexible(120f);
            builder.AddLabel("", "ef_log");
            builder.End();
            logWindow = builder.Show();
            HideCloseButton(logWindow);
            CustomUIElement log = logWindow.Get("ef_log");
            logScroll = log != null && log.root != null ? log.root.GetComponentInParent<ScrollRect>() : null;
            arrangeUntil = Time.unscaledTime + ArrangeSeconds;
            dirty = true;
        }

        private void OpenActions()
        {
            CustomUIBuilder builder = SideWindow(ActionsWindowId, "ef_actions");
            builder.SetSize(PanelLayout.PanelWidth, 120f);
            builder.BeginColumn(PanelLayout.ButtonSpacing);
            actionsHost = builder.CurrentContainer;
            builder.End();
            actionsWindow = builder.Show();
            HideCloseButton(actionsWindow);
            flowWidth = 0f;
            actionsHeight = 0f;
            shownButtons = null;
            arrangeUntil = Time.unscaledTime + ArrangeSeconds;
            dirty = true;
        }

        private void CloseSideWindows()
        {
            if (logWindow != null && logWindow.IsAlive) logWindow.Close();
            if (actionsWindow != null && actionsWindow.IsAlive) actionsWindow.Close();
            logWindow = null;
            logScroll = null;
            actionsWindow = null;
            actionsHost = null;
        }

        // The width buttons can wrap within: the actions window's content, less its padding.
        private float FlowWidth()
        {
            if (actionsWindow == null || !actionsWindow.IsAlive) return 0f;
            RectTransform content = actionsWindow.ContentRoot;
            LayoutGroup group = content.GetComponent<LayoutGroup>();
            return content.rect.width - (group != null ? group.padding.horizontal : 0);
        }

        private void Refresh()
        {
            string title = run.PanelTitle(GetPanelTitle());
            if (title != shownTitle)
            {
                shownTitle = title;
                window.SetTitle(title);
            }
            RefreshHud();
            if (SetLabel(logWindow, "ef_log", LogText())) scrollFrames = 3;
            if (actionsHost != null) RefreshButtons();
        }

        private void RefreshHud()
        {
            var blocks = new List<List<string>> { new List<string>() };
            foreach (List<HudSpan> row in run.Hud())
            {
                if (row.Count == 0)
                {
                    if (blocks.Count < hudBlocks) blocks.Add(new List<string>());
                }
                else
                {
                    blocks[blocks.Count - 1].Add(RowText(row));
                }
            }
            for (int i = 0; i < hudBlocks; i++) SetLabel(window, "ef_hud_" + i, i < blocks.Count ? string.Join("\n", blocks[i]) : "");
        }

        private static string RowText(List<HudSpan> row)
        {
            string text = "";
            foreach (HudSpan span in row)
            {
                if (text.Length > 0 && !text.EndsWith(" ")) text += "   ";
                text += Paint(span.Text, span.Tone);
            }
            return text;
        }

        private sealed class ActionButton
        {
            public string Group;
            public string Label;
            public Action Press;
        }

        private List<ActionButton> CurrentButtons()
        {
            if (run.Trading)
            {
                return new List<ActionButton>
                {
                    new ActionButton { Group = "supplies", Label = run.FrameworkText("ef_trade_deal"), Press = trade.ToggleDeal },
                    new ActionButton { Group = "move", Label = run.FrameworkText("ef_trade_finish"), Press = EndTrade },
                };
            }
            if (run.Over)
            {
                string specific = "ef_leave_" + run.Outcome;
                string label = run.FrameworkText(area.Texts.ContainsKey(specific) ? specific : "ef_leave");
                return new List<ActionButton> { new ActionButton { Group = "move", Label = label, Press = Depart } };
            }
            return run.Options().Select(option => new ActionButton { Group = option.Group, Label = option.Label, Press = () => Choose(option, false) }).ToList();
        }

        private void RefreshButtons()
        {
            List<ActionButton> buttons = CurrentButtons();
            string signature = string.Join("|", buttons.Select(b => b.Group + b.Label));
            if (signature == shownButtons) return;
            shownButtons = signature;
            CustomUIBuilder.ClearContainer(actionsHost);
            CustomUIBuilder.RebuildLayout(actionsWindow.Rect);
            flowWidth = FlowWidth();
            var builder = new CustomUIBuilder(CustomUIManager.Instance, actionsWindow, actionsHost);
            var items = new List<(RectTransform Item, bool NewRow)>();
            foreach (IGrouping<string, ActionButton> group in buttons.GroupBy(b => b.Group).OrderBy(g => OrderOf(g.Key)))
            {
                builder.AddLabel(Paint(GroupLabel(group.Key), "heading"));
                items.Add((LastChild(actionsHost), true));
                foreach (ActionButton button in group)
                {
                    builder.AddButton(button.Label, () => button.Press());
                    items.Add((LastChild(actionsHost), false));
                }
            }
            PanelLayout.FlowIntoRows(actionsHost, items, flowWidth);
            CustomUIBuilder.RebuildLayout(actionsWindow.Rect);

            // Size the window to its rows: its frame (window height less content height) plus the content's own height.
            RectTransform content = actionsWindow.ContentRoot;
            float needed = LayoutUtility.GetPreferredHeight(content) + actionsWindow.Rect.rect.height - content.rect.height;
            if (Mathf.Abs(needed - actionsHeight) > 1f)
            {
                actionsHeight = needed;
                Arrange();
            }
        }

        private static RectTransform LastChild(RectTransform parent) => (RectTransform)parent.GetChild(parent.childCount - 1);

        private int OrderOf(string group)
        {
            int index = area.GroupOrder?.IndexOf(group) ?? -1;
            return index < 0 ? int.MaxValue : index;
        }

        private string GroupLabel(string group)
        {
            string key = "ef_group_" + group;
            string text = run.FrameworkText(key);
            return string.IsNullOrEmpty(text) || text == key ? group : text;
        }

        private void Choose(RunOption option, bool confirmed)
        {
            if (run == null || dirty) return;
            if (option.ConfirmText != null && !confirmed)
            {
                ExpeditionRun asked = run;
                GenericConfirmationUIManager.Instance.OpenPanel(option.ConfirmText, option.ConfirmYes, option.ConfirmNo,
                    () =>
                    {
                        GenericConfirmationUIManager.Instance.ClosePanel();
                        if (run == asked) Choose(option, true);
                    },
                    () => GenericConfirmationUIManager.Instance.ClosePanel());
                return;
            }
            try
            {
                run.Perform(option);
            }
            catch (Exception ex)
            {
                ModEntry.Log.Warning("Expedition action " + option.Interaction.Id + " failed: " + ex);
            }
            // The counters sit in the middle of the screen, where the info panel is.
            if (run.Trading) window.Hide();
            shownButtons = null;
            dirty = true;
        }

        // Puts the counters away and logs the result. Safe to call when no trade is open.
        private void EndTrade()
        {
            if (!trade.IsOpen || run == null) return;
            run.FinishTrade();
            if (window != null && window.IsAlive) window.Show();
            shownButtons = null;
            dirty = true;
        }

        // Someone carried out skips the walk back.
        private void Depart()
        {
            if (run.Player.Get("hp") <= 0) MapUIManager.Instance.ReturnNow();
            else MapUIManager.Instance.Return();
        }

        private string LogText()
        {
            IEnumerable<LogLine> tail = run.Log_.Skip(Mathf.Max(0, run.Log_.Count - LogLinesShown));
            return string.Join("\n", tail.Select(FormatLine));
        }

        private static string FormatLine(LogLine line)
        {
            string text;
            switch (line.Tone)
            {
                case "heading": text = "\n" + Paint("<b>" + line.Text + "</b>", line.Tone); break;
                case "hint": text = Paint("<i>" + line.Text + "</i>", line.Tone); break;
                default: text = Paint(line.Text, line.Tone); break;
            }
            return string.IsNullOrEmpty(line.Roll) ? text : text + " " + Paint("<size=85%>" + line.Roll + "</size>", "dim");
        }

        private static string Paint(string text, string tone)
        {
            return tone != null && ToneColors.TryGetValue(tone, out string hex) ? "<color=" + hex + ">" + text + "</color>" : text;
        }

        private static bool SetLabel(CustomUIWindow owner, string id, string text)
        {
            if (owner == null || !owner.IsAlive) return false;
            CustomUIElement element = owner.Get(id);
            if (element == null || element.GetText() == text) return false;
            element.SetText(text);
            return true;
        }
    }
}
