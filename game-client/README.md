# Chaos Arena — Prototype 0.1.4

Unity 6 Update (`6000.5.10f1`) single-player combat sandbox.

## Prototype contract

- One 2.5D arena with perspective depth, layered original background, supports, lighting, and fog.
- One human player and one bot.
- One-way upper platforms; jump through from below and drop through the current platform.
- Player and bot bodies do not physically block each other.
- Easy, Normal, and Hard bot behavior presets; Easy is the default.
- Bot tactics include approaching, holding range, repositioning, and escaping arena edges.
- Three-stock matches stop on final elimination, show a winner, and restart immediately with `R`.
- The camera remains anchored to the arena and follows only the local player by a small clamped amount.
- Multi-part primitive fighters with head, body, limbs, backpack, weapon, and procedural movement animation.
- Horizontal aiming only; facing direction determines shot direction.
- Health starts at 100 and can reach 0 without killing the fighter.
- Lower health increases received knockback.
- Health and knockback numbers stay internal; lives remain visible.
- The carbine has light physical/visual recoil, procedural shot audio, muzzle flash, projectile trail, impact sparks, and hit flash.
- Jump speed and staged air gravity provide a higher jump and a slightly longer apex window for air movement and shooting.
- Weapon feel values are data-driven so future guns can define different recoil without duplicating motor code.
- Fixed arena pickups provide a limited-ammo Pulse SMG, five-pellet Scatter Blaster, and explosive Rocket Launcher; empty weapons return to the base carbine.
- Only leaving the arena costs one stock life.
- This build contains no extracted Flash assets and no third-party art.

## Controls

- Move: `A` / `D` or Left / Right
- Jump: `Space` (two jumps)
- Drop through the current upper platform: `S` or Down
- Fire: `J` or Left Ctrl
- Bot difficulty: `F1` Easy, `F2` Normal, `F3` Hard
- Restart sandbox: `R`

## Open

Open this directory from Unity Hub with Unity `6000.5.10f1`. On the first open,
run `Tools > Chaos Arena > Rebuild Prototype Scene`; Unity creates and opens
`Assets/Scenes/Prototype.unity`. Enter Play mode after the generation finishes.

The same scene can be generated from the command line through
`ChaosArena.Editor.PrototypeSceneBuilder.Build`.

## Build and validation

- Import/compile/scene validation: `Tools/verify-prototype.ps1`
- Windows development build: `Tools/build-prototype.ps1`
- Playable output: `Builds/Prototype01/ChaosArenaPrototype.exe`

The automated player smoke mode uses `-batchmode -nographics -chaosSmokeTest`.
It initializes the arena, player, bot, camera, and physics, then exits after two
seconds with `CHAOS_ARENA_SMOKE_PASS`.
