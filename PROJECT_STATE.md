# PROJECT STATE — 原创2.5D在线平台射击游戏

Updated: 2026-09-01  
Project root: `C:\Users\17141\OneDrive\Document\ChatGPT\Game`  
Workflow: `BUILD / STANDARD`

## Single source of truth

This file is the authoritative persisted current state. Historical notes do not override it.

Normal startup requires only this file and `NEXT_TASKS.md`. Historical evidence and version plans are stored under `docs/archive/`.

## Objective

设计并制作一款面向Steam的2至4人在线2.5D平台射击派对游戏，继承击退与场外淘汰的类型乐趣，同时在视觉、世界观、角色、地图、武器、规则和交互上形成独立原创身份。

## Current phase

Prototype 0.2.5: online host/join verified, random weapon drops, CC0 weapon and city art, dusk skyline.

## Current status

- Project workflow initialized.
- Initial game vision, feature direction, and vertical-slice boundary are drafted in `GAME_VISION.md`.
- The visual/worldbuilding direction is awaiting user selection.
- Static triage identified one uncompressed SWF v9 containing embedded bitmap, vector, text, font, sound, sprite, and ActionScript 2 tags; extraction has not started.
- Unity Hub 3.21.0 and Unity Editor 6000.5.10f1 are installed. The project is pinned to this Update release and URP 17.5.0.
- The separate `game-client/` Unity project imports and compiles successfully, generates `Prototype.unity`, builds a Windows player, and passes an automated runtime initialization smoke test.
- Prototype 0.1.1 retains 2D-plane gameplay while adding a perspective camera, layered 3D background, platform depth/supports, lighting/fog, and articulated primitive-built fighters with simple procedural movement.
- The reported magenta/black URP material defect is repaired. Runtime primitives now receive explicitly included URP Lit/Unlit material assets; the rebuilt Windows player passed headless smoke and direct window capture showed the intended colored background, arena, fighters, lights, and projectile without magenta error surfaces.
- Prototype 0.1.2 implements U-003 one-way platforms, U-004 non-colliding player bodies, U-005 hidden HP/knockback values and projectile accumulation 11→9, U-006 Easy/Normal/Hard bot presets defaulting to Easy, and U-012 layered shooting feedback with data-driven recoil. Windows build, headless runtime smoke, and direct window rendering passed; subjective movement/AI/weapon tuning remains.
- Prototype 0.1.3 addresses the first 0.1.2 feedback: AI now selects approach/hold/reposition/edge-escape tactics, Easy uses useful multi-shot bursts, base hit impulse is about 20% stronger, hit flash/sparks/stretch are more visible, jump speed is 10.8, and staged gravity extends ascent/apex control while retaining a firm fall. Windows build, headless smoke, and a 12-second ordinary runtime check passed; subjective tuning remains.
- Prototype 0.1.4 removes the sandbox's automatic stock refill: final stock loss stops combat, displays a winner, and supports immediate `R` rematch without a start countdown. It adds an arena-anchored camera that follows only the local player by a small clamped amount, plus fixed pickups for a 32-round Pulse SMG, 8-shot five-pellet Scatter Blaster, and 5-rocket explosive launcher. Build, dedicated match/reset assertions, smoke, and direct window rendering passed; balance remains subjective.
- Claude continuation materials are current: root `CLAUDE.md` provides automatic startup guidance and `docs/2026-09-01_工程交接-Claude-report.md` contains the full reproducible engineering handoff.
- Prototype 0.1.5 fixes the reported "sparks but no bullet" defect: combat VFX kept a live collider for the rest of the frame, so each projectile triggered against its own muzzle flash and destroyed itself at the barrel. Colliders are now disabled before being destroyed, projectiles ignore VFX, and a smoke assertion guards the regression. It also adds auto-rematch 2.5s after the final elimination, a bright rotating respawn-protection ring that only blinks as it expires, and a camera that balances the local fighter against the arena centre while pulling back automatically near ring-out edges. Build and smoke passed; feel is unverified.
- Prototype 0.1.6 extracts ArenaBuilder from PrototypeBootstrap and moves the level to a data-driven layout of six asymmetric one-way platforms. Bot count is adjustable from 1 to 3 with last-fighter-standing rules, bots retarget the nearest living rival instead of always the player, and the HUD covers up to four fighters. Adds CombatFeel hitstop and camera shake, plus a procedural visual pass: per-layer metallic/smoothness, a rim light, background parallax and bright platform edges. No external assets were introduced. Build and smoke passed; the new layout invalidates earlier balance conclusions and needs fresh playtesting.
- Prototype 0.1.7 lands the Geometry Fighters identity from D-019: fighters become distinct primitive solids (cube, sphere, generated tetrahedron, cylinder) with a glowing eye for facing, and FighterVisual moves from limb swing to squash, tumble and wobble so personality comes from motion. The physics capsule is unchanged. It adds U-009 ledge recovery with a time-limited hang and re-grab cooldown, U-011 offscreen guidance for the local player, and tightens ring-out bounds to |x|>13 / y<-6 while letting projectiles pass up through one-way platforms. Build and smoke passed; the ledge grab window still needs hands-on confirmation.
- Prototype 0.1.8 withdraws the 0.1.7 ledge grab: BotController had no hang awareness, so bots cycled grab/timeout/fall on platform edges and appeared stuck. The implementation stays available at tag v0.1.7. It adds a bloom/vignette/colour-grading volume with HDR and post-processing enabled on the camera, an AssignNeon material path that drives unlit colours above 1.0 so bloom catches them, per-shape glowing edge frames over the matte solids, neon platform lips and under-glow, and a WeaponVisual that rebuilds a distinct held model per weapon. Also fixes ChaosArena.Runtime.asmdef, which referenced no assemblies and broke the URP using directives. Build and smoke passed; bloom strength and readability are unverified.
- Prototype 0.1.9 answers the plagiarism question recorded with D-019: risk lives in the silhouette, not the material, so jelly humanoids would read as Fall Guys while jelly-surfaced geometric solids do not. Fighter bodies switch to a translucent glossy surface with a spring-damper jiggle, which is what actually sells softness. Bloom threshold rises to 1.15 and intensity drops to 0.5 with every emitter roughly halved, answering the light-pollution feedback. CombatVfx.JellyBurst sprays tumbling body-coloured blobs on heavy hits, stock loss and elimination, and bullet impacts splatter in the victim's colour. Build and smoke passed; translucency rendering and burst density are unverified locally.
- Prototype 0.1.10 fixes two 0.1.9 defects the user reported. The jelly translucency never applied: switching an opaque URP Lit material to transparent blending at runtime failed silently, which PITFALLS already warned against. Transparency now lives in a PrototypeJelly.mat asset alongside the existing Lit and Unlit resources, and a new assertion checks the body material's render queue and _SURFACE_TYPE_TRANSPARENT keyword so the failure cannot recur unnoticed. The edge frame also read as white because a 0.5 lerp toward white was multiplied by a 1.6 neon intensity; it now keeps the fighter's own colour.
- Prototype 0.1.11 is the first build the user has hands-on verified: the ESC menu opens and quits correctly, pressing ESC during a hitstop no longer lets the game unfreeze behind the menu, weakened respawn protection gives visible feedback, and knockback now visibly moves a grounded advancing bot. It also fixes a long-standing defect where knockback appeared to do nothing. FighterMotor overwrites horizontal velocity every physics step, so ground acceleration of 35/s cancelled a 3.25 carbine impulse in about 0.09s; the effect was worst against a grounded, actively moving bot, which is exactly what the player fights. A control lock now suspends movement for 0.15-0.45s scaled by impulse, preserving momentum under light drag, with an assertion covering the mechanism. The fighter edge frame added in 0.1.8 read as a cage around the jelly body and is removed along with its now-dead builder code; platform neon edges stay because they serve ring-out readability.
- Prototype 0.2.0 adds online host and join. Relay was chosen over direct IP because the host sits behind NAT and GAME_VISION rules out port forwarding as the default experience. Because the arena and fighters are built at runtime and cannot be network prefabs, replication runs through a single scene NetMatch object: clients push input up and the host broadcasts all four fighters' state at 25Hz. The host owns physics, AI and match rules; clients are pure presentation with kinematic bodies. A main menu now offers solo, host and join. Offline play is unchanged and all previous assertions still pass. ONLINE IS COMPLETELY UNTESTED and cannot run until cloudProjectId is set by linking a Unity Cloud project and enabling Relay.
- Prototype 0.2.5 is verified online by hand across two instances: host/join over Relay, position and animation sync, replicated shots, winner screen and disconnect-to-menu all work. Rooms open empty so a joining player is not padded out with bots, difficulty and bot count are host-only in the pause menu, and the room code has copy/paste. The rocket became a sniper and was then toned down by cutting damage rather than knockback, since internal damage drives the knockback multiplier. Fixed pickup points became random timed drops that are consumed rather than respawning in place. Art now uses three CC0 packs (TP-001..003): Kenney weapon models, ambientCG platform plates, and Kenney city buildings for a calm dusk skyline after a first neon attempt read as glare. Client prediction, a lobby and the mode framework remain unbuilt.
- The collaboration workflow is local-first and version-batched: suggestions are collected before an approved iteration, Codex and Claude share the local project state, and GitHub is compared before and after synchronization. On 2026-09-02, clean local `main`, remote `origin/main`, and peeled tag `v0.1.4` matched commit `63cbbe66ca09d864e8a86517f1506361eef1c407`.

## Established results

- Target platform: Steam.
- Shipping format: online multiplayer; no same-device multiplayer feature.
- Presentation: 2.5D movement and gameplay with 3D characters/environments.
- Target party size: 2–4 players for the first release scope.
- Engine: Unity 6000.5.10f1 Update, URP 17.5.0, Windows-first.
- Commercial model: small buy-to-play Steam party game.
- Prototype aiming: horizontal facing-direction fire.
- Health reaching zero does not kill; lower health increases received knockback; only ring-out costs a stock life.

## Unknown

- Worldbuilding/art direction is selected: abstract geometric "Geometry Fighters" (D-019). Character forms and per-mode visual language are not yet implemented.
- Networking is now Netcode for GameObjects over Unity Relay (0.2.0). Steam integration is still not selected.
- Original project name is not selected.
- Final health-to-knockback curve is not selected; Prototype 0.1 starts at 1.0x–3.5x.

## Current frontier

Playtest Prototype 0.1.4. Complete matches with each pickup and record weapon identity, ammo pacing, pickup contest value, final-elimination clarity, rematch reset reliability, respawn protection, and whether the local-player camera remains platform-centered during strong knockback.

U-009 ledge recovery and the remaining U-011 local-player offscreen indicator are the highest-priority candidates. U-007 networking, U-014 moon mode, art/worldbuilding and additional weapons remain later work.
