# Worldfall 3D: compatibility contract (v0.3.0)

Official installation and control reference: https://worldfall3d.com/

Worldfall builds a 3D representation of the local WorldBox world and supports first-person possession. It can load as a standalone game mod; it is **not** a multiplayer transport. Our BepInEx plugin does not patch Worldfall classes, override its camera, modify its controls, or distribute its DLLs.

Implemented: runtime assembly detection (`Worldfall` or `Worldfall3D`), fallback search for standalone installation files, `HELLO` capability/version hints, warning when one peer lacks Worldfall, and multiplayer hotkeys that do not collide with Worldfall's F6/F7 keys.

Not implemented: 3D avatar spawning of remote players, possession ID synchronization, velocity/rotation interpolation, animation replication, combat hit detection, network prediction, movement reconciliation, local voice/chat, authoritative physics and seamless live world reload without interrupting possession.

Important: a successful snapshot transfer loads the underlying WorldBox save. It may interrupt or invalidate the current Worldfall camera/possession state. Test on a backed-up map and do not sync while controlling a character in first person until this is validated with Worldfall's developers or against its documented API.

Planned integration requirements: an approved/documented hook to resolve a possessed actor's stable ID; API to apply remote look/velocity and spawn non-interactive remote avatars; an authoritative server decision for movement/combat; a validated mapping from game world tiles to Worldfall 3D coordinates. **Never send arbitrary reflection names or executable payloads over the wire.**
