using System;
using System.Collections.Generic;
using System.Linq;
using ExpeditionFramework.Api;

namespace ExpeditionFramework.Core
{
    /// <summary>The standard vocabulary. Registered through the same <see cref="Registry"/> a mod would use.</summary>
    internal static class Builtins
    {
        public static Effect Sequence(List<Effect> parts)
        {
            return s =>
            {
                foreach (Effect part in parts)
                {
                    part(s);
                    if (s.Run.Over) return;
                }
            };
        }

        public static Condition All(List<Condition> parts) => s => parts.All(p => p(s));

        private static string Local(string id) => id.Substring(id.IndexOf(':') + 1);

        /// <summary>Who an effect applies to: self, target, player or role.NAME. Falls back to the given default.</summary>
        private static Func<IScope, IEntity> Who(ISpec spec, string key, string fallback)
        {
            string who = spec.Text(key, fallback);
            if (who != null && who.StartsWith("role."))
            {
                string role = who.Substring(5);
                return s => s.Run.Role(role);
            }
            switch (who)
            {
                case "self": return s => s.Self;
                case "target": return s => s.Target;
                case "player": return s => s.Run.Player;
            }
            spec.Error("\"" + key + "\" must be self, target, player or role.NAME");
            return s => null;
        }

        private static int Pick(IScope s, IList<int> weights)
        {
            int total = weights.Sum();
            if (total <= 0) return -1;
            int roll = s.Run.Roll(total);
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0) return i;
            }
            return weights.Count - 1;
        }

        public static void Register(Registry r)
        {
            RegisterConditions(r);
            RegisterControl(r);
            RegisterWorld(r);
            RegisterCast(r);
            RegisterCombat(r);
            RegisterItems(r);
            RegisterTrade(r);
        }

        private static void RegisterConditions(Registry r)
        {
            r.Conditions["all"] = c => Compose(c, parts => s => parts.All(p => p(s)));
            r.Conditions["any"] = c => Compose(c, parts => s => parts.Any(p => p(s)));
            r.Conditions["not"] = c =>
            {
                var spec = (PackLoader.Spec)c;
                if (c.Has("value"))
                {
                    Condition inner = c.Condition("value");
                    return s => !inner(s);
                }
                // {"not": null} and {"not": {}} name no condition; without this they'd surface as a confusing "unknown condition value".
                if (spec.Args.Count == 0 || spec.Args.ContainsKey("value"))
                {
                    c.Error("needs a condition");
                    return s => false;
                }
                // {"not": {"scene": "combat"}} arrives as the condition's own fields, not under "value".
                Condition whole = spec.WholeCondition();
                return s => !whole(s);
            };
            r.Conditions["chance"] = c => { Value v = c.Value("value", 0); return s => s.Run.Roll(100) <= v(s); };
            r.Conditions["has_tag"] = c =>
            {
                Func<IScope, IEntity> who = Who(c, "who", "target");
                string tag = c.Require("tag");
                return s => who(s)?.HasTag(tag) == true;
            };
            r.Conditions["has_status"] = c =>
            {
                Func<IScope, IEntity> who = Who(c, "who", "target");
                string id = c.Ref(c.Require("status") ?? "", "status");
                return s => who(s)?.HasStatus(id) == true;
            };
            r.Conditions["carrying"] = c =>
            {
                var spec = (PackLoader.Spec)c;
                ItemFilter filter;
                if (c.Has("value")) filter = c.Items("value");
                else if (spec.Args.Count == 0 || spec.Args.ContainsKey("value"))
                {
                    // Without this, a missing filter would quietly match any item.
                    c.Error("needs an item filter");
                    return s => false;
                }
                // {"carrying": {"all": [...]}} arrives as the filter's own fields, not under "value".
                else filter = spec.WholeItems();
                return s => s.Run.Inventory.Items.Any(i => filter(i, s));
            };
            r.Conditions["scene"] = c => { string scene = c.Require("value"); return s => s.Run.Scene == scene; };
            r.Conditions["intent_interruptible"] = c =>
            {
                Func<IScope, IEntity> who = Who(c, "who", "target");
                return s => who(s)?.IntentInterruptible == true;
            };
            r.Conditions["fits"] = c => s => s.Run.Inventory.Fits(s.Item, s.Paired);
            r.Conditions["credits_at_least"] = c => { Value v = c.Value("value", 0); return s => s.Run.Inventory.Credits >= v(s); };
        }

        private static Condition Compose(ISpec c, Func<List<Condition>, Condition> make)
        {
            return make(c.Conditions("value").ToList());
        }

        /// <summary>A d20 (or any die) against a number. Sets run.natural and run.total.</summary>
        internal sealed class DieCheck
        {
            private Value sides;
            private Value modifier;
            private Value target;
            private Value failAt;
            private Value passAt;

            public static DieCheck From(ISpec spec)
            {
                if (!spec.Has("dc")) spec.Error("needs \"dc\"");
                return new DieCheck
                {
                    sides = spec.Value("die", 20),
                    modifier = spec.Value("mod", 0),
                    target = spec.Value("dc", 0),
                    failAt = spec.Value("fail_at", 0),
                    passAt = spec.Has("pass_at") ? spec.Value("pass_at", 20) : null,
                };
            }

            private int PassAt(IScope s, int die) => passAt != null ? passAt(s) : die;

            public int Percent(IScope s)
            {
                int die = sides(s);
                return Lines.Percent(die, modifier(s), target(s), failAt(s), PassAt(s, die));
            }

            public bool Roll(IScope s)
            {
                IRun run = s.Run;
                int die = sides(s);
                int natural = run.RollCheck(die);
                int mod = modifier(s);
                int need = target(s);
                run.Set("natural", natural);
                run.Set("total", natural + mod);
                run.NoteCheck(die, natural, mod, need);
                return natural > failAt(s) && (natural >= PassAt(s, die) || natural + mod >= need);
            }
        }

        private static void RegisterControl(Registry r)
        {
            r.Effects["if"] = c =>
            {
                Condition when = c.Condition("when");
                Effect then = c.Effect("then");
                Effect otherwise = c.Effect("else");
                return s => { if (when(s)) then(s); else otherwise(s); };
            };
            r.Effects["cond"] = CompileCond;
            r.Effects["check"] = CompileCheck;
            r.Effects["roll"] = c =>
            {
                Value sides = c.Value("die", 20);
                Value modifier = c.Value("mod", 0);
                bool note = c.Flag("note", true);
                Effect body = c.Effect("do");
                return s =>
                {
                    IRun run = s.Run;
                    int die = sides(s);
                    int natural = run.RollCheck(die);
                    run.Set("natural", natural);
                    run.Set("total", natural + modifier(s));
                    if (note) run.NoteDie(die, natural);
                    body(s);
                };
            };
            r.Effects["repeat"] = c =>
            {
                Value count = c.Value("count", 1);
                Effect body = c.Effect("do");
                return s =>
                {
                    for (int n = count(s); n > 0 && !s.Run.Over; n--) body(s);
                };
            };
            r.Effects["once"] = c =>
            {
                string key = c.Text("key", c.Path);
                Effect body = c.Effect("do");
                Effect again = c.Effect("else");
                return s => { if (s.Run.FirstTime(key)) body(s); else again(s); };
            };
            r.Effects["random"] = c =>
            {
                var weights = new List<int>();
                var bodies = new List<Effect>();
                foreach (ISpec entry in c.Entries("value"))
                {
                    weights.Add(entry.Number("weight", 1));
                    bodies.Add(entry.Effect("do"));
                }
                if (bodies.Count == 0) c.Error("needs a list of {weight, do}");
                return s =>
                {
                    int picked = Pick(s, weights);
                    if (picked >= 0) bodies[picked](s);
                };
            };
            r.Effects["for_each"] = c =>
            {
                string list = c.Text("in", "enemies");
                Condition where = c.Condition("where");
                Effect body = c.Effect("do");
                return s =>
                {
                    IEnumerable<IEntity> members;
                    if (list == "enemies") members = s.Run.Enemies.Where(e => e.Active);
                    else if (list == "others") members = s.Run.Enemies.Where(e => e.Active && e != s.Self);
                    else members = s.Run.Roster(list);
                    foreach (IEntity e in members.ToList())
                    {
                        IScope inner = s.With(s.Self, e, s.Item);
                        if (where(inner)) body(inner);
                        if (s.Run.Over) return;
                    }
                };
            };
            r.Effects["bind"] = c =>
            {
                Func<IScope, IEntity> self = c.Has("self") ? Who(c, "self", null) : null;
                Func<IScope, IEntity> target = c.Has("target") ? Who(c, "target", null) : null;
                Effect body = c.Effect("do");
                return s => body(s.With(self != null ? self(s) : s.Self, target != null ? target(s) : s.Target, s.Item));
            };
            r.Effects["set"] = c => Assign(c, (old, v) => v);
            r.Effects["add"] = c => Assign(c, (old, v) => old + v);
            r.Effects["set_text"] = c =>
            {
                string target = c.Require("var") ?? "";
                if (Expr.IsPackMemory(target))
                {
                    c.Error(ReadOnlyMemory(target));
                    return s => { };
                }
                Action<IScope, string> write = Expr.TextWriter(target, c.PackId);
                if (write == null)
                {
                    c.Error("\"var\" must be a path like run.$name, memory.$name or self.$name");
                    return s => { };
                }
                var spec = c as PackLoader.Spec;
                spec?.NoteMemory(target, write: true);
                if (c.Has("line"))
                {
                    ILine line = c.Line("line");   // its {memory.$x} pieces are noted where the line compiles
                    return s => write(s, line.Render(s));
                }
                string source = c.Require("value") ?? "";
                Func<IScope, string> read = Expr.TextReader(source, fieldRequired: false, pack: c.PackId);
                if (read == null)
                {
                    c.Error("\"value\" must be a path like item.name, or use \"line\"");
                    return s => { };
                }
                spec?.NoteMemory(source, write: false);
                return s => write(s, read(s));
            };
        }

        private static Effect CompileCond(ISpec c)
        {
            var branches = new List<(Condition When, Effect Do)>();
            foreach (ISpec entry in c.Entries("value")) branches.Add((entry.Condition("when"), entry.Effect("do")));
            if (branches.Count == 0) c.Error("needs a list of {when, do}");
            return s =>
            {
                foreach ((Condition when, Effect body) in branches)
                {
                    if (!when(s)) continue;
                    body(s);
                    return;
                }
            };
        }

        private static Effect CompileCheck(ISpec c)
        {
            Effect success = c.Effect("success");
            Effect failure = c.Effect("failure");
            if (c.Has("chance"))
            {
                Value chance = c.Value("chance", 0);
                return s =>
                {
                    int need = Math.Max(0, Math.Min(100, s.Run.Decide(new Decision("chance", s, chance(s))).Value));
                    int roll = s.Run.Roll(100);
                    s.Run.NoteRoll("d100 " + roll + " / " + need);
                    if (roll <= need) success(s); else failure(s);
                };
            }
            if (c.Has("die"))
            {
                DieCheck die = DieCheck.From(c);
                return s => { if (die.Roll(s)) success(s); else failure(s); };
            }
            if (!c.Has("roll") || !c.Has("dc")) c.Error("a check needs \"chance\", \"die\" and \"dc\", or \"roll\" and \"dc\"");
            Value dice = c.Value("roll", 0);
            Value dc = c.Value("dc", 0);
            return s =>
            {
                int total = dice(s);
                int need = dc(s);
                s.Run.NoteRoll(total + " / " + need);
                if (total >= need) success(s); else failure(s);
            };
        }

        private static string ReadOnlyMemory(string path) =>
            path + " is a pack's memory read by name, which can't be changed; a pack changes its own memory as memory.NAME";

        private static Effect Assign(ISpec c, Func<int, int, int> apply)
        {
            string path = c.Require("var") ?? "";
            if (Expr.IsPackMemory(path))
            {
                c.Error(ReadOnlyMemory(path));
                return s => { };
            }
            Value read = Expr.Reader(path, raw: true, pack: c.PackId);
            Action<IScope, int> write = Expr.Writer(path, c.PackId);
            if (read == null || write == null)
            {
                c.Error("\"var\" must be a path like run.alarm, memory.debt or self.armor");
                return s => { };
            }
            if (path.StartsWith("run.") && Array.IndexOf(ExpeditionRun.EngineVars, path.Substring(4)) >= 0)
            {
                c.Error(path + " is kept by the engine; set and add can't change it");
                return s => { };
            }
            // add reads its own var too, but only to change it, so set and add both count as writing it.
            (c as PackLoader.Spec)?.NoteMemory(path, write: true);
            Value value = c.Value("value", 0);
            return s => write(s, apply(read(s), value(s)));
        }

        private static void RegisterWorld(Registry r)
        {
            r.Effects["say"] = c =>
            {
                ILine line = c.InlineLine();
                string tone = c.Text("tone", "plain");
                // Warned through the concrete Spec, not ISpec.Warn: a pack shipping an older ExpeditionFramework.Api.dll that
                // wins the load has no ISpec.Warn, and calling it would throw MissingMethodException mid-compile.
                if (Array.IndexOf(Schema.Tones, tone) < 0) ((PackLoader.Spec)c).Warn(PackLoader.UnknownTone(tone, Schema.Tones));
                return s => s.Run.Say(tone, line, s);
            };
            r.Effects["hint"] = c =>
            {
                ItemFilter missing = c.Items("missing");
                ILine line = c.Line("text");
                if (line == null) c.Error("needs \"text\"");
                return s =>
                {
                    if (s.Run.Inventory.Items.Any(i => missing(i, s))) return;
                    if (s.Run.FirstTime("hint:" + line.Render(s))) s.Run.Say("hint", line, s);
                };
            };
            r.Effects["sound"] = c => { string id = c.Require("value"); return s => s.Run.PlaySound(id); };
            r.Effects["event"] = c =>
            {
                if (c.Has("scheduled"))
                {
                    Value at = c.Value("scheduled", 0);
                    return s =>
                    {
                        int depth = at(s);
                        string id = s.Run.Flow.ScheduledAt(depth);
                        if (id == null) return;
                        s.Run.Flow.Unschedule(depth);
                        s.Run.Flow.StartEvent(id, s);
                    };
                }
                IPool pool = c.Pool("event");
                return s => { string e = pool.Pick(s); if (e != null) s.Run.Flow.StartEvent(e, s); };
            };
            r.Effects["end_event"] = c => s => s.Run.Flow.EndEvent();
            r.Effects["stay"] = c => s => s.Run.Flow.StayInEvent();
            r.Effects["next_room"] = c => s => s.Run.Flow.NextRoom();
            r.Effects["retreat"] = c => s => s.Run.Flow.Retreat();
            r.Effects["leave"] = c => { string outcome = c.Text("value", c.Text("outcome", "left")); return s => s.Run.Finish(outcome); };
            r.Effects["schedule"] = CompileSchedule;
            r.Effects["unschedule"] = c => { Value at = c.Value("value", 0); return s => s.Run.Flow.Unschedule(at(s)); };
            r.Effects["wound"] = c => { string kind = c.Require("value"); return s => s.Run.Wound(kind); };
            // Changes reputation with a faction and logs it the way the game logs its own overnight reputation changes: its
            // own text, green for a gain, red for a loss. night_log replaces the text (arg.rep is the change), quiet logs nothing.
            r.Effects["reputation"] = c =>
            {
                string faction = c.Require("faction");
                if (faction != null && !GameReads.IsFaction(faction)) c.Error(GameReads.UnknownFaction(faction));
                Value amount = c.Value("value", 0);
                ILine logged = c.Line("night_log");
                string color = c.Text("color");
                bool quiet = c.Flag("quiet", false);
                return s =>
                {
                    int value = amount(s);
                    if (value == 0) return;
                    string line = s.Run.Reputation(faction, value);
                    if (line == null || quiet) return;
                    if (logged != null) line = logged.Render(s.WithArg("rep", value));
                    s.Run.NightLog(line, color ?? (value > 0 ? "5c6f46" : "9c3738"));
                };
            };
            r.Effects["night_log"] = c =>
            {
                ILine line = c.InlineLine();
                string color = c.Text("color");
                return s => s.Run.NightLog(line.Render(s), color);
            };
            r.Effects["emit"] = c =>
            {
                string hook = c.Hook("value");
                Func<IScope, IEntity> who = Who(c, "who", "self");
                Func<IScope, IEntity> other = c.Has("other") ? Who(c, "other", null) : (s => s.Target);
                return s => s.Run.Emit(hook, who(s), other(s));
            };
        }

        /// <summary>
        /// Puts events at future depths. With "intel", what the street said goes at different random depths up to "depths".
        /// With "pool", one event goes at "at", unless something is due there already. run.$scheduled names what's due.
        /// </summary>
        private static Effect CompileSchedule(ISpec c)
        {
            Effect then = c.Effect("then");
            if (c.Has("intel"))
            {
                string intel = c.Text("intel");
                Value depths = c.Value("depths", 0);
                return s =>
                {
                    IRun run = s.Run;
                    if (!run.Intel.TryGetValue(intel, out List<string> picks)) return;
                    List<int> open = Enumerable.Range(1, Math.Max(0, depths(s))).ToList();
                    foreach (string id in picks)
                    {
                        if (open.Count == 0) break;
                        int at = open[s.Run.Roll(open.Count) - 1];
                        open.Remove(at);
                        run.Flow.Schedule(at, id);
                    }
                    then(s);
                };
            }
            IPool pool = c.Pool("event");
            Value when = c.Value("at", 0);
            return s =>
            {
                IRun run = s.Run;
                int at = when(s);
                string due = run.Flow.ScheduledAt(at);
                if (due == null)
                {
                    due = pool.Pick(s);
                    if (due == null) return;
                    run.Flow.Schedule(at, due);
                }
                run.SetText("scheduled", Local(due));
                then(s);
            };
        }

        // ---- Who's in the story ----

        private static void RegisterCast(Registry r)
        {
            r.Effects["cast"] = c =>
            {
                string role = c.Require("as");
                string fromList = c.Text("from_list");
                bool take = c.Flag("take", false);
                string except = c.Text("except");
                Condition where = c.Has("where") ? c.Condition("where") : null;
                Func<IScope, IReadOnlyList<string>> source = fromList != null ? null : c.Enemies();
                string fallback = c.Has("fallback") ? c.Ref(c.Text("fallback"), "enemy") : null;
                return s =>
                {
                    IRun run = s.Run;
                    IEnumerable<string> ids = fromList != null ? run.Roster(fromList).Select(e => e.Id).ToList() : source(s);
                    string excluded = except != null ? run.Role(except)?.Id : null;
                    List<IEntity> open = ids.Where(id => id != excluded).Select(run.Combat.Make)
                        .Where(e => where == null || where(s.With(s.Self, e, s.Item))).ToList();
                    IEntity chosen = open.Count > 0 ? open[s.Run.Roll(open.Count) - 1] : null;
                    if (chosen == null && fallback != null) chosen = run.Combat.Make(fallback);
                    run.SetRole(role, chosen);
                    if (chosen != null && take) run.Roster(fromList).Remove(run.Roster(fromList).First(e => e.Id == chosen.Id));
                };
            };
            r.Effects["uncast"] = c => { string role = c.Require("value"); return s => s.Run.SetRole(role, null); };
            r.Effects["list_add"] = c =>
            {
                string list = c.Require("list");
                Func<IScope, IEntity> who = Who(c, "who", "self");
                return s => { if (who(s) is IEntity e) s.Run.Roster(list).Add(e); };
            };
            r.Effects["list_clear"] = c => { string list = c.Require("value"); return s => s.Run.Roster(list).Clear(); };
            r.Effects["muster"] = CompileMuster;
            r.Effects["engage"] = c =>
            {
                Effect won = c.Effect("won");
                Effect prepare = c.Has("prepare") ? c.Effect("prepare") : null;
                Value surprised = c.Value("surprised", 0);
                return s => s.Run.Combat.Engage(won, s, surprised(s) != 0, prepare);
            };
            r.Effects["focus"] = c => { Func<IScope, IEntity> who = Who(c, "who", "target"); return s => { if (who(s) is IEntity e) s.Run.Combat.SetFocus(e); }; };
            r.Effects["pick_enemy"] = c =>
            {
                Condition where = c.Condition("where");
                Value order = c.Has("order") ? c.Value("order", 0) : null;
                Value max = c.Value("max", 1);
                Effect body = c.Effect("do");
                Effect none = c.Effect("else");
                return s =>
                {
                    IEnumerable<IScope> open = s.Run.Enemies.Where(e => e.Active).Select(e => s.With(s.Self, e, s.Item)).Where(x => where(x));
                    if (order != null) open = open.OrderBy(x => order(x));
                    List<IScope> picked = open.Take(max(s)).ToList();
                    if (picked.Count == 0) none(s);
                    foreach (IScope scope in picked) body(scope);
                };
            };
        }

        private static Effect CompileMuster(ISpec c)
        {
            bool add = c.Flag("add", false);
            Effect each = c.Effect("do");
            IPool encounters = null;
            Func<IScope, IReadOnlyList<string>> group = null;
            if (c.Has("role") || c.Has("enemy")) group = c.Enemies();
            else encounters = c.Pool("encounter");
            return s =>
            {
                IRun run = s.Run;
                int before = add ? run.Enemies.Count : 0;
                if (group != null)
                {
                    run.Combat.Muster(group(s).Select(run.Combat.Make).ToList(), add);
                }
                else
                {
                    string picked = encounters.Pick(s);
                    if (picked == null) return;
                    run.Combat.MusterEncounter(picked, add);
                }
                foreach (IEntity arrived in run.Enemies.Skip(before).ToList()) each(s.With(s.Self, arrived, s.Item));
            };
        }

        private static void RegisterCombat(Registry r)
        {
            r.Effects["fight"] = c =>
            {
                IPool pool = c.Pool("encounter");
                Effect won = c.Effect("won");
                Value surprised = c.Value("surprised", 0);
                return s => { string e = pool.Pick(s); if (e != null) s.Run.Combat.StartFight(e, won, s, surprised(s) != 0); };
            };
            r.Effects["end_fight"] = c => { Value won = c.Value("won", 1); return s => s.Run.Combat.EndFight(won(s) != 0); };
            r.Effects["enemy_turn"] = c => s => s.Run.Combat.EnemyTurn();
            r.Effects["damage"] = c =>
            {
                Func<IScope, IEntity> to = Who(c, "to", "target");
                Value amount = c.Value("amount", 0);
                bool pierce = c.Text("armor", "") == "ignore";
                return s => { IEntity t = to(s); if (t != null) s.Run.Damage(t, amount(s), s.Self, pierce); };
            };
            r.Effects["heal"] = c =>
            {
                Func<IScope, IEntity> to = Who(c, "to", "player");
                Value amount = c.Value("amount", 0);
                return s =>
                {
                    IEntity t = to(s);
                    if (t != null) s.Run.Combat.Heal(t, amount(s));
                };
            };
            r.Effects["status"] = c =>
            {
                Func<IScope, IEntity> to = Who(c, "to", "target");
                string id = c.Ref(c.Require("id") ?? "", "status");
                Value turns = c.Has("turns") ? c.Value("turns", -1) : null;
                Value rooms = c.Has("rooms") ? c.Value("rooms", -1) : null;
                return s =>
                {
                    if (to(s) is IEntity t) s.Run.Combat.AddStatus(t, id, turns?.Invoke(s), rooms?.Invoke(s));
                };
            };
            r.Effects["clear_status"] = c =>
            {
                Func<IScope, IEntity> to = Who(c, "to", "target");
                string id = c.Ref(c.Require("id") ?? "", "status");
                return s => { if (to(s) is IEntity t) s.Run.Combat.ClearStatus(t, id); };
            };
            r.Effects["defeat"] = c => { Func<IScope, IEntity> who = Who(c, "who", "target"); return s => { if (who(s) is IEntity e) s.Run.Combat.Defeat(e, s.Self); }; };
            r.Effects["remove"] = c => { Func<IScope, IEntity> who = Who(c, "who", "self"); return s => { if (who(s) is IEntity e) s.Run.Combat.Remove(e); }; };
            r.Effects["spawn"] = c =>
            {
                Func<IScope, IReadOnlyList<string>> source = c.Enemies();
                Value surprised = c.Value("surprised", 0);
                Effect then = c.Effect("then");
                Effect none = c.Effect("else");
                return s =>
                {
                    if (s.Run.Scene != "combat")
                    {
                        s.Run.Log("bad", "spawn does nothing outside a fight");
                        none(s);
                        return;
                    }
                    IReadOnlyList<string> open = source(s);
                    if (open.Count == 0)
                    {
                        none(s);
                        return;
                    }
                    IEntity arrival = s.Run.Combat.Make(open[s.Run.Roll(open.Count) - 1]);
                    s.Run.Combat.Spawn(arrival, surprised(s) != 0);
                    then(s.With(s.Self, arrival, s.Item));
                };
            };
            r.Effects["intent"] = c =>
            {
                Func<IScope, IEntity> who = Who(c, "who", "target");
                string id = c.Ref(c.Require("id") ?? "", "intent");
                return s => { if (who(s) is IEntity e) s.Run.Combat.SetIntent(e, id); };
            };
        }

        private static void RegisterItems(Registry r)
        {
            r.Effects["consume"] = c =>
            {
                Value count = c.Value("value", c.Has("count") ? 0 : 1);
                if (c.Has("count")) count = c.Value("count", 1);
                bool paired = c.Text("what", "item") == "with";
                return s => s.Run.Inventory.Consume(paired ? s.Paired : s.Item, count(s));
            };
            // Each ammo item is worth its `rounds`, whatever gun it goes into, and reload takes as many rounds as fit.
            // Rounds already loaded stay unless the new ammo is a different kind. No round is thrown away: an item opened
            // but not emptied stays carried, and the ammo's `spent` remembers how much of it is gone, so the next reload
            // draws from it first. An item is used up only once all its rounds are in a gun.
            r.Effects["reload"] = c => s =>
            {
                IItem gun = s.Item;
                if (gun == null) return;
                int capacity = gun.Get("capacity");
                IItem ammo = s.Paired;
                if (ammo == null)
                {
                    gun.Set("loaded", capacity);
                    gun.Set("round_kind", 0);
                    gun.SetText("round", "");
                    return;
                }
                int kind = ammo.Get("kind");
                int kept = kind == gun.Get("round_kind") ? Math.Max(0, Math.Min(gun.Get("loaded"), capacity)) : 0;
                int perItem = ammo.Get("rounds");
                int used;
                if (perItem <= 0)
                {
                    // Ammo with no `rounds` stat: one item fills any gun.
                    if (ammo.Count <= 0) return;
                    used = 1;
                    s.Run.Inventory.Consume(ammo, 1);
                    gun.Set("loaded", capacity);
                }
                else
                {
                    int spent = Math.Max(0, Math.Min(ammo.Get("spent"), perItem - 1));
                    int take = Math.Min(capacity - kept, ammo.Count * perItem - spent);
                    if (take <= 0) return;
                    used = (spent + take) / perItem;
                    if (used > 0) s.Run.Inventory.Consume(ammo, used);
                    ammo.Set("spent", (spent + take) % perItem);
                    gun.Set("loaded", kept + take);
                }
                gun.Set("round_kind", kind);
                gun.SetText("round", ammo.Name);
                s.Run.Set("reload_used", used);
            };
            r.Effects["for_items"] = c =>
            {
                IItemUse use = c.Use();
                Effect body = c.Effect("do");
                return s =>
                {
                    foreach (IItem item in s.Run.Inventory.Select(use, s, null))
                    {
                        body(s.With(s.Self, s.Target, item));
                        if (s.Run.Over) return;
                    }
                };
            };
            r.Effects["pick_item"] = c =>
            {
                IItemUse use = c.Use();
                bool paired = c.Text("as", "item") == "with";
                Effect body = c.Effect("do");
                Effect none = c.Effect("else");
                return s =>
                {
                    IItem picked = s.Run.Inventory.Select(use, s, paired ? s.Item : null).FirstOrDefault();
                    if (picked == null) none(s);
                    else if (paired) body(s.WithPaired(picked));
                    else body(s.With(s.Self, s.Target, picked));
                };
            };
            r.Effects["equip"] = c =>
            {
                IItemUse use = c.Use();
                Effect then = c.Effect("then");
                return s =>
                {
                    foreach (IItem item in s.Run.Inventory.Select(use, s, null))
                    {
                        if (!s.Run.Inventory.Equip(item)) continue;
                        then(s.With(s.Self, s.Target, item));
                        return;
                    }
                };
            };
            r.Effects["give_item"] = c =>
            {
                string id = c.Require("value");
                return s => s.Run.Inventory.Give(id);
            };
            r.Effects["give_loot"] = CompileGiveLoot;
            r.Effects["credits"] = c =>
            {
                Value amount = c.Value("value", 0);
                return s =>
                {
                    int value = amount(s);
                    if (value >= 0) s.Run.Inventory.AddCredits(value);
                    else s.Run.Inventory.SpendCredits(-value);
                };
            };
            r.Effects["lose_items"] = c =>
            {
                Value percent = c.Value("value", 50);
                Value perCell = c.Value("per_cell", 0);
                Value max = c.Value("max", 100);
                ILine logged = c.Line("night_log");
                string color = c.Text("color");
                return s =>
                {
                    int cell = perCell(s);
                    IReadOnlyList<string> names = cell > 0
                        ? s.Run.Inventory.LoseCarriedBySize(cell, max(s))
                        : s.Run.Inventory.LoseCarried(percent(s));
                    if (logged == null || names.Count == 0) return;
                    s.Run.SetText("lost", string.Join(", ", names));
                    s.Run.NightLog(logged.Render(s.WithArg("lost", names.Count)), color);
                };
            };
            r.Effects["clear_ground"] = c => s => s.Run.Set("cleared_ground", s.Run.Inventory.ClearGround());
            r.Effects["take_back"] = c =>
            {
                Effect then = c.Effect("then");
                Effect none = c.Effect("else");
                return s =>
                {
                    string name = s.Run.Inventory.TakeBack();
                    if (name == null)
                    {
                        none(s);
                        return;
                    }
                    s.Run.SetText("taken", name);
                    s.Run.Set("found_items", Math.Max(0, s.Run.Get("found_items") - 1));
                    then(s);
                };
            };
            r.Effects["lose_found"] = c => s => s.Run.Inventory.LoseFound();
        }

        /// <summary>{"value": table}, {"from": [tables], "index": n} for tiers, or {"drops_of": who} for what someone carries.</summary>
        private static Effect CompileGiveLoot(ISpec c)
        {
            if (c.Has("drops_of"))
            {
                Func<IScope, IEntity> who = Who(c, "drops_of", null);
                return s =>
                {
                    string table = who(s)?.Drops;
                    if (table != null) GiveRolled(s, table);
                    else s.Run.Set("given", 0);
                };
            }
            if (c.Has("from"))
            {
                List<string> tables = c.Strings("from").Select(t => c.Ref(t, "loot")).ToList();
                Value index = c.Value("index", 0);
                return s => GiveRolled(s, tables[Math.Max(0, Math.Min(tables.Count - 1, index(s)))]);
            }
            string id = c.Ref(c.Require("value") ?? "", "loot");
            return s => GiveRolled(s, id);
        }

        private static void GiveRolled(IScope s, string table) => s.Run.Inventory.Give(s.Run.Inventory.RollLoot(table));

        private static void RegisterTrade(Registry r)
        {
            r.Effects["new_cart"] = c =>
            {
                string id = c.Ref(c.Require("value") ?? "", "cart");
                return s => s.Run.Trade.NewCart(id, s);
            };
            r.Effects["trade"] = c =>
            {
                string id = c.Ref(c.Require("cart") ?? "", "cart");
                Effect closed = c.Effect("on_close");
                return s => s.Run.Trade.Open(id, closed, s);
            };
            r.Effects["take_cart"] = c =>
            {
                string id = c.Ref(c.Require("cart") ?? "", "cart");
                Effect before = c.Effect("do");
                return s => s.Run.Trade.TakeCart(id, before, s);
            };
        }
    }
}
