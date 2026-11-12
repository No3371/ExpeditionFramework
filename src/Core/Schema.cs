using System.Collections.Generic;
using System.Linq;

namespace ExpeditionFramework.Core
{
    /// <summary>What a key may hold. Any leaves the value to whatever compiles it (effects, conditions, lines, values).</summary>
    internal enum Field { Any, Number, Flag, Text, Texts, Numbers, Tone, IntelTone }

    /// <summary>The keys one kind of object may have, and what each holds.</summary>
    internal sealed class Shape
    {
        public readonly Dictionary<string, Field> Fields;

        public Shape(params (string Key, Field Type)[] fields) => Fields = fields.ToDictionary(f => f.Key, f => f.Type);

        public Shape With(params (string Key, Field Type)[] more) => new Shape(Fields.Select(f => (f.Key, f.Value)).Concat(more).ToArray());
    }

    /// <summary>What each object in a pack may hold. Keys outside a shape are warned about; wrong-typed values are problems.</summary>
    internal static class Schema
    {
        // The tones ExpeditionLocation.ToneColors paints, plus "plain" (no colour). Intel cards know IntelCards.Tone's three.
        // Both lists live in the game assembly, which Core can't reference; a tone added there must be added here too.
        public static readonly string[] Tones = { "good", "bad", "dim", "heading", "enemy", "hint", "plain" };
        public static readonly string[] IntelTones = { "good", "bad", "mixed" };

        // Tags ItemReader puts on items from the game itself; every other item tag comes from item_groups or item_stats.
        public static readonly string[] EngineItemTags = { "weapon", "in_hand", "firearm", "melee", "has_rounds", "ammo", "armor_piercing", "non_lethal" };

        private static readonly Shape Named = new Shape(("extends", Field.Text));

        // Weight, when and tags only matter for what pools and tags pick from: rooms, events, encounters, enemies.
        private static readonly Shape Pooled = Named.With(("weight", Field.Number), ("tags", Field.Texts), ("when", Field.Any));

        public static readonly Dictionary<string, Shape> Sections = new Dictionary<string, Shape>
        {
            ["statuses"] = Named.With(("name", Field.Text), ("turns", Field.Number), ("rooms", Field.Number), ("intent", Field.Text),
                ("stats", Field.Any), ("interactions", Field.Any), ("on", Field.Any)),
            ["intents"] = Named.With(("label", Field.Any), ("tell", Field.Any), ("tone", Field.Tone), ("interruptible", Field.Flag),
                ("next", Field.Text), ("do", Field.Any)),
            ["loot"] = Named.With(("items", Field.Any)),
            ["carts"] = Named.With(("name", Field.Text), ("budget", Field.Any), ("stock", Field.Any)),
            ["enemies"] = Pooled.With(("name", Field.Text), ("stats", Field.Any), ("texts", Field.Any), ("opening", Field.Text),
                ("drops", Field.Text), ("behavior", Field.Any), ("interactions", Field.Any), ("on", Field.Any)),
            ["encounters"] = Pooled.With(("enemies", Field.Any), ("on", Field.Any)),
            ["events"] = Pooled.With(("text", Field.Any), ("scene", Field.Text), ("enter", Field.Any), ("choices", Field.Any)),
            ["rooms"] = Pooled.With(("text", Field.Any), ("enter", Field.Any), ("interactions", Field.Any), ("on", Field.Any)),
            ["kits"] = Named.With(("interactions", Field.Any), ("on", Field.Any)),
            ["areas"] = Named.With(("title", Field.Text), ("unlocked_by", Field.Text), ("ambience", Field.Text), ("art", Field.Text),
                ("hidden_when", Field.Any), ("player", Field.Any), ("vars", Field.Any), ("pools", Field.Any), ("entrance", Field.Text),
                ("kits", Field.Texts), ("groups", Field.Texts), ("luck", Field.Any), ("texts", Field.Any), ("packing", Field.Any),
                ("intel", Field.Any), ("hud", Field.Any), ("interactions", Field.Any), ("on", Field.Any), ("layout", Field.Any)),
        };

        public static readonly Shape LootEntry = new Shape(("item", Field.Text), ("loot", Field.Text), ("table", Field.Text), ("weight", Field.Number), ("when", Field.Any));
        public static readonly Shape CartLine = new Shape(("item", Field.Text), ("loot", Field.Texts), ("index", Field.Any), ("count", Field.Any));
        public static readonly Shape BehaviorEntry = new Shape(("weight", Field.Number), ("when", Field.Any), ("intent", Field.Text), ("behavior", Field.Any));
        public static readonly Shape EncounterEntry = new Shape(("id", Field.Text), ("count", Field.Number));
        public static readonly Shape Luck = new Shape(("value", Field.Any), ("low", Field.Number), ("high", Field.Number));
        public static readonly Shape Packing = new Shape(("no_backpack", Field.Text), ("card", Field.Text), ("needs", Field.Any));
        public static readonly Shape PackingNeed = new Shape(("items", Field.Any), ("missing", Field.Text));
        public static readonly Shape Intel = new Shape(("id", Field.Text), ("pool", Field.Text), ("chance", Field.Number), ("counts", Field.Numbers),
            ("title", Field.Any), ("description", Field.Any), ("effect", Field.Any), ("tone", Field.IntelTone), ("good", Field.Texts), ("when", Field.Any));
        public static readonly Shape Hud = new Shape(("title", Field.Any), ("rows", Field.Any), ("enemies", Field.Any));
        public static readonly Shape HudRow = new Shape(("when", Field.Any), ("parts", Field.Any), ("separator", Field.Flag));
        public static readonly Shape HudEnemies = new Shape(("line", Field.Any), ("pending", Field.Any), ("none", Field.Any), ("pending_scenes", Field.Texts));
        public static readonly Shape LayoutSlot = new Shape(("room", Field.Text), ("rooms", Field.Any), ("from", Field.Any), ("tag", Field.Text),
            ("pool", Field.Text), ("count", Field.Any), ("unique", Field.Flag));
        public static readonly Shape Interaction = new Shape(("use", Field.Text), ("id", Field.Text), ("label", Field.Any), ("group", Field.Text),
            ("in", Field.Texts), ("targets", Field.Text), ("turn", Field.Flag), ("once", Field.Flag), ("stay", Field.Flag), ("sound", Field.Text),
            ("sound_else", Field.Text), ("when", Field.Any), ("where", Field.Any), ("uses", Field.Any), ("confirm", Field.Any), ("do", Field.Any),
            ("check", Field.Any), ("after", Field.Any), ("preview", Field.Any));
        public static readonly Shape Confirm = new Shape(("when", Field.Any), ("text", Field.Any), ("yes", Field.Any), ("no", Field.Any));

        /// <summary>An item use written as an object with "select".</summary>
        public static readonly Shape Use = new Shape(("select", Field.Any), ("each", Field.Flag), ("consume", Field.Number), ("limit", Field.Number),
            ("order", Field.Any), ("where", Field.Any), ("with", Field.Any));

        public static readonly Shape Line = new Shape(("text", Field.Text), ("key", Field.Text), ("args", Field.Any), ("variants", Field.Number),
            ("tone", Field.Tone), ("cases", Field.Any));
        public static readonly Shape Case = Line.With(("when", Field.Any));
        public static readonly Shape HudPart = Line.With(("when", Field.Any), ("each_item", Field.Any));

        /// <summary>Why a value doesn't fit its field, or null.</summary>
        public static string Mismatch(Field type, object value)
        {
            switch (type)
            {
                case Field.Number:
                    return value is double ? null : "must be a number";
                case Field.Flag:
                    return value is bool ? null : "must be true or false";
                case Field.Text:
                case Field.Tone:
                case Field.IntelTone:
                    return value is string ? null : "must be a string";
                case Field.Texts:
                    return value is string || (value is List<object> texts && texts.All(t => t is string)) ? null : "must be a string or a list of strings";
                case Field.Numbers:
                    return value is double || (value is List<object> numbers && numbers.All(n => n is double)) ? null : "must be a number or a list of numbers";
                default:
                    return null;
            }
        }
    }
}
