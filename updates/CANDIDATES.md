# ACTIVE UPDATE CANDIDATES

Updated: 2026-09-02

This file contains only unfinished feature candidates. The original detailed backlog is archived at `../docs/archive/updates/CANDIDATES_FULL_2026-09-01.md`.

## Highest priority

### U-009 — Ledge recovery

Add one readable, learnable recovery opportunity near arena edges.

### U-011 — Remaining offscreen guidance

Prototype 0.1.4 completed arena-anchored local-player soft follow. A clear local-player offscreen/ring-out direction indicator remains unfinished; two-character dynamic framing was rejected for the future per-client multiplayer camera direction.

## Carried over from 0.1.4 playtest (balance, not yet approved)

### U-015 — Scatter point-blank burst

All five pellets connect at contact range for 17.5 internal damage and 6.25 base knockback in one trigger pull, before the health multiplier. Consider fewer pellets, lower per-pellet knockback, or close-range falloff.

### U-016 — Rocket self-knockback

`Explode` skips the owner entirely, so firing a rocket at an adjacent enemy carries no risk. Decide whether this stays forgiving or gains self-knockback for risk/reward.

### U-017 — Ring-out margin and rocket detonation on one-way platforms

Ring-out triggers only past |x|>16 while platforms end near ±9.5, creating a long invisible fall. Rockets also detonate on the underside of one-way platforms. Tune alongside U-009.

## Requested 2026-09-02 — visual and level pass (record only, not approved)

用户在0.1.5试玩后提出：优化画面与背景，并把平台做得更复杂。以下为拆解后的候选，**尚未批准实施**。

### U-018 — 先选定世界观方向（其他美术工作的前置条件）

`GAME_VISION.md` 的A太空废品回收队 / B浮空岛魔法快递 / C微缩实验室事故仍未选择。在方向确定前投入正式美术，产出很可能作废：配色、造型、材质语言和场景母题都由方向决定。建议先做这个决定，再开美术批次。**这是U-019和U-021的阻塞项。**

### U-019 — 不依赖美术方向的可读性视觉升级（低风险，可先做）

不需要任何第三方素材或方向决定，全部仍是程序化生成：

- 材质分层：现在所有物体共用同一套Lit/Unlit纯色材质，缺少金属度/粗糙度差异，导致平台、角色、背景质感雷同。
- 光照：加边缘光/轮廓光把角色从背景剥离；当前只有一主一辅两盏平行光。
- 背景视差：现有山峰、天际线、月亮是静止的，可随镜头做轻微视差位移，强化2.5D纵深。
- 平台边缘可读性：出界是主要败因，平台边界应更明确（边缘高亮条、危险色渐变）。
- 弹丸与拾取物的辉光/对比度，确保在更复杂的背景上依然醒目。

⚠️ 风险：背景越复杂，角色和子弹越容易被淹没。派对格斗游戏的美术必须服从可读性，**任何背景升级都要同步验证角色轮廓与弹丸是否仍然一眼可见**。

### U-020 — 竞技场结构复杂化（玩法收益 > 贴图收益）

当前只有1块主平台+3块对称静止单向平台，路线单一。按 `GAME_VISION.md` 已列方向：

- 非对称、多层次布局，制造不同的上下路线和视野死角。
- 可移动平台。
- 临时掩体 / 少量可破坏结构。
- 阶段性场地变化，让中央不永远是唯一最优位置。

建议先做**非对称多层静态布局**（成本低、玩法收益最大），移动平台放在其后单独验证。

⚠️ 技术前置：`OneWayPlatform` 目前缓存静态 `Top` 高度，`FighterMotor.UpdateOneWayCollisions` 每帧按该高度切换 `Physics.IgnoreCollision`。**平台一旦会移动，这套逻辑必须先改**，否则穿透判定会错乱；移动平台带动角色也需要额外处理。

### U-021 — 把竞技场构建从 PrototypeBootstrap 拆出（U-020的前置重构）

`PrototypeBootstrap.cs` 已被交接报告标记为职责过多、联网前必须拆分。它现在同时负责建场景、建角色、比赛生命周期、HUD和烟雾断言。**在往里继续堆关卡内容之前**，应先抽出 `ArenaBuilder` 和数据化的关卡定义，否则关卡越复杂，重赛/对象生命周期出错的概率越高，后续联网重构成本也越大。

### U-022 — 打击感与音效升级

程序化占位音效和当前的命中反馈仍是原型级。可考虑命中停顿(hitstop)、屏幕震动、更分层的音效。与美术方向无关，可独立进行。

## Following candidates

### U-007 — Internet duel

Prove a two-player Host/Join match before expanding the networking design.

### U-014 — Low-gravity party mode

Create a separate moon/low-gravity ruleset.

## Completed candidate groups

- U-003–U-006: platforms, fighter collision rules, hidden values and AI difficulty presets.
- U-012–U-013: shooting feedback, AI behavior, hit impact and air-combat tuning.
- U-008: immediate-start stock match, final elimination, winner and rematch loop.
- U-010: Pulse SMG, Scatter Blaster and Rocket Launcher fixed pickup loop.
- 0.1.5: muzzle-flash collider fix (projectiles self-destructed at the barrel), auto-rematch, respawn protection shield and balanced auto-zoom camera.
- U-011 partial: arena-anchored local-player soft-follow camera; offscreen direction guidance remains active.
