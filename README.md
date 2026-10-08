# WorldBox Gods Multiplayer, v0.3.0 (experimental)

A source-available prototype of shared god powers over **Steam friends lobbies** for **WorldBox**. It has a **Worldfall 3D coexistence check**, explicit transfer of the host's saved world and automatic tests. It is **NOT a finished multiplayer mod** and is **not a tested game DLL**.

## What is actually implemented

- Create a Steam friends-only lobby, join by lobby ID or invitation, invite friends via the overlay.
- Send a restricted set of god powers to the authoritative host, rate-limit commands and broadcast accepted commands to participants. Relay transport: Steam Networking Messages (reliable).
- Detect whether Worldfall assemblies are loaded (or present on disk), exchange version hints with peers, preserve Worldfall's F6/F7 hotkeys. Worldfall and this project are independent DLLs.
- **Opt-in world-save transfer**: client confirms replacement of its **unsaved** local world, host calls `SaveManager.saveWorldToDirectory`, transfers `map.wbox` in bounded 16 KiB frames, receiver verifies SHA-256 and invokes `SaveManager.loadMapFromBytes`. Transfer is limited to 64 MiB. Load is asynchronous, and its completion must be verified in the game.
- Commit sequence / retransmission-safety checks, member-only sessions, explicit rejection of incompatible wire protocol versions and unexpected senders.
- Continuous integration that runs protocol, chunk/integrity and fake WorldBox API contract tests. Real DLL compilation is **gated on locally provided legal reference DLLs**.

## What's still missing for a real, final release

- **Authoritative simulation replication.** Capturing and loading a save once does not keep independently running AI, actors, cities, projectiles, RNG and economy in lockstep. Even after an initial transfer, worlds can diverge. Correct replication requires a full runtime adapter, delta streams, or a server-authoritative simulation/view renderer.
- **Worldfall player-avatar replication.** The 3D camera/world on each client may work locally, but player possession, player movement and combat are NOT networked by this project. The public website does not document a supported networking API. No Worldfall binary, private code or assets are included.
- **Live game compatibility proof and packaged DLL.** This environment has no WorldBox install, .NET SDK or Steam accounts for a two-computer test. APIs were inferred from public WorldBox 0.51.2 research and may change.
- World snapshot capture is performed on the game's main thread and may cause stutter for large maps. Current 64 MiB cap, retry/timeout limits and handshake validation are safety measures, not performance certification.
- Host migration, drop-in join without map replacement, seamless resync during 3D possession and save completion confirmation are not implemented.

## Installation and build (Windows)

1. Install WorldBox from Steam and compatible [BepInEx 5](https://github.com/BepInEx/BepInEx) (the code is a **BepInEx plugin**, not a NeoModLoader plugin).
2. Install .NET SDK 8 or newer and use the included script to compile against your *own installation's* Unity/BepInEx and a compatible Steamworks.NET assembly.
3. Build/install with:

```powershell
.\scripts\build-and-install.ps1 -WorldBoxDir 'C:\Program Files (x86)\Steam\steamapps\common\worldbox' -SteamDll 'C:\path\Steamworks.NET.dll' -Install
```

4. Launch WorldBox using Steam. Check `BepInEx/LogOutput.log` for `WorldBox Gods 0.3.0 preview ready`.
5. Install [Worldfall 3D](https://worldfall3d.com/) separately if desired, following its own instructions. Never copy Worldfall.dll from this repository: it isn't included.

The installer stops if the test suite fails or the DLL cannot be compiled. Do **not** install this alongside another copy of the same multiplayer DLL. A compiled game DLL alone may not be enough if its Steamworks.NET dependency is missing; see the build script.

## Controls

| Controls | Action |
|---|---|
| Ctrl+Shift+M | Toggle multiplayer HUD (if Unity OnGUI is alive) |
| Ctrl+Shift+H | Host a Steam friends-only lobby |
| Ctrl+Shift+J | Join by numeric lobby ID from BepInEx configuration |
| Ctrl+Shift+I | Steam invite overlay |
| Ctrl+Shift+L | Leave lobby |
| Ctrl+Shift+P | Request listed god power at configured world coordinates |
| UI: Request host world, then CONFIRM | **Replace local unsaved map** with host snapshot |

World coordinates and power ID can also be entered in the HUD. Only a strict allowlist is accepted: `human`, `elf`, `orc`, `dwarf`, `sheep`, `wolf`, `dragon`, `meteor`, `lightning`, `rain`, `fire`, `tornado`, `bomb`, `plague`, `volcano`. Some may be rejected if the game changes asset names or delegates.

## Architecture

```text
WorldBox + optional Worldfall 3D  (host)
              |
         BepInEx plugin
              |
     Steam friends lobby (8 max)
              |
  SteamNetworkingMessages channel 0 (reliable)
       /                      \
  ACT/COMMIT              SYNCREQ/BEGIN/chunks
       |                      |
 Reflection WorldPowers   SaveManager reflection
       |                      |
   Client replay       Optional client save load
```

Worldfall's 3D rendering is not changed: this plugin only exchanges Worldfall presence/version hints and works with underlying WorldBox state.

## GitHub Releases (automatic experimental previews)

Preview builds are published at [GitHub Releases](https://github.com/frd21313123123/worldbox/releases) **after** the main branch passes the full CI workflow. To publish another preview, update `VERSION` to a fresh `vX.Y.Z-preview.N` tag and push to `main`. The release publisher creates the corresponding tag at the **tested commit**, uploads a source ZIP and SHA-256 checksums, and includes `WorldBoxMultiplayer.dll` **only if** CI built it with legal private game references.

No DLL is fabricated when reference secrets are missing. Such previews are explicitly marked **source-only** and **prerelease**, not stable or installable. Refer to [docs/RELEASE_CHECKLIST.md](docs/RELEASE_CHECKLIST.md) before any stable game release. For configuration, see [docs/CI.md](docs/CI.md).

## Test and CI

```powershell
dotnet run --project tests/WireTests.csproj -c Release
dotnet run --project tests/SnapshotTests.csproj -c Release
dotnet run --project tests/GameAdapterContractTests.csproj -c Release
```

- `.github/workflows/ci.yml`: runs these tests on push, pull request and manually, uploads source as an artifact even on failure.
- A separate Windows job builds `WorldBoxMultiplayer.dll` **only when** the variable `ENABLE_GAME_BUILD=true` and private reference archive secrets are configured. See [docs/CI.md](docs/CI.md).
- The workflow does not assume public permission to redistribute WorldBox or Worldfall DLLs. It will not fabricate a real game build from stubbed test assemblies.
- Project repository: https://github.com/frd21313123123/worldbox . The `scripts/publish-to-github.ps1` helper is only for creating a separate new repository.

## Safety and limitations

`SYNCREQ` can only be sent as a member of the same lobby; only the lobby owner can send a snapshot to a requesting client. The receiver verifies an exact byte count and SHA-256 before asking WorldBox to load it. World saves are still **untrusted data** from the host, not secure merely because they were hashed. Join only trusted Steam friends. The project never executes remote code/console commands.

### Release gate

Do not label a build as stable until it passes: (1) CI C# tests; (2) game DLL compilation against supported version; (3) two accounts joining via Steam; (4) initial map transfer and verification; (5) stress testing world simulation, disconnect/reconnect and Worldfall 3D. **The last two are currently unimplemented as final-grade multiplayer systems.**
