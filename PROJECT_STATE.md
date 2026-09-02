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

Prototype 0.1.4: complete-match, soft-follow camera, and three-weapon pickup playtest build.

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

- Worldbuilding/art direction is not selected.
- Exact networking/Steam integration is not selected.
- Original project name is not selected.
- Final health-to-knockback curve is not selected; Prototype 0.1 starts at 1.0x–3.5x.

## Current frontier

Playtest Prototype 0.1.4. Complete matches with each pickup and record weapon identity, ammo pacing, pickup contest value, final-elimination clarity, rematch reset reliability, respawn protection, and whether the local-player camera remains platform-centered during strong knockback.

U-009 ledge recovery and the remaining U-011 local-player offscreen indicator are the highest-priority candidates. U-007 networking, U-014 moon mode, art/worldbuilding and additional weapons remain later work.
