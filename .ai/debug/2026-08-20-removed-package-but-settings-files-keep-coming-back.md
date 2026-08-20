# Removed a Unity package from manifest.json but its settings files keep reappearing after refresh

**2026-08-20 ・ cost: ~20 minutes ・ resolved by: forcing an actual script/domain reload before cleaning up leftover files**

## Symptom

- `Packages/manifest.json` and `packages-lock.json` were clean — no trace of
  `com.unity.xr.openxr` / `com.unity.xr.interaction.toolkit` / `com.unity.xr.management`
  after a `git checkout` / `git revert`
- Deleted the packages' leftover asset folders (`Assets/XR/`, `Assets/XRI/`) and reverted
  `ProjectSettings/EditorBuildSettings.asset` / `ProjectSettings/ShaderGraphSettings.asset`
  to match git HEAD
- Ran `AssetDatabase.Refresh()` + `UnityEditor.PackageManager.Client.Resolve()` (via
  Unity MCP `Unity_RunCommand`) so the live Editor would pick up the change
- The files came back anyway — `git status` showed `Assets/XR/`, `Assets/XRI/` untracked
  again and the same two settings files modified again, every time, even though the
  manifest itself stayed clean

## Why it was hard

| Ruled out | On what evidence |
|---|---|
| Manifest edit didn't actually take | `git diff Packages/manifest.json` was empty each time — the file on disk was correct |
| Some other process editing the files | No other tool/process was touching the project; only this session's own `AssetDatabase.Refresh()` / `Client.Resolve()` calls preceded each reappearance |

## What was actually true

The removed package's assemblies (`com.unity.xr.openxr`'s `[InitializeOnLoad]` code) were
still loaded in the running Editor process. `Client.Resolve()` and
`AssetDatabase.Refresh()` alone do not force a domain reload/recompile unless Unity
decides one is needed. With the old code still resident, its startup logic kept
re-creating its default settings objects (`Assets/XR/Settings/OpenXR Editor
Settings.asset`, and an `m_configObjects` entry keyed `com.unity.xr.openxr.settings4` in
`ProjectSettings/EditorBuildSettings.asset`) on every refresh, regardless of what the
manifest said.

```
git diff ProjectSettings/EditorBuildSettings.asset
+    com.unity.xr.openxr.settings4: {fileID: 11400000, guid: 6cbf07fa08aaf074bade37dc98086bd7, type: 2}
```

## Two-minute path next time

| Instrument | Reading that means this |
|---|---|
| `mcp__unity-mcp__Unity_ManageEditor` → `GetState` | `IsCompiling` / `IsUpdating` both `false` right after a manifest edit means no domain reload happened — old assemblies are probably still resident |
| `git status` right after deleting leftover files, then again after one more `AssetDatabase.Refresh()` | Same files reappearing with zero manifest diff = stale in-memory package code, not a manifest problem |

## Lesson

After removing a UPM package from a live Editor session, call
`EditorUtility.RequestScriptReload()` (via Unity MCP `Unity_RunCommand`) and wait for
`IsCompiling` / `IsUpdating` to clear *before* cleaning up the package's leftover asset
files — cleaning up first just gets undone by the still-resident old code.
