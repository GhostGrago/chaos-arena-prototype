# Claude 工程入口

这是一个 Unity 2.5D 平台射击派对游戏原型。当前版本为 **Prototype 0.1.4**，工程状态以 `PROJECT_STATE.md` 为唯一事实来源。

## 开始工作前

按顺序阅读：

1. `PROJECT_STATE.md`
2. `NEXT_TASKS.md`
3. `docs/2026-09-01_工程交接-Claude-report.md`
4. `updates/VERSION_0_1_4.md`
5. `GAME_VISION.md`
6. 仅在需要历史理由时读取 `DECISIONS.md`

不要把 `docs/archive/` 中的历史状态覆盖当前文件。

## 当前工程

- Unity项目：`game-client/`
- Unity版本：`6000.5.10f1`
- URP：`17.5.0`
- 平台：Windows
- 构建：`game-client/Tools/build-prototype.ps1`
- 验证：`game-client/Tools/verify-prototype.ps1`
- 可执行文件：`game-client/Builds/Prototype01/ChaosArenaPrototype.exe`

```powershell
& .\game-client\Tools\verify-prototype.ps1
& .\game-client\Tools\build-prototype.ps1
```

构建前如果游戏正在运行，只结束名称精确为 `ChaosArenaPrototype` 的进程；不要使用破坏性Git或文件清理命令。

## 已完成的0.1.4功能

- 一名玩家对一名AI，Easy/Normal/Hard，默认Easy。
- 2.5D透视场景、单向平台、二段跳、分阶段空中重力、水平射击。
- 受击不会因内部值归零直接死亡；受击越多，后续击飞越强；只有出界扣库存生命。
- 玩家与AI身体互相穿过，但攻击仍可命中。
- 最后一条生命耗尽后停止战斗、显示胜者，`R`立即重赛；没有开局倒计时。
- 镜头以竞技场为锚，只小幅平滑跟随本机玩家。
- 基础Carbine无限弹药；三个固定有限弹药拾取：Pulse SMG、Scatter、Rocket。
- 三个拾取点、最终淘汰、胜者和重赛重置已有自动烟雾断言。

## 当前优先级

先完成0.1.4人工试玩并记录具体问题：

- 三种武器的区分度、弹药、刷新、击退和击杀速度；
- 火箭范围与散射近身伤害是否过强；
- AI争夺拾取物是否公平；
- 最终淘汰、胜者显示、重赛和0.9秒重生保护；
- 轻跟随镜头在强击飞时是否仍保留整个平台。

未完成候选依次是：U-009抓边、U-011本机玩家屏外方向提示、U-007两人Host/Join、U-014低重力/月球模式。除非用户明确批准新的版本批次，不要直接实施。

## 重要边界

- 新建议默认写入 `updates/CANDIDATES.md`，攒成连贯版本并获得用户明确批准后再改代码。
- 不恢复同机多人；正式方向是2–4人在线，每台客户端镜头跟随自己拥有的角色。
- 联网和Steam尚未选技术、未安装包、未配置服务。
- 不使用参考Flash游戏的代码、地图、UI、角色、音效或素材制作公开版本。
- 网络素材必须记录商业许可和来源；优先CC0/可商用免费素材。
- 当前美术、音效和数值均为原型，不得宣称生产完成。
- 本地项目文件是Codex与Claude协作时的主要工作副本。每次开始和结束版本迭代时，先检查工作树、`main`、`origin/main`和对应版本标签；如本地与远端不一致，先查看提交和差异并保留未提交内容，不要盲目pull、push、merge或覆盖。

## 开发流程

用户随时提需求，但不一定立刻实施；默认先记入 `updates/CANDIDATES.md` 攒批，由Claude判断何时能组成连贯、可测试的版本批次（0.1.5、0.1.6…）。

批准规则按改动性质分类（用户2026-09-02确认，细化D-018的"用户批准"）：

| 改动性质 | 是否先问用户 |
|---|---|
| 修bug、调数值、用户已明确提过的需求 | 不用问，做完再报告 |
| 新玩法、改设计方向、Claude自己提出的新功能 | 先列清单，等用户点头 |

每个版本批次的固定步骤：

1. 开工前看一眼 `git status` 和改动文件即可；不必每次深入排查GitHub，别在这上面花时间。
2. 小范围改代码。
3. 运行Unity构建和 `-chaosSmokeTest`。
4. 同步更新 `PROJECT_STATE.md`、`NEXT_TASKS.md`、`updates/VERSION_*.md` 和 `updates/CANDIDATES.md`。
5. 提交、打 `v0.1.x` 标签、推送。

## 远程仓库

- `https://github.com/GhostGrago/chaos-arena-prototype`（**private**），默认分支 `main`。
- **本地文件是唯一事实来源**；云端只是版本记录，推送前看一下改动文件就够了。
- Codex与Claude不会同时工作（一方上传时另一方休息），因此不需要处理并发冲突；谁改完谁提交即可。
- 0.1.1–0.1.3从未进入git，无法重建；`v0.1.4` 是可追溯历史的起点。

## 修改约定

- 使用小范围、可验证修改，保留用户现有文件。
- 修改后至少运行Unity构建和 `-chaosSmokeTest`。
- 不把编译/烟雾通过等同于主观手感通过。
- 同步更新 `PROJECT_STATE.md`、`NEXT_TASKS.md`、对应 `updates/VERSION_*.md` 和 `SESSION_FINAL_HANDOFF.md`。
