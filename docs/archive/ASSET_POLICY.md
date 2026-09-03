# 临时与第三方素材政策

## 资产分区

### ReferenceOnly/ExtractedFlash

- 来源：用户授权的本地《Gun Mayhem 2》Flash 副本。
- 用途：内部玩法验证、尺寸参考和临时占位。
- 禁止：公开截图、宣传片、Demo、Steam 构建、商店页面、公开仓库和最终发行。
- 要求：每项资产保留来源标记，不允许改名后冒充原创资产。
- 生命周期：对应原创替代资产通过验收后立即从可构建目录移除。

### ThirdParty/Licensed

- 只接收许可证清晰、允许当前商业用途的素材。
- 成本与选型优先级：CC0/公有领域素材 → 明确允许商用、修改和随游戏分发且署名可履行的免费素材 → 合适的付费素材 → 项目自制或委托。
- 保存原始下载页面、作者、许可证、下载日期、是否需要署名及修改限制。
- “网上可以下载”不等于可商用；来源不清的素材不得进入项目。
- `NonCommercial`、禁止必要修改、来源页面缺失或授权主体不明确的素材不得进入公开版本。
- 原型期混用素材时也要遵守统一的比例、骨骼、碰撞体、材质和性能预算；玩法稳定后优先统一玩家角色、武器和关键场景轮廓。

### Original

- 项目自行创作、委托创作或通过明确合同获得完整项目使用权的资产。
- Steam 公开构建默认只允许 `Original` 与经过审核的 `ThirdParty/Licensed`。

## 构建门禁

- 临时提取素材必须存放在独立目录，不能放进最终资源目录。
- 在公开演示前执行一次资产来源审计。
- 任何来源或许可证为 `UNKNOWN` 的资产都会阻止公开构建。
- 最终发行包中不得包含 `ReferenceOnly/ExtractedFlash` 内容。

## 素材登记字段

| 字段 | 说明 |
|---|---|
| Asset ID | 项目内唯一编号 |
| Filename | 文件名 |
| Category | ExtractedFlash / Licensed / Original |
| Source | 本地样本路径或公开网页 |
| Author | 作者或发行方 |
| License | 许可证名称及版本 |
| Attribution | 是否需要署名及署名文字 |
| Allowed Use | 内部占位 / 商业使用 / 修改 / 再分发 |
| Replacement | 原创替换任务及状态 |

## 已登记的第三方素材

| 字段 | 值 |
|---|---|
| Asset ID | TP-001 |
| Filename | `game-client/Assets/Resources/Weapons/blaster-{a,b,d,e}.obj` |
| Category | ThirdParty/Licensed |
| Source | https://kenney.nl/assets/blaster-kit （Blaster Kit 2.1） |
| Author | Kenney (www.kenney.nl) |
| License | Creative Commons Zero (CC0 1.0) |
| Attribution | 不要求；许可原文：「You can use this content for personal, educational, and commercial purposes.」 |
| Allowed Use | 商业使用 / 修改 / 随游戏再分发 |
| Replacement | 无需替换；CC0 可直接用于公开构建 |
| 备注 | 仅使用网格。原始配色被运行时替换为本项目调色板，避免引入第二套美术语言。许可证原文随资产存放于 `Kenney-BlasterKit-License.txt`。 |
