# DECISIONS — 原创2.5D在线平台射击游戏

## D-001 — Workflow profile

- Date: 2026-08-31
- Decision: Use `BUILD / STANDARD`.
- Rationale: Selected as the smallest adequate persistent workflow for the current project.
- Consequences: Promote only when coordination, risk, or evidence needs justify the added files.

## D-002 — Product format

- Date: 2026-08-31
- Decision: Build a Steam-first online multiplayer game with 2.5D gameplay and 3D presentation; do not ship same-device multiplayer.
- Rationale: This matches the intended online product and avoids shared-keyboard, split-screen, and local-camera constraints.
- Consequences: Online session flow, latency handling, reconnect behavior, and player population become core product requirements.

## D-003 — Originality boundary

- Date: 2026-08-31
- Decision: Retain only genre-level ideas such as platform movement, knockback, ring-outs, and arena pickups; independently create all expression, content, structure, and tuning.
- Rationale: The project should stand on its own creatively and avoid functioning as a reskin or reproduction of the reference game.
- Consequences: No public or final reuse of the Flash game's assets/code; maps, characters, UI, equipment, effects, names, modes, and progression are designed from new source documents. Temporary internal extraction is governed by D-004.

## D-004 — Temporary reference assets

- Date: 2026-08-31
- Decision: Permit offline extraction of the user-authorized local Flash copy solely for isolated internal placeholders and reference; permit web assets only when commercial-use licensing and provenance are documented.
- Rationale: Temporary assets can accelerate graybox and interaction prototyping without defining the final visual identity.
- Consequences: Extracted Flash assets are prohibited from public builds, screenshots, trailers, store pages, public repositories, and release packages; every temporary asset requires a tracked original replacement.

## D-005 — Engine and commercial model

- Date: 2026-08-31
- Decision: Use Unity for development and target a small buy-to-play Steam party game.
- Rationale: Unity supports the required 2.5D/3D workflow and is suitable for a compact PC release.
- Consequences: Prototype baseline is Unity 6.3 LTS with Windows as the first build target. Steam integration is deferred until the offline combat loop is validated.

## D-006 — Prototype 0.1 rules

- Date: 2026-08-31
- Decision: Build one graybox arena with one human player and one bot. Fighters have 100 health and three stock lives. Hits reduce health; reaching zero health does not kill. Lower health increases received knockback. Only leaving the arena costs a life.
- Rationale: This isolates the movement, aiming, hit, knockback, ring-out, respawn, and bot loop before adding content or networking.
- Consequences: Initial knockback multiplier ranges from 1.0x at full health to 3.5x at zero health and is explicitly provisional tuning.

## D-007 — Initial aiming model

- Date: 2026-08-31
- Decision: Prototype 0.1 uses horizontal fire in the facing direction. Free-angle aiming is deferred for a later controlled comparison.
- Rationale: Horizontal fire matches a platform-led arena, avoids prematurely designing wall/projectile interaction around free aim, and reduces the first prototype's variables.
- Consequences: Bot and player use the same horizontal firing constraint. This decision is reversible after movement and map tests.

## D-008 — Installed Unity baseline

- Date: 2026-08-31
- Decision: Use the installed Unity 6000.5.10f1 Update release with URP 17.5.0 for the active prototype instead of installing a second 6000.3 editor.
- Rationale: The project is at the beginning of development, and Unity recommends Update releases for new and mid-cycle projects. Using the installed version avoids duplicate editor storage while keeping an exact patch lock.
- Consequences: `game-client/ProjectSettings/ProjectVersion.txt`, its package manifest, documentation, and verification script are pinned to 6000.5.10f1. Reconsider an LTS lock when production content stabilizes.

## D-009 — Preserve side-view platform readability

- Date: 2026-08-31
- Decision: Keep gameplay on a side-view 2D plane and use 3D assets, depth, lighting, and a mild perspective camera for the 2.5D presentation. Defer the top-down 3D direction.
- Rationale: Platform height differences, ledge recovery, knockback trajectories, and falling out of the arena are the current combat hook and are most immediately readable from the side.
- Consequences: Prototype 0.1.1 changes only presentation, not collision dimensions or combat tuning. Worldbuilding remains undecided, so the visual pass uses original neutral test geometry rather than production art.

## D-010 — Batch future suggestions before implementation

- Date: 2026-08-31
- Decision: New feature and content suggestions are recorded in `updates/CANDIDATES.md` by default and are not implemented immediately. Codex will propose a version only after the candidates form a coherent, bounded, testable batch; implementation begins only after explicit user approval.
- Rationale: Bundling related changes reduces rebuild churn, exposes dependencies before coding, and prevents an accumulation of disconnected features from obscuring core gameplay validation.
- Consequences: Small ideas may remain in the candidate pool across sessions. Critical defects and blockers are handled separately and still require an explanation and appropriate authorization.

## D-011 — License-clear free assets before bespoke production art

- Date: 2026-08-31
- Decision: During gameplay development, prefer existing free assets with documented commercial-use rights; reserve substantial custom modeling and final art unification for after the gameplay contract and worldbuilding are stable.
- Rationale: This directs limited production time toward combat and online systems while retaining a viable path to a coherent commercial release.
- Consequences: CC0/public-domain assets are preferred. Every third-party asset still requires provenance and license tracking, and inconsistent or identity-defining assets may need modification or replacement before release.

## D-012 — First Internet proof is a two-player Host/Client duel

- Date: 2026-09-01
- Decision: The first Internet networking proof will be limited to one player hosting and one remote player joining from another computer; the remote player replaces the AI in the connected match.
- Rationale: Two players are sufficient to validate ownership, input, physics, shooting, knockback, ring-out, lifecycle, latency and disconnect handling before multiplying synchronization and lobby complexity to four players.
- Consequences: The proof is a separate future milestone after offline core rules stabilize. Host migration, dedicated servers, four-player rooms, public matchmaking and AI takeover on disconnect are outside its first scope. Network technology remains undecided and must be evaluated from current official sources before implementation.

## D-013 — Local offline gameplay before networking or Steam

- Date: 2026-09-01
- Decision: Defer Steam integration and the U-007 Internet proof. Current development focuses on making the one-computer offline single-player-versus-AI game enjoyable and mechanically stable.
- Rationale: Movement, platform traversal, knockback, AI pressure, weapon pacing and the match loop are still being validated. Stabilizing them before networking avoids duplicating ownership, physics and synchronization work.
- Consequences: No networking research, package installation, service registration or Steam configuration is active. The eventual Steam/online product direction is retained but has no current schedule. “Local offline” does not reverse D-002's exclusion of same-device multiplayer unless the user explicitly changes that scope later.

## D-014 — Data-driven weapon feel, one gun in Prototype 0.1.2

- Date: 2026-09-01
- Decision: Improve the existing carbine with layered recoil, muzzle, projectile, audio and impact feedback, and store its feel values in a shared weapon profile; do not add more guns to 0.1.2.
- Rationale: The current shooting loop needs immediate cause-and-effect feedback, while multiple weapons would add balance/content variables before the base interaction is validated.
- Consequences: Future guns can set their own cooldown, projectile speed, accumulation damage, knockback, shooter recoil, visual recoil and color through the same interface. The present procedural effects are prototype assets and remain subject to accessibility and subjective tuning.

## D-015 — Staged base-mode gravity; moon gravity remains a separate mode

- Date: 2026-09-01
- Decision: Prototype 0.1.3 uses jump speed 10.8 with staged effective gravity (about 82% while rising, 55% near the apex, and 105% while falling) instead of lowering global gravity. A future low-gravity/moon mode is tracked separately as U-014.
- Rationale: A modest apex extension creates an air-movement and air-shooting decision window without making every fall, hit and projectile in the base game uniformly slow.
- Consequences: The base curve requires hands-on tuning across jumps, one-way platforms, knockback and recovery. A moon mode must later define its own movement, impulse, camera, boundary and match-duration contract rather than multiplying one gravity value.

## D-016 — Immediate match start and per-client soft-follow camera

- Date: 2026-09-01
- Decision: Prototype 0.1.4 starts matches immediately without a countdown. Final stock loss stops combat and shows the winner. The camera remains arena-anchored and follows only the local player by a small clamped amount rather than centering or zooming around both fighters.
- Rationale: The current prototype needs a fast complete loop, while the intended online product will give each client a different locally owned protagonist. Arena anchoring preserves platform height and ring-out readability.
- Consequences: A future network client assigns its owned fighter as the camera target. Remaining offscreen guidance must identify the local player without reintroducing a shared two-character camera.

## D-017 — Three fixed limited-ammo pickup weapons

- Date: 2026-09-01
- Decision: Add one pressure weapon (Pulse SMG), one close-range burst weapon (Scatter Blaster), and one slow area weapon (Rocket Launcher) at fixed, visible pickup points with limited ammo and timed respawn.
- Rationale: Three deliberately different rhythms test whether pickups change platform routes without introducing a large random arsenal or inventory system.
- Consequences: Pickups use the shared weapon profile/projectile path. Values are provisional; future content must not expand weapon count until identity, pickup fairness and ring-out pacing are playtested.

## D-018 — Batched iteration with local-first Codex/Claude collaboration

- Date: 2026-09-02
- Decision: Feature requests and tuning ideas continue to accumulate in `updates/CANDIDATES.md` instead of being implemented immediately. When the candidate set forms a coherent, testable version and the user approves it, Codex and Claude may collaborate on that version using the local project files as the primary working copy. Before and after an iteration, compare the local branch, worktree, version documents, and tag with GitHub `origin/main`.
- Rationale: Batching changes keeps each version understandable and testable. A local-first workflow lets both assistants share the same complete project state, while an explicit Git comparison prevents either a stale cloud copy or uncommitted local work from being silently overwritten.
- Consequences: Never blindly pull, push, merge, or overwrite when local and remote histories differ. First inspect status, commits, tags, and diffs; preserve uncommitted work; then choose the intended direction. Only the tested, documented version should be synchronized to GitHub. At this decision point, local `main`, remote `main`, and peeled tag `v0.1.4` all resolve to commit `63cbbe66ca09d864e8a86517f1506361eef1c407`.

## D-019 — 世界观定为抽象几何"几何战士"

- Date: 2026-09-02
- Decision: 放弃原先三个叙事型备选（太空废品回收队 / 浮空岛魔法快递 / 微缩实验室事故），改为抽象几何身份。玩家角色为基本立体（立方体、球体、四面体、圆柱体），各自配色，以发光"眼"指示朝向。工作名 **几何战士 / Geometry Fighters**，基础击退淘汰模式称 **几何角斗场**。
- Rationale: 项目目标是承载多种规则完全不同的模式（生化感染、旋转平台、逃杀、强力武器）。叙事型世界观要求每个模式在故事中自圆其说，与多模式目标直接冲突；抽象几何没有这个包袱。同时抽象几何在本项目上有四个叠加优势：开发与建模成本最低；与现有全 primitive 的工程完全对齐，占位美术直接升格为正式方向；角色层面不再需要任何第三方素材，消除授权风险；几何图元剪影区分度最高，4人混战中辨认自己最容易。状态（感染、增益、濒死）可用形体崩坏与材质变化直接表达，不依赖UI图标。
- Consequences: `GAME_VISION.md` 的待选方向段落被替换为已选定方向、视觉语言与状态表达约定。美术批次的阻塞项 U-018 解除。已知风险是纯抽象容易显得冷淡，缓解手段是**幽默来自动作而非造型**——依靠夸张的挤压拉伸、翻滚与濒死抖动建立人格，`FighterVisual` 已具备这一层。正式定名前必须在 Steam 与商标层面查重，中英文分别检查。
