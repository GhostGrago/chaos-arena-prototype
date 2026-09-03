# ACTIVE UPDATE CANDIDATES

Updated: 2026-09-03

Active sections contain only unfinished work. A short completed-identifier index prevents obsolete IDs from being reopened; full history is preserved in `VERSION_*.md`, `PROJECT_STATE.md`, Git tags, and `../docs/archive/updates/CANDIDATES_FULL_2026-09-01.md`.

No new gameplay batch is currently approved.

## 0.4.0 试玩反馈（2026-09-03，用户提出，下一批处理）

版本节奏：小修与小维护合并进当前版本日志，只有成组的大改动才推进版本号，由当前协作助手判断。
0.4.0 刚推进过一次，这三项属于「小维护 + 一项设计方向」，默认并入 0.4.0 日志而不是再推 0.5.0；
除非地图差异化最终改动量很大，再单独评估。

### U-036 — 狙击枪朝向仍然反向（缺陷，最高优先）

**现象**：用户在 0.4.0 中报告狙击枪枪口仍朝向自己、枪尾朝外。

**处理历史（两次都改错了，必须停止猜测）**：
- 初始 `MuzzleSign = -1`，用户报告反向。
- 0.4.0 改为 `+1`，用户报告**仍然**反向。
- 符号翻转等于绕 Y 轴旋转 180°，因此两种状态不可能都是反的。其中一次报告必然指向了别的东西。

**本次实测证据（`blaster-e.obj` 分组测量）**：

| 分组 | Z 范围 | 说明 |
|---|---|---|
| `magazine` | +0.670 .. +0.820 | 弹匣位于机匣下方，即握把一侧 |
| `scope` | +0.485 .. +0.975 | 瞄准镜位于机匣上方 |
| 整体 | +0.000 .. +1.390 | |

机匣区间约 0.485–0.975。向 -Z 延伸 0.485 单位且截面极细（X 0.06–0.09，Y 0.06–0.087）＝**枪管**；
向 +Z 延伸的部分粗壮（Y 0.358），末端 1.251–1.390 是分离的小块＝**枪托底板**。
因此 **`blaster-e` 枪口在 -Z**。

**规则由两个参照物独立确认**：
- `blaster-b`（手枪，用户明确确认正确）：握把在 +Z（该侧 Ymin -0.197 vs -0.117），枪口在 -Z，取值 `-1`。
- `blaster-a`（霰弹）：`magazine` 位于 -0.220..-0.070，即握把在 -Z，枪口在 +Z，当前取值 `+1`。

⇒ 规则成立：**枪口在 -Z 取 `-1`，在 +Z 取 `+1`**。据此狙击枪应为 **`-1`**。

**推测的矛盾来源**：`WeaponPickup.Update()` 让地面掉落**持续自转**
（`transform.Rotate(0f, 70f * Time.deltaTime, 0f)`），所以地面上的武器没有固定朝向，
任何一把枪在任意时刻都可能看着像反的。用户上一轮正好在检查放大后的地面掉落。

**下一批动作**：
1. 狙击枪改回 `-1`；
2. **先用截图确认实际渲染结果再宣布修好**，不要第三次盲翻符号；
3. 评估是否让地面掉落停止自转、或只在一个固定朝向上小幅摆动，以免朝向无法判断。

### U-037 — 地图差异化不足（设计方向，用户明确提出）

**现象**：四张地图只有配色与背景不同，平台布局大同小异，玩法体验没有区分度。

**用户给的方向**：
- 底部不必永远是一整块完整平台；
- 例如太空图可以**没有底板**，只留两侧平台，掉下去直接死；
- 底板可以**断开**成几段，中间是空的；
- 让平台设计本身更有趣、可玩性更多。

**实施时必须一并处理的约束**（当前代码假设了底板存在）：
- `AssertEveryArenaBuilds` 要求 `Layout[0]` 必须是实心主甲板 —— 需改为「至少存在一块可落脚的实心平台」；
- `SpawnPointFor()` 用 `Layout[0]` 计算出生点 —— 需改为按地图声明出生点，否则无底板地图会把人生成在空中；
- `PickupDirector.PickDropPoint()` 从任意平台随机取点，本身没问题；
- `BotController.RefuseStepIntoTheVoid` 的探地逻辑正好能让 AI 适应断开的地板，但 `DeckEdge = 10.2` 与
  `RecoveryFloor = -3.5` 是硬编码的，无底板地图需要按地图给值；
- `ArenaBuilder.BuildArenaDetails()` 里的支柱与底舱是霓虹城专属硬编码装饰，应移进主题数据。

考虑到需要改动出生点、AI 边界与断言，这一项如果做全，改动量可能够格单独推一个版本。

### U-038 — 护盾改为半透明气泡

**现状**：`ProtectionShield` 是 18 段方块拼成的旋转虚线圆环。

**用户要求**：改成**更大的、浅蓝色半透明气泡**包住角色，而不是虚线圈。

**实施要点**：
- 用球体 + `PrototypeJelly.mat`（透明状态必须来自材质资源，不能在运行时切混合模式，这是 0.1.9 的既有坑）；
- 半径需大于角色本体，让角色能被看见「包在里面」；
- 保留现有行为：**只在即将消失时闪烁，且无音效**；
- 该护盾现在同时服务于出生保护与 SHIELD 道具（`Fighter.IsDamped`），两者应有区分度
  （例如道具气泡略大或颜色略不同），否则玩家分不清是保护期还是吃到了道具。

## Validation before another feature batch

### V-001 — Prototype 0.2.5 balance and readability

- Test random-drop pacing at 5.5–9.5 seconds with the three-slot cap.
- Re-test the sniper after its damage reduction and Scatter at point-blank range.
- Confirm the dusk skyline stays calm and fighters, projectiles and platform edges remain readable.
- Record camera comfort, bot behavior and the client's visible round-trip input latency.

### V-002 — Four-player online validation

Relay play is hands-on verified with two instances only. A full four-player room still needs seat, movement, shooting, drop, elimination, rematch and disconnect validation.

## Maintenance candidates

These are technical hardening tasks, not new gameplay:

### M-001 — Deterministic generated scene

`PrototypeSceneBuilder.BuildWindows` currently regenerates scene object file IDs, leaving a semantic no-op diff in `Assets/Scenes/Prototype.unity`. Make repeated builds stable before relying on a clean-worktree version check.

### M-002 — Connection-establishment regression test

`CheckConnectionLost` correctly waits until a client has connected once before treating a later disconnect as connection loss. Add an automated state-transition test; current coverage is manual two-instance verification rather than a dedicated assertion.

### M-003 — Stronger knockback regression test

The current assertion proves that the control lock engages, but does not simulate a physics step or verify that launch velocity survives movement processing. Extend coverage without changing established feel values.

## Deferred gameplay and design candidates

The entries below require explicit approval before implementation.

### U-009 — Ledge recovery

The 0.1.7 implementation was withdrawn in 0.1.8 because bots repeatedly grabbed, timed out and re-grabbed edges. Any return must include AI hang awareness, escape/release decisions and anti-loop coverage.

### U-014 — Low-gravity/moon mode

Create a separate ruleset rather than altering base-mode gravity. Depends on an approved mode batch and preferably U-028.

### U-015 — Scatter point-blank balance

All pellets can connect at contact range. Re-test on the current arena before choosing fewer pellets, lower per-pellet damage/knockback or close-range falloff.

### U-020 / U-025 — Moving and rotating platforms

Requires rewriting the one-way-platform height/carry logic before platforms can move reliably.

### U-022 — Production-quality combat audio and feedback

Current hitstop, camera shake and procedural audio remain prototype-level. Any presentation change still requires hands-on confirmation.

### U-024 — Infection mode

Requires contact rules, team conversion, scoring, mode-specific state visuals and U-028.

### U-026 — Battle-royale mode

Requires a larger arena, shrinking-play-area rules, more content and higher player-count validation; depends on U-028.

### U-027 — Power-weapon mode

A lower-cost rules variant, but still requires U-028 and explicit approval.

### U-028 — Mode framework

Extract pluggable win conditions, scoring, spawn rules, HUD and per-mode camera behavior before implementing a second mode. Do not begin without approval.

### U-029 — Online lobby/waiting room

Show joined players, mode and bot settings, readiness and host-controlled start. Do not begin without approval.

### U-030 — Client prediction

Prediction requires input buffering, replay, reconciliation and smoothing. Treat it as an isolated high-risk networking batch; do not begin without approval.

## Completed identifiers

- U-035 local three-player mixed input: integrated into 0.3.0 with P1 keyboard, P2/P3 as separate Input System gamepads, no AI, three-target camera, device names/disconnect warning and three-seat assertions. Live Windows detection confirmed Xbox Controller + DualSense Wireless Controller; physical button gameplay remains user hands-on.
- U-034 local two-player milestone: implemented in 0.3.0 with two human seats/no AI, P1 keyboard, P2 controller/keyboard fallback, shared camera and separate offscreen indicators. The same milestone now includes LT/RT firing, physical weapon-specific shooter recoil and display settings up to 4K/Borderless. P2 movement and A jump passed hands-on; the integrated refinements remain pending hands-on confirmation.
- U-033 sniper identity tuning: implemented in 0.2.6 with knockback `(6.4,2.2)` → `(7.0,2.4)` and cooldown `1.5s` → `1.7s`; damage remains10 and ammo remains3. Hands-on feel pending.
- U-032 arena-space tuning: implemented in 0.2.6; every combat platform is 10% wider on X and ring-out bounds are extended to `|x|>19 / y<-9.5` so air control and double jump can recover. Hands-on feel pending.
- U-031 transparent cloud/fog approach: invalidated after hands-on review because jelly transparency made the shapes look reflective. Keep the 0.2.5 background simple; do not restore this approach.
- U-007 Host/Join proof: completed and verified with two instances; four-player testing is V-002.
- U-011 local-player offscreen guidance: completed in 0.1.7.
- U-016 rocket-specific issue: obsolete because the rocket was replaced by the sniper.
- U-017 tighter ring-out bounds and projectiles passing through one-way platforms: completed in 0.1.7.
- U-018/U-019/U-021/U-023: Geometry Fighters direction, visual pass, ArenaBuilder extraction and geometric bodies are complete.
