# Build and release status: 0.3.0 (2026-10-08)

**Classification: experimental source / NOT READY FOR RELEASE.**

Implemented in source: Steam friends lobby and invitation flow, host-routed allowlisted god powers, Worldfall presence/version detection, explicit host world snapshot request, save/load reflection bridge, 16 KiB framed reliable transfer, SHA-256 receiver validation, duplicate packet handling, bounding (64 MiB), backlog replay after save load begins, CI tests and conditional game DLL job.

Not executed: .NET test projects and real-game DLL build (SDK and WorldBox dependencies not present). No two-user Steam test, snapshot load completion check, authoritative full-world delta replication or Worldfall first-person remote avatars. Repository established at https://github.com/frd21313123123/worldbox; successful Actions runs are not yet confirmed.

Warnings:
- Unity API may change. WorldBox SaveManager signatures based on public research for game 0.51.2; only fake adapter contract tests provided.
- Initial snapshot sync is not continuous or deterministic world simulation synchronization. The transfer must be explicitly requested by the client; it replaces their local map.
- Worldfall multiplayer possession is a separate unimplemented component. The detection/compatibility layer is not a 3D multiplayer engine.
- `docs/CI.md` describes how to enable real DLL compilation using private, legally obtained assembly references.
