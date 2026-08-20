# `.ai/` — shared project knowledge

For two readers: **whoever picks this project up next**, and **the AI helping them**.

It exists for one reason: this project contains things that cost time to work out and
that neither the code nor the git history will tell you.

## What is here

| Path | Holds | Lifetime |
|---|---|---|
| [decisions/](decisions/) | Choices, what the rejected options lost on, what would reverse them | Reversible — edited in place |
| [debug/](debug/) | Incidents, **indexed by symptom** | One-off, must stay findable |
| [memory/](memory/) | Shared AI memory, one fact per file | Long |
| [handoff.md](handoff.md) | Progress, work in flight, next step | **Expires by design** |

Not present yet, on purpose:

- `architecture.md` — no code exists yet to describe an end-to-end data flow for. Add it
  once the interaction prefab from [decisions/modular-portable-interaction.md](decisions/modular-portable-interaction.md)
  has a first working version.
- `CLAUDE.md` at the repo root — doesn't exist yet. `.ai/decisions/` is currently the
  project's only decision record; if `CLAUDE.md` or another decision doc gets added
  later, don't fragment — add entries there and only use `.ai/decisions/` for what
  doesn't fit.

## Where a new piece of information goes

All conditions must hold.

| You observed | Write it to | Test |
|---|---|---|
| Non-obvious platform/SDK behaviour | `CLAUDE.md` constraints (once it exists) | True in a year **and** not guessable |
| A choice made, alternatives rejected | `.ai/decisions/` | You can write "why not the other one" |
| A fault whose symptom was far from its cause | `.ai/debug/` | Next time it is searched **by symptom** |
| Progress, in-flight work, next step | `.ai/handoff.md` | It will go stale |
| How a person likes to work | **Local memory, not the repo** | About the person, not the project |

**Do not write** code structure, fixed bugs, or anything git history already says. Those
rot immediately and the original source is more accurate.

## Promoting local memory

**Would this still be true for a different person on this project?** Yes promotes.

## One fact per memory file

So two people's edits merge. A single large file guarantees conflicts.

## Fighting rot

Wrong code fails a build; wrong documentation does nothing, so it survives.

1. Write what can be **checked**, not what was concluded
2. Cite `file:line` or a command for anything someone may need to confirm
3. Debug entries record **measurements**, not conclusions
4. Date-stamp documents so staleness is visible
