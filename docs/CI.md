# Continuous integration and creating the repository

**Hosted CI always runs protocol/Worldfall handshake tests.** A compiled **game DLL** requires private reference assemblies from the user's legitimate installation of WorldBox/BepInEx and a compatible Steamworks.NET DLL. Source control does not contain these assemblies.

## Experimental public-reference build (no WorldBox binaries uploaded)

On every push to `main`, the **public-game-dll** job downloads public Unity 2022.3.60, BepInEx 5.4.23.5 and Steamworks.NET references. It compiles `src/WorldBoxMultiplayer.csproj` targeting `netstandard2.1`, then packages:

- `BepInEx/plugins/WorldBoxMultiplayer/WorldBoxMultiplayer.dll`
- `BepInEx/plugins/WorldBoxMultiplayer/Steamworks.NET.dll`

as a `WorldBoxMultiplayer-experimental-windows.zip` GitHub Actions artifact. On a new `VERSION` value, a successful CI run publishes the ZIP into a GitHub **prerelease**, together with sources and SHA-256 checksums. It never includes or redistributes WorldBox or Worldfall DLLs. Do not treat compilation as validation of multiplayer behavior.

**Known limitations:** Host save transfer is opt-in, but AI/actor/city replication beyond powers remains unimplemented. Worldfall's networked possession/3D avatars are also unimplemented. A plugin that compiles may still crash when loaded by a particular game build. Always back up world saves.

## GitHub repository

Source is maintained at https://github.com/frd21313123123/worldbox. Do not upload proprietary WorldBox or Worldfall assemblies to the public repository.

## Enable real DLL compilation on GitHub-hosted Actions

1. On a Windows computer you own, install WorldBox through Steam, BepInEx 5 and a compatible Steamworks.NET DLL.
2. Create a minimal reference-only archive from the game install:

```powershell
.\scripts\prepare-ci-refs.ps1 -WorldBoxDir 'C:\Program Files (x86)\Steam\steamapps\common\worldbox' -SteamDll 'C:\path\Steamworks.NET.dll' -Output "$env:TEMP\wbmp-private-refs.zip"
```

3. Host the ZIP **privately** in an HTTPS file store you control. Do not post it in the public GitHub repository and do not publish the link. Ensure the license permits your use. The action expects a **direct downloadable** HTTPS URL, optionally a time-limited signed link.
4. Go to GitHub repository **Settings > Secrets and variables > Actions**. Add repository **secrets** `WB_REFS_URL` (private HTTPS archive URL) and `WB_REFS_SHA256` (SHA256 printed by the preparation script). Set **variable** `ENABLE_GAME_BUILD` to `true`.
5. Trigger `WorldBox Multiplayer CI` from the Actions tab, or push to `main`. Unit tests run first. If they pass and secrets are valid, the Windows job compiles the DLL and uploads it as an Actions artifact. If assembly signatures are incompatible, compilation fails. No DLL is published on failure.

Security: secrets are not checked in, and CI publishes only the newly compiled `WorldBoxMultiplayer.dll`. Keep the source archive private and short-lived. The compiled DLL still needs in-game testing on both ends. **Green CI does not prove Steam networking or Worldfall 3D movement is working.**

## Expected layout of ZIP

```text
refs/
  game/
    worldbox_Data/Managed/UnityEngine.CoreModule.dll
    worldbox_Data/Managed/UnityEngine.InputLegacyModule.dll
    worldbox_Data/Managed/UnityEngine.IMGUIModule.dll
    BepInEx/core/BepInEx.dll
  Steamworks.NET.dll
```

ZIP root contains `game/` and `Steamworks.NET.dll` (the action expands it under `refs/`).

## Why CI doesn't download Worldfall

Worldfall is not a build dependency. The integration detects its assembly or install path and exchanges compatible state tags, without referencing its private code. A functional first-person multiplayer synchronization would need a validated public API from Worldfall, additional hooks with permission, or a separate compatibility layer. We cannot establish that by compiling code alone.
