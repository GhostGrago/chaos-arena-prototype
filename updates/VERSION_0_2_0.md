# Prototype 0.2.0 — 在线主机与加入（Relay）

Status: `COMPILES — ONLINE UNVERIFIED, BLOCKED ON CLOUD PROJECT LINK`
Date: 2026-09-02

## 用户确认的版本边界

- 本人开主机，朋友只要有游戏副本即可加入。
- 加入后一起打 AI 或互相对决。
- 选择互联网中继而非局域网，因此朋友异地也能连。

## 为什么用 Relay 而不是直连

主机处于路由器 NAT 之后，外网无法主动连入。直连只能靠端口转发，而 `GAME_VISION.md` 明确写了「直接IP/手动端口转发不作为默认玩家体验」，且家庭宽带若为 CGNAT 则端口转发也无效。Relay 用房间码穿透，无需任何路由器配置。

## 已实现

### 新增包

`netcode.gameobjects 2.13.2`、`transport 2.7.4`、`services.core 1.18.0`、`services.authentication 3.7.4`、`services.relay 1.2.0`。版本号由查询 Unity 官方 registry 得到，非猜测。

### 架构：单一同步对象而非逐角色网络对象

竞技场与角色全部在运行时生成，无法作为网络 Prefab。因此不给每个角色挂 `NetworkObject`，而是由场景中唯一的 `NetMatch` 承载全部复制：

- 客户端 → 主机：`SubmitInputRpc` 上报输入。跳跃与下穿用**计数器**而非布尔，丢包只会延迟一次跳跃而不会吞掉。
- 主机 → 客户端：`BroadcastStateRpc` 以 25Hz 广播全部 4 个角色的位置、速度、血量、生命、武器、弹药、朝向、激活状态。
- 座位分配：主机固定 0 号，客户端按连接顺序取 1/2/3。

### 权威划分

- **主机**：跑物理、AI、比赛判定、出界与胜负。
- **客户端**：纯表现。本地刚体转为 kinematic，直接套用收到的位置（大偏差时瞬移，否则指数平滑）。本地 AI 与运动控制关闭。
- 被远程玩家占用的座位，其 `BotController` 在主机上关闭，改由该玩家输入驱动。

### 主菜单

游戏不再直接进入对局，改为先进菜单：

- `PLAY SOLO (vs BOTS)` — 完全离线，行为与 0.1.11 一致。
- `HOST ONLINE ROOM` — 登录 UGS、申请 Relay 分配、显示**房间码**。
- `JOIN ROOM` — 输入房间码加入。
- 暂停菜单新增 `LEAVE TO MENU`，可断开并返回。

Relay 端点显式选取 DTLS 加密端点——本版 Relay 包未提供 allocation 转 transport 的辅助函数，因此不做假设。

## 验证结果

- Windows Development Build：`Build Successful`。
- `-chaosSmokeTest` 通过：`CHAOS_ARENA_0111_ASSERTIONS_PASS` 等全部既有断言照常通过，**证明联机改动没有破坏单机模式**。

## ⚠️ 阻塞项：必须先绑定 Unity Cloud 项目

`ProjectSettings.asset` 中 `cloudProjectId` 为空。**在绑定之前，Host 与 Join 一定会失败**，游戏内会提示 `Online unavailable: link this project to a Unity Cloud project first.`

需要用户本人操作（涉及 Unity 账号登录，Claude 不代为登录）：

1. Unity Hub 打开 `game-client`。
2. 菜单 `Edit → Project Settings → Services`。
3. 用 Unity 账号登录，创建或关联一个 Unity Cloud 项目。
4. 在 Unity Cloud 后台为该项目启用 **Relay** 服务（有免费额度）。
5. 关闭编辑器后重新构建。

## 完全未验证的部分

**联机功能一行都没有实测过。** 编译通过与单机烟雾通过不代表联机可用。以下全部未验证：

- Relay 分配、房间码生成与加入是否成功。
- 主机与客户端之间的状态同步是否正确、平滑。
- 座位分配、断线处理。
- 客户端表现模式下的动画与特效表现。

绑定云项目后，**可在同一台机器上开两个游戏实例互相连接**来验证（两端都通过 Relay 出网，不依赖局域网）。

## 已知设计限制

- **无客户端预测**：客户端看到自己的移动有一个来回延迟。`GAME_VISION` 将预测列为后续工作，本版只求跑通闭环。
- 特效、音效、击退表现在客户端由本地逻辑触发，可能与主机不完全一致。
- 未做房间列表、准备状态、重连。
