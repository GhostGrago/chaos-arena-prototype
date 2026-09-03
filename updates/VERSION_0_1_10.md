# Prototype 0.1.10 — 真正的半透明果冻与边框修正

Status: `TESTED_BUILD — HANDS-ON TUNING REQUIRED`
Date: 2026-09-02

## 用户反馈

0.1.9 试玩：「还没有半透明，但是模型外面有一个白色的边框」。两个问题都确认属实。

## 修复

### 1. 半透明没有生效（0.1.9 的缺陷）

0.1.9 在运行时把不透明的 URP Lit 材质切换成透明混合（写 `_Surface`、`_SrcBlend`、`_DstBlend`、`_ZWrite`、开关键字）。**这个切换静默失败了**——没有报错，材质保持不透明。

工程的 `PITFALLS.md` 早就写过不要在代码里猜 URP 材质状态，`PrototypeLit` / `PrototypeUnlit` 作为 Resources 资产存在正是为此。0.1.9 违反了这条自己的规则。

**修法**：新增 `Assets/Resources/ChaosArenaMaterials/PrototypeJelly.mat` 材质资产，透明状态写在资产里：

| 字段 | 值 |
|---|---|
| `_Surface` | 1（Transparent） |
| `_SrcBlend` / `_DstBlend` | 5 / 10（SrcAlpha / OneMinusSrcAlpha） |
| `_ZWrite` | 0 |
| `m_CustomRenderQueue` | 3000 |
| `RenderType` 标签 | Transparent |
| `m_ValidKeywords` | `_SURFACE_TYPE_TRANSPARENT` |
| `_Smoothness` | 0.9 |

`AssignJelly` 改为加载该资产并只覆盖颜色与 alpha（0.72），不再在运行时改混合状态。

**新增回归断言**：直接检查角色身体材质的 `renderQueue >= 3000` 且启用了 `_SURFACE_TYPE_TRANSPARENT`。这个失败模式以后由烟雾测试自动捕获，不再依赖肉眼发现。

### 2. 白色边框

边框颜色原为 `Lerp(角色色, 白, 0.5)` 再乘 1.6 强度，两者叠加把颜色洗成了白色。

改为 `Lerp(角色色, 白, 0.12)`、强度 1.15，边框现在保留角色本身的颜色，读作角色的一部分而不是一圈白轮廓。

## 验证结果

- Windows Development Build：`Build Successful`。
- `-chaosSmokeTest` 通过：`CHAOS_ARENA_0110_ASSERTIONS_PASS`、`SMOKE_READY`、`SMOKE_PASS`。
- 透明断言实际执行并通过，证明材质资产确实生效（这是 0.1.9 缺失的验证）。

## 仍需试玩确认

- 半透明程度（alpha 0.72）是否合适；过透会伤害可读性。
- 多个半透明角色重叠时的排序是否正常（`_ZWrite: 0` 的常见代价）。
- 边框颜色现在是否过暗、还能不能起到勾勒轮廓的作用。
- 遗留未处理：U-009抓边（需先做AI挂边感知）、U-015散射贴脸、U-016火箭无自伤。
