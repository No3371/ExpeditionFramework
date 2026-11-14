using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Core;

namespace ExpeditionFramework.Game
{
    /// <summary>Turns game items into what the engine sees: facts read from the item, then whatever packs say about it.</summary>
    internal sealed class ItemReader
    {
        private const string AmmoType = "AMMUNITION";
        private const string FirearmType = "FIREARM";
        private const string MeleeType = "MELEE_WEAPON";
        private const string WeaponType = "WEAPON";

        /// <summary>The facts <see cref="ReadFacts"/> sets on every weapon, in the order the debug log prints them.</summary>
        public static readonly string[] FactNames = { "cells", "reach", "breadth", "price", "kinds", "purposes" };

        // The types EF already turns into tags; counting them would make every weapon read as more than a weapon.
        private static readonly HashSet<string> TaggedTypes = new HashSet<string> { WeaponType, MeleeType, FirearmType };

        private readonly Content content;
        // One RunItem per gun or blade, and per kind of everything else, so state like a gun's magazine lasts the trip.
        private readonly Dictionary<object, RunItem> known = new Dictionary<object, RunItem>();
        private List<GameItem> rounds = new List<GameItem>();

        public ItemReader(Content content) { this.content = content; }

        public static bool IsFirearm(GameItem item) => item.IsGameItemType(FirearmType);

        /// <summary>The item's picture as the bag draws it, or null for an item that isn't from the game.</summary>
        public static UnityEngine.Sprite Picture(RunItem item)
        {
            GameItem game = item?.Handle as GameItem ?? (item?.Handle as List<GameItem>)?.FirstOrDefault();
            return game is GameItemElement element ? element.ResolveSpriteByName(element.spritePath) : null;
        }

        public bool IsMelee(GameItem item) => item.IsGameItemType(MeleeType)
            || (content.ItemStats.TryGetValue(item.identifier, out ItemStats stats) && stats.Tags.Contains("melee"));

        // A round has a caliber. Containers some mods type as ammunition while they hold rounds (a box, a case) don't.
        private static bool IsRound(GameItem item) =>
            item.IsGameItemType(AmmoType) && !item.IsTag(AmmoBoxHelper.AMMO_BOX_TAG) && item.IsTag(GunHelper.GUN_CALIBER_TAG);

        public List<RunItem> Read(List<GameItem> items, ICollection<GameItem> held)
        {
            rounds = items.Where(IsRound).ToList();
            var result = new List<RunItem>();
            foreach (GameItem item in items.Where(i => IsFirearm(i) || IsMelee(i))) result.Add(ReadWeapon(item, held.Contains(item)));
            foreach (IGrouping<string, GameItem> group in items.Where(i => !IsFirearm(i) && !IsMelee(i)).GroupBy(i => i.identifier))
            {
                result.Add(ReadStack(group.Key, group.ToList()));
            }
            return result;
        }

        private RunItem Known(object key, string id, string name)
        {
            if (!known.TryGetValue(key, out RunItem item)) known[key] = item = new RunItem { Id = id };
            item.Name = name;
            item.Tags.Clear();
            return item;
        }

        private RunItem ReadWeapon(GameItem weapon, bool inHand)
        {
            RunItem item = Known(weapon, weapon.identifier, weapon.GetDisplayName());
            item.Handle = weapon;
            item.Count = 1;
            item.Sound = weapon.soundUse;
            item.Tags.Add("weapon");
            if (inHand) item.Tags.Add("in_hand");
            item.Tags.Add(IsFirearm(weapon) ? "firearm" : "melee");
            if (IsFirearm(weapon) && Matching(weapon).Count > 0) item.Tags.Add("has_rounds");
            item.ApplyStats(content);
            ReadFacts(weapon, item);
            if (IsFirearm(weapon)) ReadGun(weapon, item);
            return item;
        }

        /// <summary>
        /// What the weapon's shape, price and game types say. ReadWeapon calls this after item_stats, as it does ReadGun, so a
        /// pack can't mask the game's numbers. Shape is the item's own, without attachments. Price is the base price (unitValue): GetValue() adds what
        /// the party, a trader and the stack size do, so the same knife would read differently at a counter. Purposes counts only
        /// the game's major types, because the other types include grades like JUNK or MIDLINE that say nothing about other uses.
        /// </summary>
        public static void ReadFacts(GameItem weapon, RunItem item)
        {
            GridShape shape = weapon.shape;
            (int reach, int breadth) = Footprint.Measure(shape.width, shape.height, (x, y) => shape.GetLocal(x, y) == 1);
            List<string> types = weapon.GetGameItemType();
            item.Set("cells", weapon.GetItemSize());
            item.Set("reach", reach);
            item.Set("breadth", breadth);
            item.Set("price", (int)Math.Min(int.MaxValue, Math.Max(0L, weapon.unitValue)));
            item.Set("kinds", types.Count(t => !TaggedTypes.Contains(t)));
            item.Set("purposes", types.Count(t => t != WeaponType && TypeHelper.MAJOR_TYPES_LIST.Contains(t)));
        }

        /// <summary>One log line: the id, the facts, the shape grid (rows top first, 1 filled), the game types and the name.</summary>
        public static string Describe(GameItem weapon, RunItem item)
        {
            GridShape shape = weapon.shape;
            IEnumerable<string> rows = Enumerable.Range(0, shape.height)
                .Select(y => new string(Enumerable.Range(0, shape.width).Select(x => shape.GetLocal(x, y) == 1 ? '1' : '0').ToArray()));
            return "id=" + item.Id + " " + string.Join(" ", FactNames.Select(n => n + "=" + item.Get(n)))
                + " width=" + shape.width + " height=" + shape.height + " shape=" + string.Join("/", rows)
                + " types=" + string.Join(",", weapon.GetGameItemType()) + " name=" + weapon.GetDisplayName();
        }

        private void ReadGun(GameItem gun, RunItem item)
        {
            if (!gun.IsTag(GunHelper.GUN_CURRENT_FIREPOWER_TAG)) GunHelper.UpdateCurrentGunStats(gun);
            item.Set("firepower", gun.GetTagReadonly(GunHelper.GUN_CURRENT_FIREPOWER_TAG).GetInt());
            item.Set("accuracy", gun.GetTagReadonly(GunHelper.GUN_CURRENT_ACCURACY_TAG).GetInt());
            item.Set("recoil", gun.GetTagReadonly(GunHelper.GUN_CURRENT_RECOIL_TAG).GetInt());
            item.Set("ergonomics", gun.GetTagReadonly(GunHelper.GUN_CURRENT_ERGONOMICS_TAG).GetInt());
            item.Set("reliability", gun.GetTagReadonly(GunHelper.GUN_CURRENT_RELIABILITY_TAG).GetInt());
            item.Set("suppressed", gun.GetTagReadonly(GunHelper.GUN_SUPPRESSED_TAG).GetInt() != GunHelper.SUPPRESSED_NONE ? 1 : 0);
            int bonus = 0;
            foreach (GameItem part in GraphUtils.FindAllChildrenType<GameItem>(gun))
            {
                if (part != gun && content.ItemStats.TryGetValue(part.identifier, out ItemStats stats) && stats.Props.TryGetValue("capacity_bonus", out int extra)) bonus += extra;
            }
            item.Set("capacity", item.Get("capacity") + bonus);
        }

        private RunItem ReadStack(string id, List<GameItem> stack)
        {
            RunItem item = Known(id, id, stack[0].GetDisplayName());
            item.Handle = stack;
            item.Count = GraphUtils.CountAll(stack);
            if (IsRound(stack[0]))
            {
                item.Tags.Add("ammo");
                int kind = KindOf(stack[0]);
                item.Set("kind", kind);
                if (kind == 1) item.Tags.Add("armor_piercing");
                if (kind == 2) item.Tags.Add("non_lethal");
            }
            item.ApplyStats(content);
            return item;
        }

        private static int KindOf(GameItem round)
        {
            switch (round.GetTagReadonly(AmmoBoxHelper.AMMO_TYPE_TAG).GetString())
            {
                case AmmoBoxHelper.ARMOR_PIERCING_AMMO: return 1;
                case AmmoBoxHelper.NON_LETHAL_AMMO: return 2;
                default: return 0;
            }
        }

        public bool Fits(RunItem gun, RunItem round)
        {
            return gun.Handle is GameItem weapon && round.Handle is List<GameItem> stack && stack.Count > 0 && Matching(weapon).Contains(stack[0]);
        }

        private List<GameItem> Matching(GameItem gun)
        {
            int caliber = gun.GetTagReadonly(GunHelper.GUN_CALIBER_TAG).GetInt();
            List<GameItem> matching = rounds.Where(r => r.GetTagReadonly(GunHelper.GUN_CALIBER_TAG).GetInt() == caliber).ToList();
            // The game sells no .45 or 12 gauge, so those guns take 10mm.
            if (matching.Count == 0 && (caliber == GunHelper.CALIBER_45AFP || caliber == GunHelper.CALIBER_12GAUGE))
            {
                matching = rounds.Where(r => r.GetTagReadonly(GunHelper.GUN_CALIBER_TAG).GetInt() == GunHelper.CALIBER_10MM).ToList();
            }
            return matching;
        }
    }
}
