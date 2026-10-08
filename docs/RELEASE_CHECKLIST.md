# Release gate (do not publish 1.0 until all pass)

- [ ] All CI protocol and snapshot tests green
- [ ] Actual game DLL compiled against supported WorldBox and BepInEx/Steamworks versions
- [ ] Local Steam lobby host/join/invite functionality tested with two unique accounts
- [ ] Same initial world loaded successfully on client without save corruption
- [ ] Continuous authoritative actor/city/terrain changes verified across peers
- [ ] Worldfall 3D possession and multiplayer avatar replication implemented and tested
- [ ] 60-minute stress test with disconnect/reconnect, migration policy, and desync checks
- [ ] Game version guardrails and privacy review completed

Current source is **experimental 0.3.0**, not feature-complete or release-ready.
