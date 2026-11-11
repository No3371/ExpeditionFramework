using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Api;

namespace ExpeditionFramework.Core
{
    /// <summary>The public face of a run: ids and interfaces going in, the engine's own types behind them.</summary>
    public sealed partial class ExpeditionRun : IFlow, ICombat, IInventory, ITrade
    {
        public IFlow Flow => this;
        public ICombat Combat => this;
        public IInventory Inventory => this;
        public ITrade Trade => this;

        IEntity IRun.Focus => focus;
        void IRun.Say(string tone, ILine line, IScope scope) => Say(tone, (Line)line, scope);
        void IRun.Emit(string hook, IEntity subject, IEntity other) => Emit(hook, subject as Entity, other as Entity);
        void IRun.Wound(string kind) => Bridge.Wound(kind);
        void IRun.NightLog(string text, string color) => Bridge.NightLog(text, color);
        void IRun.PlaySound(string id) => Bridge.PlaySound(id);

        private T Find<T>(Dictionary<string, T> table, string id, string kind) where T : class
        {
            if (id != null && table.TryGetValue(id, out T def)) return def;
            Log("bad", "unknown " + kind + " " + id);
            return null;
        }

        // ---- Flow ----

        void IFlow.NextRoom() => NextRoom();
        void IFlow.Retreat() => Retreat();
        void IFlow.EndEvent() => EndEvent();
        void IFlow.StayInEvent() => StayInEvent();
        void IFlow.Schedule(int at, string eventId) => schedule[at] = eventId;
        void IFlow.Unschedule(int at) => schedule.Remove(at);
        string IFlow.ScheduledAt(int at) => schedule.TryGetValue(at, out string id) ? id : null;
        IEnumerable<string> IFlow.Scheduled => schedule.Values;

        void IFlow.StartEvent(string eventId, IScope scope)
        {
            EventDef def = Find(content.Events, eventId, "event");
            if (def != null) StartEvent(def, scope);
        }

        // ---- Combat ----

        IEntity ICombat.Make(string enemyId)
        {
            EnemyDef def = Find(content.Enemies, enemyId, "enemy");
            return def != null ? MakeEntity(def) : null;
        }

        void ICombat.Muster(IEnumerable<IEntity> group, bool add) => Muster(group.OfType<Entity>().ToList(), add);

        void ICombat.MusterEncounter(string encounterId, bool add)
        {
            EncounterDef encounter = Find(content.Encounters, encounterId, "encounter");
            if (encounter != null) MusterEncounter(encounter, add);
        }

        void ICombat.Engage(Effect won, IScope scope, bool surprised, Effect prepare) => Engage(won, scope, surprised, prepare);

        void ICombat.StartFight(string encounterId, Effect won, IScope scope, bool surprised)
        {
            EncounterDef encounter = Find(content.Encounters, encounterId, "encounter");
            if (encounter != null) StartFight(encounter, won, scope, surprised);
        }

        void ICombat.EndFight(bool won) => EndFight(won);
        void ICombat.EnemyTurn() => EnemyTurn();

        void ICombat.Spawn(IEntity arrival, bool surprised)
        {
            if (arrival is Entity e) Spawn(e, surprised);
        }

        void ICombat.SetFocus(IEntity who) => SetFocus(who as Entity);

        void ICombat.Defeat(IEntity victim, IEntity source)
        {
            if (victim is Entity e) Defeat(e, source as Entity);
        }

        void ICombat.Remove(IEntity who)
        {
            if (who is Entity e) e.Gone = true;
        }

        void ICombat.AddStatus(IEntity who, string statusId, int? turns, int? rooms)
        {
            StatusDef def = Find(content.Statuses, statusId, "status");
            if (who is Entity e && def != null) AddStatus(e, def, turns, rooms);
        }

        void ICombat.ClearStatus(IEntity who, string statusId)
        {
            if (who is Entity e) e.Statuses.RemoveAll(s => s.Def.Id == statusId);
        }

        void ICombat.SetIntent(IEntity enemy, string intentId)
        {
            IntentDef def = Find(content.Intents, intentId, "intent");
            if (enemy is Entity e && !e.IsPlayer && def != null) e.Intent = def;
        }

        int ICombat.Heal(IEntity who, int amount)
        {
            Decision decision = Decide(new Decision("heal", new Scope(this, Hero, who, null), amount));
            int before = who.Get("hp");
            if (!decision.Cancelled) who.Set("hp", Math.Min(who.Get("max_hp"), before + decision.Value));
            int healed = who.Get("hp") - before;
            Set("healed", healed);
            return healed;
        }

        // ---- Inventory ----

        IReadOnlyList<IItem> IInventory.Items => Bridge.Items();

        IReadOnlyList<IItem> IInventory.Select(IItemUse use, IScope scope, IItem main)
        {
            return Select((ItemUse)use, Scope.From(scope), main as RunItem).ToList();
        }

        bool IInventory.Fits(IItem gun, IItem round) => gun is RunItem g && round is RunItem r && Bridge.Fits(g, r);

        void IInventory.Consume(IItem item, int count)
        {
            if (item is RunItem it) Bridge.Consume(it, count);
        }

        bool IInventory.Equip(IItem item) => item is RunItem it && Bridge.Equip(it);

        string IInventory.Give(string itemId)
        {
            string name = itemId != null ? Bridge.GiveItem(itemId) : null;
            // GameBridge.GiveItem treats "" as nothing to give, so an empty id is an empty roll, not a failed give.
            if (!string.IsNullOrEmpty(itemId) && name == null) Log("bad", "could not give " + itemId + " (unknown item id, or no room on the ground)");
            Found(name);
            return name;
        }

        string IInventory.RollLoot(string tableId)
        {
            LootDef table = Find(content.Loot, tableId, "loot");
            return table != null ? DecideLoot(table) : null;
        }

        int IInventory.Credits => Bridge.Credits;
        void IInventory.AddCredits(int amount) => Bridge.AddCredits(amount);
        bool IInventory.SpendCredits(int amount) => Bridge.SpendCredits(amount);

        IReadOnlyList<string> IInventory.LoseCarried(int percent)
        {
            List<string> names = Bridge.LoseCarried(percent);
            foreach (string name in names) Log("bad", FrameworkText("ef_lost", name));
            return names;
        }

        void IInventory.LoseFound() => Bridge.LoseFound();
        string IInventory.TakeBack() => Bridge.TakeBack();
        int IInventory.ClearGround() => Bridge.ClearGround();

        // ---- Trade ----

        void ITrade.NewCart(string cartId, IScope scope)
        {
            CartDef def = Find(content.Carts, cartId, "cart");
            if (def != null) NewCart(def, scope);
        }

        void ITrade.Open(string cartId, Effect closed, IScope scope) => OpenTrade(cartId, closed, scope);
        void ITrade.TakeCart(string cartId, Effect before, IScope scope) => TakeCart(cartId, before, scope);
    }

    /// <summary>The world as seen from the map, where no expedition is going: nothing there, and nothing changes.</summary>
    internal sealed class NoWorld : IFlow, ICombat, IInventory, ITrade
    {
        public static readonly NoWorld Instance = new NoWorld();

        public void NextRoom() { }
        public void Retreat() { }
        public void StartEvent(string eventId, IScope scope) { }
        public void EndEvent() { }
        public void StayInEvent() { }
        public void Schedule(int depth, string eventId) { }
        public void Unschedule(int depth) { }
        public string ScheduledAt(int depth) => null;
        public IEnumerable<string> Scheduled => new string[0];

        public IEntity Make(string enemyId) => null;
        public void Muster(IEnumerable<IEntity> group, bool add) { }
        public void MusterEncounter(string encounterId, bool add) { }
        public void Engage(Effect won, IScope scope, bool surprised, Effect prepare) { }
        public void StartFight(string encounterId, Effect won, IScope scope, bool surprised) { }
        public void EndFight(bool won) { }
        public void EnemyTurn() { }
        public void Spawn(IEntity arrival, bool surprised) { }
        public void SetFocus(IEntity who) { }
        public void Defeat(IEntity victim, IEntity source) { }
        public void Remove(IEntity who) { }
        public void AddStatus(IEntity who, string statusId, int? turns = null, int? rooms = null) { }
        public void ClearStatus(IEntity who, string statusId) { }
        public void SetIntent(IEntity enemy, string intentId) { }
        public int Heal(IEntity who, int amount) => 0;

        public IReadOnlyList<IItem> Items => new IItem[0];
        public IReadOnlyList<IItem> Select(IItemUse use, IScope scope, IItem main) => new IItem[0];
        public bool Fits(IItem gun, IItem round) => false;
        public void Consume(IItem item, int count) { }
        public bool Equip(IItem item) => false;
        public string Give(string itemId) => null;
        public string RollLoot(string tableId) => null;
        public int Credits => 0;
        public void AddCredits(int amount) { }
        public bool SpendCredits(int amount) => false;
        public IReadOnlyList<string> LoseCarried(int percent) => new string[0];
        public void LoseFound() { }
        public string TakeBack() => null;
        public int ClearGround() => 0;

        public void NewCart(string cartId, IScope scope) { }
        public void Open(string cartId, Effect closed, IScope scope) { }
        public void TakeCart(string cartId, Effect before, IScope scope) { }
    }
}
