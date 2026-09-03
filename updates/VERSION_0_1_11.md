# Prototype 0.1.11 — 击退失效修复与移除边框

Status: `TESTED_BUILD — HANDS-ON TUNING REQUIRED`
Date: 2026-09-02

## 用户反馈

1. 「为什么打 AI 很多时候没有击退」
2. 「模型外面还是有一个框架，虽然颜色跟模型一样了」

## 修复：击退被移动控制吃掉（长期存在的缺陷）

### 根因

`FighterMotor.FixedUpdate` 每个物理步都**直接覆盖**水平速度：

```
newX = MoveTowards(当前速度x, 输入方向 × moveSpeed, 加速度 × fixedDeltaTime)
```

地面加速度 35/秒。Carbine 满血击退冲量仅 3.25，因此移动控制会在 **约 0.09 秒内把整个击退抵消**。`AddForce` 确实执行了，但下一个物理步就被覆盖掉。

这解释了「很多时候」的分布规律：

| 情形 | 击退是否可见 |
|---|---|
| 目标在空中（加速度 14） | 部分可见 |
| 目标血量低（倍率最高 3.5x） | 可见 |
| **目标站在地面且正在移动** | **几乎不可见** |

AI 一直在主动移动并朝玩家推进，正好落在最差的一档，所以对 AI 的手感最差。这个缺陷自 0.1.2 引入移动控制以来一直存在，只是 0.1.5 修好枪口特效后才第一次有机会被观察到。

### 修法：受击硬直

`FighterMotor` 新增控制锁：

- `ApplyKnockbackStun(seconds)` 挂起移动控制。
- 锁定期间保留动量，仅施加轻微阻力（地面 5/秒、空中 1.2/秒）自然收敛。
- `Fighter.TakeHit` 按冲量大小调用，时长 0.15–0.45 秒。

击退现在真正生效，且**越重的击飞失控越久**，符合"受击越多飞得越远"的既定规则。

### 回归断言

新增 `AssertKnockbackSurvivesMovement`：验证控制锁初始未激活、调用后确实激活。这个失败模式此前完全没有测试覆盖。

## 移除角色边框

0.1.8 加的发光线框在果冻材质下读作"模型外面套了个笼子"。0.1.10 已修正其颜色，但用户确认结构本身不需要。

- 角色不再生成 `Edge Frame`。
- `ProceduralShapes` 中的 `CreateEdgeFrame` / `CreateRing` / `CreateBar` / `TetrahedronCorners` 随之成为死代码，一并删除，无残留。
- 对应断言移除。

竞技场平台的霓虹边缘条**保留**——那是出界可读性所需，与角色边框无关。

## 验证结果

- Windows Development Build：`Build Successful`。
- `-chaosSmokeTest` 通过：`CHAOS_ARENA_0111_ASSERTIONS_PASS`、`SMOKE_READY`、`SMOKE_PASS`。

## 0.1.11 发布前的追加调整（2026-09-02，未新增版本号）

### 重生保护改为减伤减击退

用户决定：保护期不再完全免疫，改为削弱。完全免疫会让攻击看起来"没有判定"，即使有护盾圆环提示，读起来仍像 bug。

- 保护期内伤害 ×0.35，击退 ×0.4（`Fighter.ProtectedDamageScale` / `ProtectedKnockbackScale`）。
- 保护时长不变：出界重生 1.2 秒、开局与重赛 0.9 秒。
- 现在保护期内攻击**有明确反馈**，只是难以立刻再次淘汰对手。

新增断言 `AssertProtectionWeakensRatherThanBlocks`：受保护角色必须掉血但掉得比正常少，两个方向都检查。

### ESC 暂停菜单

此前**没有任何退出游戏的方式**，窗口只能靠任务管理器关闭。

- `ESC` 开关菜单，菜单内含 `RESUME` / `RESTART MATCH` / `QUIT GAME`。
- 暂停时 `Time.timeScale = 0`，并挂起比赛逻辑与所有游戏热键。
- `CombatFeel` 新增暂停感知：否则命中顿帧的计时器会在暂停期间把 `timeScale` 恢复成 1，导致菜单背后游戏继续跑。

新增断言 `AssertPauseRestoresTime`：暂停后 `timeScale` 为 0、恢复后回到 1。

## 仍需试玩确认

- 硬直 0.15–0.45 秒是否合适。**过长会让被打时感觉失控、操作黏滞**，这是本版最主要的风险。
- 满血状态下 Carbine 的击退现在是否够明显，还是仍需上调基础数值。
- 另一个可能的"没有击退"来源尚未改动：**重生保护 1.2 秒内完全免疫**，包括不吃击退。若在保护期内攻击 AI 同样毫无反应，护盾圆环是唯一提示。如觉得该时长过长可下调。
- 遗留未处理：U-009抓边、U-015散射贴脸、U-016火箭无自伤。
