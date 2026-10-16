<!-- VBWR B -->
<!-- Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto -->
<!-- Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT -->
<!-- VBWR E -->

# Vibe Panel Codex

I use this optional local plugin to show Codex activity on my VibePanel screen. Its Python hook reads Codex lifecycle metadata and writes a small local state file. It sends nothing over the network, does not read transcripts, and never saves prompts, assistant replies, commands, tool arguments, or tool results.

The hook handles all 12 documented events: `SessionStart`, `SessionEnd`, `UserPromptSubmit`, `PreToolUse`, `PermissionRequest`, `PostToolUse`, `PreCompact`, `PostCompact`, `SubagentStart`, `SubagentStop`, `Stop`, and `Interrupt`. Each invocation returns `{}` so this display integration cannot approve, block, or steer Codex. Python 3.10 or newer must be available as `python3` on Linux/macOS or through `py -3` on Windows.

## Prepare locally

From this folder, run `py -3 -B install.py prepare` on Windows or `python3 -B install.py prepare` on Linux/macOS. This stages a copy under `~/.codex/plugins/vibepanel-codex` and adds an available entry to the personal `~/.agents/plugins/marketplace.json`, preserving other entries and backing up an existing marketplace once. It does **not** install or trust the plugin inside Codex. Restart the desktop app, choose the local plugin in the Plugins Directory, and review and trust the hook definition there. `status` only checks local files; `unlist` removes the marketplace entry but preserves copied plugin files. If the plugin is enabled, disable it inside Codex too. The VibePanel UI and `vp` CLI can call the same explicit `prepare` operation once their package controls are connected.

Plugins containing lifecycle hooks are currently for manual local Codex desktop installation; they are not eligible for the public plugin directory. See the [Codex hooks reference](https://learn.chatgpt.com/docs/hooks) and [local plugin packaging guide](https://developers.openai.com/plugins/build/plugins).

## State for the display

The hook writes `current.json` and up to 64 `sessions/<sessionKey>.json` files under `%LOCALAPPDATA%\VibePanel\codex` on Windows, `~/Library/Application Support/VibePanel/codex` on macOS, or `${XDG_STATE_HOME:-~/.local/state}/VibePanel/codex` on Linux. Writes are atomic. An event is at most a few hundred bytes; hook input over 1 MiB is ignored. The UI should treat an activity timestamp older than five minutes as stale, even when `isLive` was true at the last event.

```json
{"version":1,"kind":"codex.activity","sessionKey":"opaque hash","turnKey":"opaque hash or null","event":"PermissionRequest","status":"Approval requested on PC","phase":"waiting","isLive":true,"atUtc":"2026-10-16T09:30:00.000Z","atUnixNs":1792143000000000000,"toolKind":"terminal","chatName":null}
```

`sessionKey` and `turnKey` are truncated SHA-256 hashes of the Codex identifiers. `toolKind` is a broad category such as `terminal`, `files`, or `connected-app`. Codex hooks do not provide a stable chat title; `chatName` remains null until the user sets an alias in VibePanel. `current.json` represents the most recent observed hook event, which may belong to a background chat rather than the foreground Codex window.
