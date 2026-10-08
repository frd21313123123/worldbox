# Required in-game acceptance tests before calling this a release

Environment: two Windows PCs, two distinct Steam accounts that own WorldBox, same installed version of WorldBox, compatible BepInEx 5 and Steamworks.NET, optionally the same Worldfall build. Back up both save directories first.

## 1. Boot and Steam transport

- Both machines start WorldBox through Steam and show `WorldBox Gods 0.3.0 preview ready` in BepInEx logs.
- Host presses Ctrl+Shift+H. A lobby ID and member count appear.
- Host presses Ctrl+Shift+I, invites the second Steam account, second machine joins using the Steam overlay. Alternatively enter Lobby ID and Ctrl+Shift+J.
- Peer count and Worldfall hints reflect actual installed/loaded mods.
- In the lobby the client requests one `meteor` power. Host alone applies it and broadcasts an accepted commit. Client sees event replay, no duplicate calls.
- Deliberately test a rejected invalid power and too-fast repeated actions; neither may mutate the map.

## 2. World save transfer (DESTRUCTIVE to unsaved local map)

- Start from two *different* throwaway saves and back them up.
- Client clicks `Request host world`, then `CONFIRM: Replace my world`.
- Observe total chunk count, SHA-256 integrity and WorldBox's own loading state. Verify map size, cities, actors and terrain AFTER the game finishes loading. The prototype only knows when the asynchronous loader was invoked; it does not verify post-load fidelity.
- Disconnect during a large snapshot, retry after cooldown and verify no map is loaded from partial bytes.
- Confirm invalid/duplicate chunk behavior from the pure unit tests. Validate 64 MiB cap without consuming excessive memory.
- Verify that submitting commands during the save transfer is blocked on the client.
- Validate that a random natural disaster and autonomous war still diverge after a few minutes. This is a known **failure** and a release blocker until authoritative state replication is implemented.

## 3. Worldfall 3D coexistence

- Install Worldfall on both clients according to https://worldfall3d.com/.
- Enter 3D view, pan/zoom and confirm that F6/F7 work as before.
- Re-run host/join/powers. Report crashes, stuck cameras, or broken rendering.
- Confirm that remote first-person avatars are **not present**. This is a known **failure** and release blocker for the requested Worldfall multiplayer feature.
- Test moving in first person only after saving backups; do not attempt world reload while possessing a creature until an approved Worldfall API adapter exists.

## 4. Faults and packaging

- Host disconnects: clients must stop accepting packets and must report host migration unsupported.
- Send incompatible protocol version 2; do not join.
- Create a release DLL via the GitHub Action only if all C# tests and the real game build pass; collect action logs and hash the DLL artifact.
- A green GitHub Action without the above hands-on tests does **not** qualify as a gameplay release.

## Evidence to attach to a release issue

- WorldBox build/version; Unity version; BepInEx version; Steamworks.NET version and the exact Worldfall DLL filename/version.
- Both machines' `BepInEx/LogOutput.log` and Unity `Player.log` (redact IDs/private paths).
- 30-60s screen recording of host/client with matching map and a later recording of simulation drift.
- Actions run URL, SHA256 of built DLL, and screenshots of both players in 3D.
