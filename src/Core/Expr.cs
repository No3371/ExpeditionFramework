using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExpeditionFramework.Api;

namespace ExpeditionFramework.Core
{
    /// <summary>
    /// Compiles expressions like "60 + item.accuracy * 5 - target.defense", "2d6+1", "self.hp <= 5 && run.depth >= 2" or
    /// "carried(#light) > 0 ? 2 : 0". Booleans are 1 and 0. Paths start with self, target, player, item, with, run, role.NAME,
    /// arg, game or val. A name starting with # reads a tag, as 1 or 0.
    /// </summary>
    public sealed class Expr
    {
        private readonly string text;
        private readonly Func<string, Value> named;
        private readonly Func<string, bool> hasMod;
        private int at;
        private string error;

        private Expr(string text, Func<string, Value> named, Func<string, bool> hasMod)
        {
            this.text = text;
            this.named = named;
            this.hasMod = hasMod;
        }

        /// <summary>
        /// Returns null and sets the error when the text doesn't parse. <paramref name="named"/> resolves val.NAME and
        /// <paramref name="hasMod"/> answers mod(id).
        /// </summary>
        public static Value Compile(string text, out string error, Func<string, Value> named = null, Func<string, bool> hasMod = null)
        {
            var parser = new Expr(text, named, hasMod);
            Value value = parser.Ternary();
            parser.Skip();
            if (parser.error == null && parser.at < text.Length) parser.error = "unexpected '" + text.Substring(parser.at) + "'";
            error = parser.error;
            return error == null ? value : null;
        }

        // ---- Paths ----

        private static bool Split(string path, out string head, out string name)
        {
            int dot = path.IndexOf('.');
            head = dot > 0 ? path.Substring(0, dot) : path;
            name = dot > 0 ? path.Substring(dot + 1) : null;
            return dot > 0 && name.Length > 0;
        }

        private static Func<IScope, IEntity> EntityOf(string head)
        {
            switch (head)
            {
                case "self": return s => s.Self;
                case "target": return s => s.Target;
                case "player": return s => s.Run.Player;
                default: return null;
            }
        }

        private static Func<IScope, IItem> ItemOf(string head)
        {
            switch (head)
            {
                case "item": return s => s.Item;
                case "with": return s => s.Paired;
                default: return null;
            }
        }

        private static Value ReadEntity(Func<IScope, IEntity> who, string name, bool raw)
        {
            if (name.StartsWith("#"))
            {
                string tag = name.Substring(1);
                return s => who(s)?.HasTag(tag) == true ? 1 : 0;
            }
            if (raw) return s => who(s)?.Base(name) ?? 0;
            return s => who(s)?.Get(name) ?? 0;
        }

        private static Value ReadItem(Func<IScope, IItem> what, string name)
        {
            if (name.StartsWith("#"))
            {
                string tag = name.Substring(1);
                return s => what(s)?.HasTag(tag) == true ? 1 : 0;
            }
            return s => what(s)?.Get(name) ?? 0;
        }

        /// <summary>Reader for a path, or null if the head is unknown. With raw, entity stats leave out what statuses add.</summary>
        public static Value Reader(string path, Func<string, Value> named = null, bool raw = false)
        {
            if (!Split(path, out string head, out string name)) return null;
            Func<IScope, IEntity> entity = EntityOf(head);
            if (entity != null) return ReadEntity(entity, name, raw);
            Func<IScope, IItem> item = ItemOf(head);
            if (item != null) return ReadItem(item, name);
            switch (head)
            {
                case "run": return s => s.Run.Get(name);
                case "arg": return s => s.Arg(name);
                case "game": return s => s.Run.Game(name);
                case "val": return named?.Invoke(name);
                case "role":
                    if (!Split(name, out string role, out string stat)) return null;
                    return ReadEntity(s => s.Run.Role(role), stat, raw);
                default: return null;
            }
        }

        /// <summary>Writer for a path, or null if the head is unknown.</summary>
        public static Action<IScope, int> Writer(string path)
        {
            if (!Split(path, out string head, out string name)) return null;
            Func<IScope, IEntity> entity = EntityOf(head);
            if (entity != null) return (s, v) => entity(s)?.Set(name, v);
            Func<IScope, IItem> item = ItemOf(head);
            if (item != null) return (s, v) => item(s)?.Set(name, v);
            if (head == "run") return (s, v) => s.Run.Set(name, v);
            if (head == "role" && Split(name, out string role, out string stat)) return (s, v) => s.Run.Role(role)?.Set(stat, v);
            return null;
        }

        /// <summary>Writer for a text path like run.$name, self.$name or role.boss.$name, or null.</summary>
        public static Action<IScope, string> TextWriter(string path)
        {
            int mark = path.LastIndexOf(".$", StringComparison.Ordinal);
            if (mark <= 0) return null;
            string head = path.Substring(0, mark);
            string name = path.Substring(mark + 2);
            if (head == "run") return (s, v) => s.Run.SetText(name, v);
            Func<IScope, IEntity> entity = head.StartsWith("role.") ? (s => s.Run.Role(head.Substring(5))) : EntityOf(head);
            if (entity != null) return (s, v) => entity(s)?.SetText(name, v);
            Func<IScope, IItem> item = ItemOf(head);
            if (item != null) return (s, v) => item(s)?.SetText(name, v);
            return null;
        }

        private static string Local(string id)
        {
            if (id == null) return "";
            int colon = id.IndexOf(':');
            return colon >= 0 ? id.Substring(colon + 1) : id;
        }

        /// <summary>
        /// Reads text: HEAD.name, HEAD.id, HEAD.$var. With no field the head reads as its own id, without the pack, which is
        /// how "{self}_look" names a text key. Returns null if this isn't a text path.
        /// </summary>
        public static Func<IScope, string> TextReader(string path, bool fieldRequired)
        {
            string head;
            string field;
            if (path.StartsWith("role."))
            {
                string[] parts = path.Split(new[] { '.' }, 3);
                if (parts.Length < 2) return null;
                head = "role." + parts[1];
                field = parts.Length == 3 ? parts[2] : null;
            }
            else
            {
                Split(path, out head, out field);
            }
            if (field == null && fieldRequired) return null;
            if (field != null && field != "name" && field != "id" && !field.StartsWith("$")) return null;
            string variable = field != null && field.StartsWith("$") ? field.Substring(1) : null;

            if (head == "area") return field == null || field == "id" ? s => Local(s.Run.AreaId) : (Func<IScope, string>)null;
            if (head == "run") return variable != null ? s => s.Run.GetText(variable) : (Func<IScope, string>)null;

            Func<IScope, IEntity> entity = head.StartsWith("role.") ? (s => s.Run.Role(head.Substring(5))) : EntityOf(head);
            if (entity != null)
            {
                if (variable != null) return s => entity(s)?.GetText(variable);
                if (field == "name") return s => entity(s)?.Name;
                return s => Local(entity(s)?.Id);
            }
            Func<IScope, IItem> item = ItemOf(head);
            if (item != null)
            {
                if (variable != null) return s => item(s)?.GetText(variable);
                if (field == "name") return s => item(s)?.Name;
                return s => Local(item(s)?.Id);
            }
            return null;
        }

        // ---- Grammar ----

        private void Skip() { while (at < text.Length && char.IsWhiteSpace(text[at])) at++; }

        private bool Eat(string token)
        {
            Skip();
            if (string.CompareOrdinal(text, at, token, 0, token.Length) != 0) return false;
            at += token.Length;
            return true;
        }

        private Value Ternary()
        {
            Value condition = Or();
            if (!Eat("?")) return condition;
            Value yes = Ternary();
            if (!Eat(":")) Fail("missing : after ?");
            Value no = Ternary();
            return s => condition(s) != 0 ? yes(s) : no(s);
        }

        private Value Or()
        {
            Value left = And();
            while (Eat("||")) { Value a = left, b = And(); left = s => a(s) != 0 || b(s) != 0 ? 1 : 0; }
            return left;
        }

        private Value And()
        {
            Value left = Compare();
            while (Eat("&&")) { Value a = left, b = Compare(); left = s => a(s) != 0 && b(s) != 0 ? 1 : 0; }
            return left;
        }

        private Value Compare()
        {
            Value a = Sum();
            if (Eat("<=")) { Value b = Sum(); return s => a(s) <= b(s) ? 1 : 0; }
            if (Eat(">=")) { Value b = Sum(); return s => a(s) >= b(s) ? 1 : 0; }
            if (Eat("==")) { Value b = Sum(); return s => a(s) == b(s) ? 1 : 0; }
            if (Eat("!=")) { Value b = Sum(); return s => a(s) != b(s) ? 1 : 0; }
            if (Eat("<")) { Value b = Sum(); return s => a(s) < b(s) ? 1 : 0; }
            if (Eat(">")) { Value b = Sum(); return s => a(s) > b(s) ? 1 : 0; }
            return a;
        }

        private Value Sum()
        {
            Value left = Product();
            while (true)
            {
                if (Eat("+")) { Value a = left, b = Product(); left = s => a(s) + b(s); }
                else if (Eat("-")) { Value a = left, b = Product(); left = s => a(s) - b(s); }
                else return left;
            }
        }

        private Value Product()
        {
            Value left = Unary();
            while (true)
            {
                if (Eat("*")) { Value a = left, b = Unary(); left = s => a(s) * b(s); }
                else if (Eat("/")) { Value a = left, b = Unary(); left = s => { int d = b(s); return d == 0 ? 0 : a(s) / d; }; }
                else return left;
            }
        }

        private Value Unary()
        {
            if (Eat("-")) { Value a = Unary(); return s => -a(s); }
            Skip();
            if (at < text.Length && text[at] == '!' && (at + 1 >= text.Length || text[at + 1] != '='))
            {
                at++;
                Value a = Unary();
                return s => a(s) == 0 ? 1 : 0;
            }
            return Atom();
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '#' || c == '+';

        private string Word(bool allowPlus)
        {
            Skip();
            int start = at;
            while (at < text.Length && IsWordChar(text[at]) && (allowPlus || text[at] != '+')) at++;
            return text.Substring(start, at - start);
        }

        private Value Atom()
        {
            if (Eat("("))
            {
                Value inner = Ternary();
                if (!Eat(")")) Fail("missing )");
                return inner;
            }
            string word = Word(allowPlus: false);
            if (word.Length == 0)
            {
                Fail("expected a number or path");
                return s => 0;
            }
            Skip();
            if (at < text.Length && text[at] == '(')
            {
                at++;
                return Call(word);
            }
            if (word == "true") return s => 1;
            if (word == "false") return s => 0;
            if (int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out int number)) return s => number;
            if (Dice(word, out int count, out int sides))
            {
                return s => Sum(s, count, sides);
            }
            Value reader = Reader(word, named);
            if (reader == null)
            {
                Fail("unknown path '" + word + "' (paths start with self., target., player., item., with., run., role., arg., game. or val.)");
                return s => 0;
            }
            return reader;
        }

        private static int Sum(IScope s, int count, int sides)
        {
            int total = 0;
            for (int i = 0; i < count; i++) total += s.Run.Roll(sides);
            return total;
        }

        private List<Value> Arguments()
        {
            var list = new List<Value>();
            if (Eat(")")) return list;
            do list.Add(Ternary()); while (Eat(","));
            if (!Eat(")")) Fail("missing ) after arguments");
            return list;
        }

        private static IEnumerable<IScope> EachEnemy(IScope s)
        {
            return s.Run.Enemies.Where(e => e.Active).Select(e => s.With(s.Self, e, s.Item));
        }

        // "#a+b" means carrying both tags on one item; anything else is an item id.
        private static Func<IItem, bool> ItemSpec(string spec)
        {
            if (!spec.StartsWith("#")) return item => item.Id == spec;
            string[] tags = spec.Split('+').Select(t => t.TrimStart('#')).ToArray();
            return item => tags.All(item.HasTag);
        }

        private static IEnumerable<IItem> Carried(IScope s, Func<IItem, bool> spec) => s.Run.Inventory.Items.Where(spec);

        private Value Call(string name)
        {
            switch (name)
            {
                case "carried":
                {
                    Func<IItem, bool> spec = ItemSpec(Word(allowPlus: true));
                    if (!Eat(")")) Fail("carried() takes one item id or #group");
                    return s => Carried(s, spec).Sum(i => i.Count);
                }
                case "best_item":
                {
                    Func<IItem, bool> spec = ItemSpec(Word(allowPlus: true));
                    if (!Eat(",")) Fail("best_item(items, value, default)");
                    Value value = Ternary();
                    if (!Eat(",")) Fail("best_item(items, value, default)");
                    Value fallback = Ternary();
                    if (!Eat(")")) Fail("missing ) after best_item");
                    return s =>
                    {
                        List<int> values = Carried(s, spec).Select(i => value(s.With(s.Self, s.Target, i))).ToList();
                        return values.Count > 0 ? values.Max() : fallback(s);
                    };
                }
                case "intel":
                {
                    string id = Word(allowPlus: false);
                    if (!Eat(")")) Fail("intel() takes one id");
                    return s => s.Run.Intel.TryGetValue(id, out List<string> picks) ? picks.Count : 0;
                }
                case "mod":
                {
                    string id = Word(allowPlus: false);
                    if (id.Length == 0 || !Eat(")")) Fail("mod() takes one mod id");
                    int present = hasMod?.Invoke(id) == true ? 1 : 0;
                    return s => present;
                }
                case "list_size":
                {
                    string list = Word(allowPlus: false);
                    if (!Eat(")")) Fail("list_size() takes one name");
                    return s => s.Run.Roster(list).Count;
                }
            }

            List<Value> args = Arguments();
            Value Need(int i) => i < args.Count ? args[i] : (s => 0);
            switch (name)
            {
                case "min":
                    if (args.Count < 2) Fail("min needs at least two values");
                    return s => args.Min(a => a(s));
                case "max":
                    if (args.Count < 2) Fail("max needs at least two values");
                    return s => args.Max(a => a(s));
                case "clamp":
                    if (args.Count != 3) Fail("clamp(value, low, high)");
                    return s => Math.Max(Need(1)(s), Math.Min(Need(2)(s), Need(0)(s)));
                case "dice":
                    if (args.Count != 2) Fail("dice(count, sides)");
                    return s => Sum(s, Need(0)(s), Need(1)(s));
                case "scheduled":
                    if (args.Count != 1) Fail("scheduled(depth)");
                    return s => s.Run.Flow.ScheduledAt(Need(0)(s)) != null ? 1 : 0;
                case "count_enemies":
                    if (args.Count > 1) Fail("count_enemies(where)");
                    return s => EachEnemy(s).Count(e => args.Count == 0 || args[0](e) != 0);
                case "any_enemy":
                    if (args.Count != 1) Fail("any_enemy(condition)");
                    return s => EachEnemy(s).Any(e => args[0](e) != 0) ? 1 : 0;
                case "all_enemies":
                    if (args.Count != 1) Fail("all_enemies(condition)");
                    return s => EachEnemy(s).All(e => args[0](e) != 0) ? 1 : 0;
                case "sum_enemies":
                    if (args.Count < 1 || args.Count > 2) Fail("sum_enemies(value, where)");
                    return s => EachEnemy(s).Where(e => args.Count < 2 || args[1](e) != 0).Sum(e => args[0](e));
                case "max_enemies":
                    if (args.Count < 1 || args.Count > 2) Fail("max_enemies(value, where)");
                    return s => EachEnemy(s).Where(e => args.Count < 2 || args[1](e) != 0).Select(e => args[0](e)).DefaultIfEmpty(0).Max();
            }
            Fail("unknown function '" + name + "'");
            return s => 0;
        }

        private static bool Dice(string word, out int count, out int sides)
        {
            count = 1;
            sides = 0;
            int d = word.IndexOf('d');
            if (d < 0) return false;
            if (d > 0 && !int.TryParse(word.Substring(0, d), NumberStyles.None, CultureInfo.InvariantCulture, out count)) return false;
            return int.TryParse(word.Substring(d + 1), NumberStyles.None, CultureInfo.InvariantCulture, out sides) && sides > 0 && count > 0;
        }

        private void Fail(string message) { if (error == null) error = message; }
    }
}
