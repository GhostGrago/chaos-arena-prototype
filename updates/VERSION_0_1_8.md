# Prototype 0.1.8 — 撤回抓边、霓虹几何与武器造型

Status: `TESTED_BUILD — HANDS-ON TUNING REQUIRED`
Date: 2026-09-02

## 用户反馈驱动的三项改动

0.1.7试玩后用户反馈：人机卡在边缘上；几何体太简单，希望更复杂并有霓虹灯光感；枪械系统要更复杂。

## 已撤回

### U-009 抓边回场（0.1.7引入，本版撤回）

**这是0.1.7引入的缺陷，不是调参问题。** `BotController` 完全不知道"挂边"状态，人机抓住边缘后继续发常规指令，结果在边缘反复「抓住→超时脱手→坠落→再抓住」，表现为卡在平台边上。

修复需要给AI增加挂边感知与主动脱手决策，属于独立工作量。按用户要求先整体撤回，`FighterMotor` 中相关状态、`TryFindLedge` 与对应断言一并移除，无残留死代码。实现保留在 `v0.1.7` 标签中，恢复时可直接取回。

## 已实现

### 霓虹几何：Bloom 后处理

- `ArenaBuilder.BuildPostProcessing` 运行时构建全局 `Volume`：Bloom（阈值0.85、强度1.35、散射0.72）、Vignette、ColorAdjustments（对比+14、饱和+12）。
- 相机启用 HDR 与 `renderPostProcessing`，并开启 SMAA。**没有这一步 Bloom 不会生效。**
- 新增 `PrototypeMaterials.AssignNeon`：把 Unlit 颜色推到 1.0 以上，使 Bloom 能够捕捉，材质才真正"发光"而不只是刷亮。

### 角色复杂化：发光边框

新增 `ProceduralShapes.CreateEdgeFrame`，按形态描出发光线框，套在哑光实体外：

| 形态 | 边框 |
|---|---|
| 立方体 | 12 条棱 |
| 四面体 | 6 条棱 |
| 球体 | 2 个正交圆环 |
| 圆柱体 | 上下圆环 + 4 根立柱 |

统一由 `CreateBar` 生成（拉伸的细方块，可连接任意两点），圆环由分段连线构成。朝向眼改为高强度霓虹。

### 竞技场霓虹化

平台边缘条、正面导轨、新增的**底部辉光带**、甲板灯全部改为霓虹材质，平台从灰板变成"通电的硬件"。

### 枪械造型系统

新增 `WeaponVisual`：按当前武器实时重建手持模型，换枪即换造型，**四把枪剪影刻意不同**。

| 武器 | 造型 |
|---|---|
| Carbine | 长单管 + 发光瞄具 |
| Pulse SMG | 短双管 + 发光线圈与弹匣 |
| Scatter | 宽机身 + 三根短管 + 发光收束口 |
| Rocket | 粗管 + 上下尾翼 + 发光弹头 |

注：0.1.7改为几何形态时误删了手持武器模型，本版一并补回并做成按武器区分。

### 构建修复

`ChaosArena.Runtime.asmdef` 原先 `references` 为空，引用 URP 命名空间导致 `CS0234` 编译失败。已加入 `Unity.RenderPipelines.Core.Runtime` 与 `Unity.RenderPipelines.Universal.Runtime`。

## 验证结果

- Windows Development Build：`Build Successful`。
- `-chaosSmokeTest` 通过：`CHAOS_ARENA_018_ASSERTIONS_PASS`、`SMOKE_READY`、`SMOKE_PASS`。
- 新增断言：每个角色具备发光边框（子物体≥4）与武器挂点。
- 既有断言（枪口特效碰撞体、人机名单、最后存活判定、四面体网格）继续通过。

## 仍需试玩确认

- Bloom 强度是否过曝，霓虹会不会盖掉角色可读性。
- 边框线宽 0.055 在远景下是否看得清、是否产生锯齿噪点。
- 四把枪的造型在战斗距离下是否真的能分辨。
- 后处理对帧率的影响（SMAA + Bloom 有成本，低配机器需实测）。
- 遗留未处理：U-015散射贴脸过强、U-016火箭无自伤，仍需新地图重测。
