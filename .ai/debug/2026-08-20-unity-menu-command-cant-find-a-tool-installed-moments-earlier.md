# Unity Editor menu command says a CLI tool isn't installed, right after installing it

**2026-08-20 ・ cost: ~10 minutes ・ resolved by: starting the process from a shell with a manually patched PATH instead of via the Editor**

## Symptom

- Installed `uv`/`uvx` via `winget install --id astral-sh.uv` this session, confirmed
  working (`uv --version` succeeded) from a **newly opened** shell
- Ran Unity's own `STYLY/STYLY NetSync/Start NetSync Server` menu item (via Unity MCP
  `Unity_ManageMenuItem`) — no visible effect, no server port ever came up listening
- A PowerShell window the menu command had spawned (found later, not immediately
  visible) showed: `uv is not installed on your system. Would you like to install uv
  using winget?` → attempted `winget install --id=astral-sh.uv -e` → failed with "no
  upgrade available" (since it was already installed) → dead-ended at "Press Enter to
  exit"

## Why it was hard

| Ruled out | On what evidence |
|---|---|
| `uv` itself wasn't actually installed | `uv --version` / `uvx --version` succeeded from a fresh shell moments before |
| The menu command wasn't executing at all | `Unity_ManageMenuItem` returned `"executed": true`; it just silently did nothing visible in the Unity console |

## What was actually true

The Unity Editor process had been launched **before** `uv` was installed, so it inherited
the old PATH and kept it for its whole lifetime — Windows processes don't pick up PATH
environment variable changes made after they start. The Editor's own subprocess (spawned
by the menu command) inherited that same stale PATH, couldn't find `uv`, and its
self-repair logic tried (and failed) to reinstall via winget instead of failing cleanly.

```
Get-Process | Where-Object { $_.ProcessName -match "python|uv" }
(nothing — the menu command's subprocess never got past the PATH check)
```

## Two-minute path next time

| Instrument | Reading that means this |
|---|---|
| `mcp__unity-mcp__Unity_ManageEditor` → `GetState` → `TimeSinceStartup` | A large number (thousands of seconds) means the Editor process predates any tool installed this session — assume its PATH is stale |
| Start the tool manually from a shell with the install path appended to `$env:PATH` for that call, instead of relying on an Editor-spawned subprocess | Sidesteps the stale-PATH problem entirely |

## Lesson

A CLI tool installed mid-session is invisible to any process that was already running
(Unity Editor, an already-open shell) until that process restarts. Don't trust an
Editor-spawned subprocess to see a tool installed in the same session — either restart
the Editor first, or run the tool from a shell you control the PATH for.
