# Telling intel through ZoH Framework

A pack can let customers tell the player its [told intel](intel.md#told-slots): a customer says a line, and the card it
is about appears on the area's map card at that moment. This needs no C#. The pack ships a
ZoH Framework story that uses two verbs the Expedition Framework provides, `/ef.rumour` and
`/ef.tell`.

ZoH Framework is a separate mod and stays optional. Without it nobody reads the story, nothing claims the pack, and its
told slots roll with the day like any other slot, so the pack keeps all its intel.

## What the pack ships

1. Intel slots with `"told": true` in an area. [Told slots](intel.md#told-slots)
2. `Stories/main.zoh` with `uses: ["expedition_framework"];` in its header. That line is what gives the story the
   `ef.*` verbs, and it makes the framework claim the pack, so the pack's told slots wait to be told.
3. A line of text for every card a customer can tell, in the pack's `Localization/<language>.csv`. See
   [The text keys](#the-text-keys).

The manifest needs no prerequisite on ZoH Framework for this: it reads `Stories/main.zoh` of every enabled mod. The
pack's id in its manifest is both its pack id here and its consumer id there, so the verbs reach this pack's areas and no
other pack's.

## The verbs

Both work only while a customer is talking (ZoH Framework's live pass).

| Verb | Parameters | Gives back |
|---|---|---|
| `/ef.rumour;` | `key:` a key template, default `"rumour_{pick}"`. `area:` only this area (its id, with or without the pack). `slot:` only this slot id. `tell:` `false` to reserve the card instead of showing it, default `true` | The text key of the card it picked. `nothing` when no card can be told now |
| `/ef.tell "key";` | A key that `/ef.rumour tell: false` gave back today | `true` when the card is now showing. `false` for a key that was never reserved, was already told, or is from an earlier day |

`/ef.rumour` picks one told slot of the pack that can still tell today, then one id of its pool that has no card yet.
What "can still tell" means (the slot's budget, `when`, `intel_max`, undiscovered areas) is in
[Told slots](intel.md#told-slots). When every budget is spent, or the pack has no told slot, it gives `nothing`: the
story should then say nothing about a rumour.

With `tell: false` the card is reserved: it counts toward the slot's budget and the area's `intel_max`, but it is not on
the map until `/ef.tell` is called with its key. Use this to let the player choose whether to hear it. A reserved card
that is never told is gone when the day ends. Two reservations that get the same key (the same template and the same
pick) leave only the second one tellable.

A story that uses the framework from a mod that isn't an expedition pack gets `nothing` from `/ef.rumour` and one
warning in the log.

## The text keys

`/ef.rumour` gives back a text key, ready to hand to `/converse`. The key is the template with these filled in:

| Placeholder | Becomes |
|---|---|
| `{pick}` | The id the card names, without its pack: `fence` for `my_pack:fence` |
| `{area}` | The area's id, without its pack |
| `{slot}` | The slot's id |

With the default template, a slot whose pool is `["fence", "guard_dog"]` needs the keys `rumour_fence` and
`rumour_guard_dog` in the pack's CSV. A pack with told slots in several areas can keep their lines apart with
`key: "rumour_{area}_{pick}"`.

Lint can't check keys built this way, as with [the picked id in text](intel.md#the-picked-id-in-text). Every id in the
pool of every told slot needs its key; a missing one is shown to the player as the key itself.

## An example story

Story syntax is ZoH Framework's; its README is the reference for it, and for the `smalltalk` header used here.

```zoh
Yard rumours
uses: ["expedition_framework"];
smalltalk: { "chance": 20, "at": ["arrive"] };
required_verbs: ["ps.entry"];
===
/ps.entry; -> *entry;
/jump ?, *entry;

@schedule
/exit;

@smalltalk_arrive
/ef.rumour; -> *key;
/if *key, is: ?, /exit;;
/converse *key;
/exit;
```

The keys of the `smalltalk` map are quoted; without the quotes the story fails to load. Each `@smalltalk_*` entry ends
with `/exit;`, or the talk runs on into whatever label follows it.

A regular customer who makes small talk on arriving asks for a rumour. If there is one, the customer says its line and the
card is on the map. If there is none, the story ends without a line and the customer behaves as usual.

Letting the player decide, with a reserved card:

```zoh
@smalltalk_arrive
/ef.rumour tell: false; -> *key;
/if *key, is: ?, /exit;;
/choose/
    prompt: "rumour_offer"
    true "rumour_listen" "yes"
    true "rumour_wave_off" "no"
/; -> *pick;
/if *pick, is: "yes", /ef.tell *key;;
/if *pick, is: "yes", /converse *key;;
/exit;
```

`ef.tell` stays out of `required_verbs` as well.

## `required_verbs`

List only the verbs the story can't do without, as above, and leave the `ef.*` verbs out. ZoH Framework refuses to load a
story when a verb in its `required_verbs` list is missing, and the `ef.*` verbs are missing whenever the Expedition
Framework is missing or disabled. `required_verbs: "*"` requires every verb the story calls, so it fails the same way.

With the `ef.*` verbs left out of the list, a missing provider only skips their lines, with a warning in the log.
`*key` stays `nothing`, and the story above ends quietly.

## Checking it in the game

- ZoH Framework's log names the verbs each story got: `ef.rumour, ef.tell` for the pack's story.
- A new day shows no card for the pack's told slots until a customer tells one.
- The debug panel's stand-in teller ([Told slots](intel.md#told-slots)) tells a card without waiting for a customer.
