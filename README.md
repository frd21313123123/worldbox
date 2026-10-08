# WorldBox Gods Multiplayer

Experimental Steam co-op mod for WorldBox, with optional Worldfall 3D coexistence support.

**Status: experimental, not a playable final release.** Network lobby, action relay and host save transfer code exist in the v0.3.0 source archive; a real game DLL requires reference assemblies from a lawful WorldBox installation, and live Steam / Worldfall testing remains outstanding.

## Source package

The complete original v0.3.0 source package is available in the related ChatGPT conversation. The GitHub repository is being initialized for source control and CI. No WorldBox or Worldfall proprietary DLLs belong in version control.

## Build prerequisites

- .NET SDK 8+
- WorldBox (Steam), BepInEx 5, compatible Steamworks.NET
- Legally obtained game assembly references supplied locally or privately to CI

Do not mistake a passing unit test for validated multiplayer. Worldfall's remote 3D avatar motion and authoritative creature/city synchronization are not yet implemented.

## Release policy

A stable tag is blocked until DLL compilation succeeds and two Steam clients pass lobby, world save transfer, reconnect, powers, and Worldfall smoke tests.
