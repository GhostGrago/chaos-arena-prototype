# EVIDENCE INDEX — 原创2.5D在线平台射击游戏

Use this index only for reusable claims, material tests, benchmarks, incidents, or decisions whose provenance matters.

## Status vocabulary

- BUILD: `IMPLEMENTED / TESTED / BLOCKED / DEFERRED`
- RESEARCH: `CONFIRMED / INFERRED / HYPOTHESIS / UNKNOWN / INVALIDATED`

## Entries

### E-001 — Authorized Flash container triage

- Status: `CONFIRMED`
- Claim/result: The authorized local package contains `Gun Mayhem 2 More Mayhem.swf`, an uncompressed `FWS` SWF v9 file of 10,519,513 bytes with SHA-256 `EA675F3E0DF73671FFE1A376138ED5F4A13691B5C51B11BD6C590B2C1DB53FE9`.
- Source: `work/gunmayhem2-online-feasibility/scope.md` and read-only local header/hash inspection on 2026-08-31.
- Proves: The principal asset container is directly parseable without first decompressing or running it.
- Does not prove: That every embedded asset can be exported losslessly or that any extracted asset is licensed for publication.

### E-002 — Embedded content classes

- Status: `CONFIRMED`
- Claim/result: Tag-level parsing found embedded bitmap tags, 62 sound tags, hundreds of vector shape/sprite tags, fonts/text, and ActionScript 2 `DoAction` blocks.
- Source: Read-only SWF tag inventory on 2026-08-31.
- Proves: The file contains multiple extractable media classes and uses an AS2-era structure.
- Does not prove: Semantic names, visual quality, or final exported file counts before a dedicated SWF exporter is used.

### E-003 — Prototype 0.1 static validation

- Status: `IMPLEMENTED`
- Claim/result: `game-client/` contains a Unity 6000.3.10f1 project skeleton for one human, one bot, one arena, horizontal shooting, nonlethal zero health, escalating knockback, ring-out lives, and respawn. Project JSON parsed successfully, C# delimiter checks passed, and `git diff --check` reported no whitespace errors.
- Source: `game-client/`, `PROTOTYPE_01.md`, and local static validation on 2026-08-31.
- Proves: The intended source/configuration files are present and structurally consistent at a basic static level.
- Does not prove: Unity compilation, package resolution, scene generation, or gameplay feel; the Editor installation still requires user authorization.

### E-004 — Unity environment installation boundary

- Status: `IMPLEMENTED`
- Claim/result: Unity Hub 3.21.0 and Unity Editor 6000.5.10f1 are installed. The prototype configuration was aligned to the Editor's built-in URP 17.5.0 package.
- Source: local package/process/path checks and installer result on 2026-08-31.
- Proves: Hub and the required Editor executable/package baseline are available locally.
- Does not prove: That a Unity license is active or that the prototype compiles.

### E-005 — Unity license gate

- Status: `TESTED`
- Claim/result: Headless project verification initially exited with code 198 and `No valid Unity Editor license found`; after the user activated a license, the same verification path proceeded successfully. The gate is resolved.
- Source: `game-client/Logs/prototype-verification.log`, generated 2026-08-31.
- Proves: Unity licensing is now sufficient for the project's local batch import and build workflow.
- Does not prove: Future eligibility for a particular Unity subscription tier.

### E-006 — Prototype 0.1 build and runtime smoke test

- Status: `TESTED`
- Claim/result: Unity 6000.5.10f1 with URP 17.5.0 imported and compiled the project, generated `Assets/Scenes/Prototype.unity`, built a Windows development player successfully, and ran it headlessly to initialize the arena, player, bot, camera, and PhysX. The player emitted `CHAOS_ARENA_SMOKE_PASS` and exited with code 0, with no matched compiler errors or runtime exceptions.
- Source: `game-client/Logs/prototype-verification.log`, `game-client/Logs/prototype-build.log`, and `game-client/Logs/player-smoke.log`, generated 2026-08-31.
- Proves: The source-to-Windows-player pipeline and initial runtime construction path work on this machine.
- Does not prove: Visual correctness, input feel, AI quality, combat balance, or long-session stability; these require hands-on playtesting.

### E-007 — Prototype 0.1.1 2.5D visual pass build and smoke test

- Status: `TESTED`
- Claim/result: The prototype now constructs a perspective side-view camera, layered original 3D background, dimensional arena supports/trims, two-light scene with fog, and multi-part fighters with visible weapons and procedural limb motion while retaining Z-locked physics. Unity rebuilt the Windows player successfully and the updated runtime emitted `CHAOS_ARENA_SMOKE_READY` and `CHAOS_ARENA_SMOKE_PASS` without matched runtime exceptions.
- Source: `game-client/Assets/ChaosArena/Runtime/PrototypeBootstrap.cs`, `FighterVisual.cs`, `game-client/Logs/prototype-build.log`, and `game-client/Logs/player-smoke-2.5d.log`, generated 2026-08-31.
- Proves: The 2.5D presentation code compiles, is included in the Windows build, and initializes alongside the existing player, bot, camera, and physics path.
- Does not prove: Subjective visual quality, camera framing at every display ratio, silhouette clarity during combat, or improved player enjoyment; hands-on visual review is still required.

### E-008 — URP material repair and rendered-window validation

- Status: `TESTED`
- Claim/result: All procedural primitive renderers now receive materials copied from explicit URP Lit/Unlit assets stored in `Assets/Resources/ChaosArenaMaterials`. Unity compiled both shaders into the Windows build, the player emitted `CHAOS_ARENA_SMOKE_PASS`, the ordinary runtime log contained no matched material/shader exceptions, and direct capture of the real player window showed no magenta error surfaces.
- Source: `game-client/Assets/ChaosArena/Runtime/PrototypeMaterials.cs`, `game-client/Assets/Resources/ChaosArenaMaterials/`, `game-client/Logs/prototype-build.log`, `game-client/Logs/player-smoke-material-fix.log`, ordinary player log, and direct Windows window inspection on 2026-08-31.
- Proves: The demonstrated default-material incompatibility is repaired in the current Windows build and the intended scene colors render on this machine.
- Does not prove: Compatibility with every GPU/driver, subjective art quality, or visual clarity at every aspect ratio.

### E-010 — Prototype 0.1.3 build and runtime validation

- Status: `TESTED`
- Claim/result: Prototype 0.1.3 compiles and builds with tactical bot movement, longer Easy fire bursts, base knockback changed from 2.7/1.15 to 3.25/1.38, larger/longer impact feedback, jump speed 10.8, and staged effective air gravity. The player emitted `CHAOS_ARENA_SMOKE_READY` and `CHAOS_ARENA_SMOKE_PASS`; a separate ordinary 12-second windowed run produced no matched runtime exceptions.
- Source: `game-client/Assets/ChaosArena/Runtime/`, `game-client/Logs/prototype-build.log`, `game-client/Logs/prototype-0.1.3-smoke.log`, and `game-client/Logs/prototype-0.1.3-runtime.log`, generated 2026-09-01.
- Proves: The 0.1.3 code compiles, enters the runtime scene and remains free of detected initialization/runtime exceptions during the bounded checks.
- Does not prove: Subjective AI intelligence, ideal air time, hit readability during dense combat, or balanced ring-out frequency; those require hands-on playtesting.

Each future entry should record: ID, status, claim/result, source path, what it proves, what it does not prove, and date.

### E-011 — Prototype 0.1.4 match, camera and weapon build validation

- Status: `TESTED`
- Claim/result: Prototype 0.1.4 compiles and builds with final-stock elimination, winner/rematch state, small local-player camera follow, three fixed limited-ammo weapon pickups, scatter projectiles, rocket area hits, pickup-aware bot decisions, and weapon/ammo HUD. Dedicated runtime assertions verified exactly three pickups, third-stock elimination, correct winner and rematch restoration to active three-stock fighters with base carbines. The smoke log emitted all pass markers; direct Windows capture showed the 0.1.4 title, three colored pickups, weapon HUD, platform-centered composition and no magenta materials.
- Source: `game-client/Assets/ChaosArena/Runtime/`, `game-client/Logs/prototype-build.log`, `game-client/Logs/prototype-0.1.4-smoke.log`, and direct Windows window inspection on 2026-09-01.
- Proves: The source compiles, the primary lifecycle invariants execute correctly in the bounded runtime assertion, and the initial 0.1.4 scene renders as intended on this machine.
- Does not prove: Weapon balance, long-session pickup reliability, ideal camera comfort, player understanding, or networking readiness; these require hands-on matches and later multiplayer work.

### E-009 — Prototype 0.1.2 combat-feel build and rendered-window check

- Status: `TESTED`
- Claim/result: Prototype 0.1.2 compiles and builds with one-way upper platforms, per-fighter drop-through, ignored fighter-body collisions, hidden health/knockback HUD values, projectile accumulation reduced from 11 to 9, Easy/Normal/Hard bot behavior presets defaulting to Easy, and a data-driven carbine with recoil, muzzle, trail, procedural audio, impact and hit-state feedback. The Windows player emitted `CHAOS_ARENA_SMOKE_READY` and `CHAOS_ARENA_SMOKE_PASS`; direct capture showed the expected 0.1.2 scene, lives-only HUD, Easy label, controls and no magenta surfaces.
- Source: `game-client/Assets/ChaosArena/Runtime/`, `game-client/Logs/prototype-build.log`, `game-client/Logs/prototype-0.1.2-smoke.log`, and direct Windows window inspection on 2026-09-01.
- Proves: The new code is present in a runnable Windows build and its primary runtime construction/rendering path works on this machine.
- Does not prove: That every platform edge case is reliable, the three AI tiers are ideally tuned, the feedback is readable under all combat conditions, or the game feels better to a player; those require hands-on playtesting.
