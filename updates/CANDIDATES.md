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

### U-018 — 已于2026-09-02解决：选定"几何战士"

抽象几何方向已确定，见 `GAME_VISION.md` 与 `DECISIONS.md` D-019。美术批次的阻塞项解除。附带结果：角色与场景不再需要任何第三方素材，`ASSET_POLICY` 的授权风险在这一层消失。

### U-019 — 程序化部分已在0.1.6完成；贴图部分留待0.1.7

不需要任何第三方素材或方向决定，全部仍是程序化生成：

- 材质分层：现在所有物体共用同一套Lit/Unlit纯色材质，缺少金属度/粗糙度差异，导致平台、角色、背景质感雷同。
- 光照：加边缘光/轮廓光把角色从背景剥离；当前只有一主一辅两盏平行光。
- 背景视差：现有山峰、天际线、月亮是静止的，可随镜头做轻微视差位移，强化2.5D纵深。
- 平台边缘可读性：出界是主要败因，平台边界应更明确（边缘高亮条、危险色渐变）。
- 弹丸与拾取物的辉光/对比度，确保在更复杂的背景上依然醒目。

⚠️ 风险：背景越复杂，角色和子弹越容易被淹没。派对格斗游戏的美术必须服从可读性，**任何背景升级都要同步验证角色轮廓与弹丸是否仍然一眼可见**。

### U-020 — 竞技场结构复杂化（静态布局已在0.1.6完成，移动平台仍未做）

当前只有1块主平台+3块对称静止单向平台，路线单一。按 `GAME_VISION.md` 已列方向：

- 非对称、多层次布局，制造不同的上下路线和视野死角。
- 可移动平台。
- 临时掩体 / 少量可破坏结构。
- 阶段性场地变化，让中央不永远是唯一最优位置。

建议先做**非对称多层静态布局**（成本低、玩法收益最大），移动平台放在其后单独验证。

⚠️ 技术前置：`OneWayPlatform` 目前缓存静态 `Top` 高度，`FighterMotor.UpdateOneWayCollisions` 每帧按该高度切换 `Physics.IgnoreCollision`。**平台一旦会移动，这套逻辑必须先改**，否则穿透判定会错乱；移动平台带动角色也需要额外处理。

### U-021 — 已在0.1.6完成：ArenaBuilder 已拆出

`PrototypeBootstrap.cs` 已被交接报告标记为职责过多、联网前必须拆分。它现在同时负责建场景、建角色、比赛生命周期、HUD和烟雾断言。**在往里继续堆关卡内容之前**，应先抽出 `ArenaBuilder` 和数据化的关卡定义，否则关卡越复杂，重赛/对象生命周期出错的概率越高，后续联网重构成本也越大。

### U-022 — 打击感与音效升级

程序化占位音效和当前的命中反馈仍是原型级。可考虑命中停顿(hitstop)、屏幕震动、更分层的音效。与美术方向无关，可独立进行。

## 模式路线图（2026-09-02记录，均未批准实施）

用户希望这是一款多模式派对对战游戏。抽象几何身份使得任何模式都无需在设定上自圆其说。以下按建议顺序排列，**一次只做一个**，每个都要单独调到好玩。

### U-023 — 角色形态改为基本立体

把现有胶囊人替换为立方体/球体/四面体/圆柱体四种形态，加发光"眼"指示朝向。这是几何战士身份落地的第一步，也是所有模式的共同基础，应先于任何新模式。

### U-024 — 生化感染模式

被接触者转化为感染方，继续感染他人得分。⚠️ **核心动词是接触而非射击，这是另一套战斗系统**，不是换个胜负条件那么简单：需要接触判定、阵营切换、感染方与幸存方的不同目标与计分。视觉上用形体崩坏表达感染状态，不依赖UI。

### U-025 — 旋转与移动平台模式

转盘和移动平台上的战斗。⚠️ **技术前置**：`OneWayPlatform` 目前缓存静态 `Top` 高度，`FighterMotor.UpdateOneWayCollisions` 每帧据此切换穿透。平台可动前必须重写这套逻辑，另外还需处理移动平台带动角色。

### U-026 — 逃杀模式

更大场地与收缩机制。依赖更多关卡内容和人数支持，建议排在联网之后。

### U-027 — 强力武器模式

高强度武器狂欢局。相对最便宜，主要是数值与刷新规则的变体，可作为验证"模式框架"的第一个低成本试点。

### U-028 — 模式框架与每模式镜头

上述模式需要一个统一的规则框架（胜负条件、计分、出生、HUD可插拔），否则每加一个模式都要改 `PrototypeBootstrap`。当前镜头为固定2.5D侧视，不同模式可能需要不同视角，每套都是独立工程量。**建议在做第二个模式之前先抽出这个框架。**

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
- 0.1.6: ArenaBuilder extraction (U-021), six-platform asymmetric layout (U-020 static part), 1-3 bot free-for-all, hitstop and camera shake (U-022 partial), and the direction-neutral visual pass (U-019).
- 0.1.5: muzzle-flash collider fix (projectiles self-destructed at the barrel), auto-rematch, respawn protection shield and balanced auto-zoom camera.
- U-011 partial: arena-anchored local-player soft-follow camera; offscreen direction guidance remains active.
