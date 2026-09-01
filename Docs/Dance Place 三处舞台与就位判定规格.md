---
状态: v3（传送已从舞台移除，编辑器实测，未在头显验证，2026-09-01）
日期: 2026-09-01（v1: 2026-08-31）
关联文档: "PlayScene 双模式与节奏谱生成规格.md"（玩法本体）；"Dance Capture 录制与回放规格.md"（take 数据）
---

# Dance Place 三处舞台与就位判定规格

场景里有**三处舞台**，彼此至少 20 米。玩家用传送锚点在它们之间移动，**站上某一处才会出现面板和玩法**。每处是一支不同的舞。

## 1. 玩家循环

```
空场（无面板、无玩法）
  → 看见远处三个不同颜色的穹顶   ← 这就是寻路
  → 过去（⚠️ 怎么过去现在是空的，见 §5）
  → 走进 6 米：面板亮起，屏幕开始放视频，舞者开始跳
  → 打 beat / 跟引导球
  → 走掉 → 本轮成绩清零
```

**三处舞台就是选曲界面。** 用走路代替菜单——这是这个设计最值钱的地方，不要在它上面再叠一层选曲 UI。

> 🔴 **v3（2026-09-01）：传送已按要求从舞台 prefab 里整个删除**，包括柱子、它的 `CapsuleCollider` 和 `TeleportationAnchor`。**现在没有任何跨舞台的位移手段**——见 §5，这是一个待补的缺口，不是一个已完成的设计。

## 2. 组成：一套玩法 + 三份布景

> **只有布景是三份的，玩法只有一套。**

| | 份数 | 内容 |
|---|---|---|
| **布景**（`DanceStage.prefab`） | ×3 | 穹顶、圆形地板、视频屏 + **自带 VideoPlayer**、舞者 ×3、站位锚点（`Standing Anchor`）、世界面板 |
| **玩法** | ×1 | `BeatSpawner`、引导球 ×2、combo trail ×2、判定体积、`DancePlayer` |

### 2.1 舞台是一个 prefab，实例之间只差一个 `take`

`Assets/Prefabs/DanceStage.prefab`。加第四处舞台的完整步骤是：

```
拖进场景 → 摆位置 → 把 take 换成另一个 DanceRecording → 完
```

不用连线，不用登记：`DancePlaceManager` 在 `Start()` 里自己找齐场上所有 `DancePlace`（见 §3.2）。

**实测：`Stage 2` / `Stage 3` 相对 prefab 各自只有一处 component override，就是 `DancePlace` 的 `take`。**（`Stage 1` 是 prefab 的来源，零 override。）

> **这条是要守住的不变式。** 如果你发现自己在某个实例上手改了别的东西——抠像阈值、海报、穹顶颜色——那个设置就该搬到 `DanceRecording` 上去，而不是留在实例里。三处舞台一旦开始各自漂移，prefab 就白做了。

所以下面这些**全部**从 `DancePlace` 挪进了 `DanceRecording`：

| 字段 | 为什么属于 take 而不属于舞台 |
|---|---|
| `poster` | 是这段视频的海报 |
| `chromaKey` / `keyColor` / `keyThreshold` / `keySmoothness` / `spillRemoval` | 是这次拍摄怎么打光的属性，跟着素材走（见 §6.2） |
| `domeMaterial` / `domeColor` | 是这个年代的场所长什么样 |

> 为什么不另做一个 "StageProfile" 资产？因为那就等于**又回到两处挂载点**——正是把 take 从 `PlayModeController` 挪到 `DancePlace` 时要解决的问题。一处舞台 = 一个 SO，就一个。

### 2.2 玩法为什么不进 prefab

| 原因 | |
|---|---|
| 玩法绑在**手上**，不绑在舞台上 | 引导球、combo trail、判定体积都是手柄的子物体，而手只有一双 |
| **prefab 不能序列化场景引用** | 三份玩法就得在运行时 `Find` 一遍 rig，把现在明确的连线换成隐式查找 |
| 三份状态要三份地关 | 现在是"换参考系"，物件自己就过去了 |

引导球和节奏球的世界位置来自 `DanceReferenceFrame`，不是来自父物体的 transform。换舞台 = 换参考系，物件自己就过去了。

三份玩法的代价对比（按现有资产估）：

| | 一套 | 三套 |
|---|---|---|
| `BeatSpawner` | 1 | 3 |
| 引导球 | 2 | 6 |
| **舞者（1 个 SkinnedMesh / 66.3k 顶点 / 52 骨骼）** | 3 | **9** |

舞者是布景，必须是三份；但**离场的舞台要整组停掉**。9 个蒙皮角色同时跑在一体机上，比三张 RenderTexture（约 10.5 MB）贵得多——**要省先省这个，不是省视频**。

> ⚠️ **换成 `SuperFusionAncestor` 之后单个舞者从 20.5k 涨到 66.3k 顶点，是原来的 3.2 倍。** 同时只有一处舞台激活，所以实际在跑的是 3 个约 20 万顶点（原来约 6 万）。**这个数字必须在一体机上实测**——它是目前整个场景最贵的一项。

## 3. 就位判定：距离 + 滞后，不用 trigger box

每帧算头部到三处站位点的距离，取最近且在半径内的那个。

```
进入半径  6 m
离开半径  8 m     ← 必须比进入半径大
```

### 3.1 为什么不用 trigger box

一个大盒子在**这个**场景里有具体的坑：

```
XR Origin 上有 CharacterController          → 会触发 OnTriggerEnter
两个手柄上各挂一个 Beat Hit Volume(isTrigger) → 也会触发
```

玩家站进去时**两只手也在盒子里**，会收到 3 次 Enter。更麻烦的是 `Beat Hit Volume` 会随模式开关激活/停用——**每切一次模式就伪造出一对 Enter/Exit**。要用就得按组件类型或 layer 过滤。

距离判定没有这些问题：3 次比较、开销可忽略、进出半径能分别设（trigger box 要做滞后得套两个盒子）、出问题在 Inspector 里一眼能看出来。

### 3.2 `DancePlaceManager` 不能挂在会被停用的物体上

> 和 `PlayModeController` 同一条硬约束：自我停用的组件永远无法把自己重新启用。它跟 `PlayModeController` 一起挂在 `Play Controller` 上。

### 3.3 舞台是找出来的，不是登记出来的

`places` 数组**留空**，`Start()` 里 `FindObjectsByType<DancePlace>` 拿全部。

理由是舞台现在是 prefab 实例，会被随手复制。一份手工列表意味着"复制一个 prefab 之后还得回来登记一下"，而**忘了登记不会报错，只会让那处舞台永远不激活**——这种沉默失败正是应该用代码消掉的。

数组不为空时按数组走，用于故意屏蔽掉场上某几处。场上一处都没有时会警告。

## 4. 参考系：位置取玩家，朝向取舞台

这是整个功能**最容易做错**的一点。

现在 `DanceReferenceFrame.Capture(head)` 的**位置和朝向都取自玩家头部**。舞台是世界里的固定位置，屏幕和舞者也朝着固定方向——直接沿用就会出事：

| 锚定方式 | 后果 |
|---|---|
| 全取玩家（现状） | 玩家背对屏幕传送进来，球全生成在他背后 |
| 全取舞台 | 一定面朝屏幕，但玩家实际没站在设计师标的那个点上时，球会整体偏掉 |
| **位置取玩家、朝向取舞台** ✅ | 球总在"玩家与屏幕之间"，同时贴合他实际站位 |

```csharp
// DanceReferenceFrame 已经有 public 构造函数，这个结构本身不用改
new DanceReferenceFrame(head.position, place.StandingRotation)
```

要改的是 `DancePlayer` 和 `DanceRecordingBeatSource`：它们现在**写死**调用 `Capture(head)`，需要能接收外部传入的参考系。

> 地上要放脚印/朝向标记。玩家不知道该朝哪站的话，这套锚定逻辑再对也没用。

## 5. 位移：🔴 当前是空的

**v3 把传送从舞台 prefab 里删掉了**（按要求："把 prefab 里面的 teleport 相关都删除掉"）。删掉的是：

| 删掉的东西 | 它原来干什么 |
|---|---|
| `Beacon` 整个 GameObject | 柱子本体：`MeshFilter` + `MeshRenderer` + `CapsuleCollider` + `TeleportationAnchor` |
| `DancePlace.beacon` 字段与它的开关逻辑 | 就位时把柱子整根关掉 |

**保留**的是 `Teleport Anchor` 这个空物体本身（已改名 **`Standing Anchor`**）以及它下面的 `Standing Marker` / `Facing Arrow`。它叫 teleport 只是历史包袱；它实际是 §4 的**站位与朝向**，`DancePlace.standingAnchor` 指着它，删了整套锚定就没了。

### 5.0 由此产生的缺口

场景里**已经不存在任何 `TeleportationAnchor` 或 `TeleportationArea`**（实测：全场只剩 rig 上的 `TeleportationProvider` 和 `ClimbTeleportInteractor`，都没有可落点）。所以：

- 传送射线还能打出来，但**打不到任何合法落点**，按下去什么都不会发生
- 唯一的位移手段是**摇杆**，24 米大约要推十几秒，而且 VR 里长距离连续位移容易晕

**这一条必须补，否则三处舞台实际上只剩你出生的那一处能玩。** 可能的补法（都没做）：

| 方向 | 说明 |
|---|---|
| 在地板上放 `TeleportationArea` | 最省事，但落点自由，玩家可能站歪，§4 的朝向锚定会失效 |
| 把 `TeleportationAnchor` 挂回 `Standing Anchor` | 保留精确落点与朝向，但那就是把刚删掉的东西加回来 |
| 换一种旅行方式 | 剧本要的是"沿时间线前进"，也许根本不该是传送——见 `Wage Love 剧本翻译与实现对照.md` §5 |

### 5.1 寻路现在靠穹顶

灯柱原本兼任跨房间路标。它没了之后，**三个不同颜色的穹顶就是路标**（§8.5）——它们比柱子大得多，而且反向绕序让你从外面能直接看进去，所以远处也能看出里面在放什么。

---

## 5-旧. 传送与灯柱（v2 及以前，已移除，保留作参考）

### 5.1 射程由 `Velocity` 决定，不是 `Max Raycast Distance`

传送射线是 `XRRayInteractor`，`Line Type = ProjectileCurve`。

> ⚠️ **抛物线下射程只由 `Velocity` 决定。** `Max Raycast Distance` 只服务于 `StraightLine`，`End Point Distance` 只服务于 `BezierCurve`——两个看着都像"距离"，改了却没任何效果。

原值 10 最远只能跳 **11.5 m**，平着指只有 5.4 m——**根本到不了 24 米外的下一处**。现改为 **18**：

| Velocity | 水平瞄 | 仰角 30° | 最大射程 |
|---|---|---|---|
| 10（原） | 5.4 m | 10.8 m | 11.5 m |
| **18（现）** | 9.6 m | 30.9 m | **34.4 m** |

### 5.2 锚点是一根柱子，玩家靠近时整根关掉（已删除）

每处站一根 0.6 m 粗 × 3.5 m 高的灯柱，它同时是传送目标和跨房间可见的路标。

```
Teleport Anchor        空物体，transform = 站位 + 朝向
  ├─ Beacon            柱子：MeshRenderer + Collider + TeleportationAnchor
  ├─ Standing Marker   地面圆板
  └─ Facing Arrow      朝向箭头
```

> **`TeleportationAnchor` 挂在柱子上，不是父节点上。** 这样一次 `SetActive(false)` 同时关掉渲染、碰撞、传送目标三样，而不用在 `DancePlace` 里逐个开关——顺带也让 `DancePlace` 完全不需要引用 XRI。

关闭时机沿用 §3 的进 6m / 出 8m。因为它在玩家走到 6 米时就消失了，所以**永远不会撞到它**，也不会出现"传送到自己已经站着的地方"。

附带好处：**柱子让抛物线好瞄得多**。打一块 2×2 m 的地板需要精确控仰角；打一根 3.5 米高的立柱，弧线在任意高度擦到都算命中。

### 5.3 锚点本身（部分保留：空物体改名为 `Standing Anchor`）

rig 上 `Locomotion → Teleportation` 本来就是开着的。

**锚点的 transform 同时就是这处舞台的站位和朝向**（§4 的 `StandingRotation`），不要再单独维护一个站位点——两份数据必然会有一天对不上。

20 米用摇杆推过去大约 10 秒，而且 VR 里长距离连续位移容易晕。传送是舒适度上的正确答案，摇杆保留作微调站位用。

## 6. 视频：海报 ↔ 实时，**每处一个解码器**

三处都常驻一块屏，而且**每处舞台自带一个 `VideoPlayer`**。

| 状态 | 屏幕显示 |
|---|---|
| 未就位 | **海报**（静态贴图，`Texture2D`） |
| 就位、解码器暖机中（约 1.7 s） | **仍然是海报** |
| 就位、有画面 | RenderTexture（实时） |

关键在第二行：**暖机期间继续显示海报，而不是黑屏**。这样 1.7 秒完全不可见，玩家感觉是瞬时的。

### 6.0 为什么从"一个解码器轮流用"改成"每处一个"

v1 是全场一个 `VideoPlayer` 轮流借用。改掉它有两个理由：

**一、单个解码器换 clip 就等于重新暖机。** `Park()` 之所以便宜，是因为它不动 clip；而轮流用的那个播放器**每换一处舞台就换一次 clip**，等于每次都从头付暖机费。每处一个之后，走过一次的舞台是"暂停在原地"，再走回去下一帧就有画面。

> 实测（编辑器 Play 模式）：先站 Stage 1 → 走到 Stage 2 → 走回 Stage 1，回来时 `LiveTexture != null` 当场成立，屏幕没有黑过也没有回落到海报。三个 RenderTexture 的 instance ID 各不相同（`-18724 / -19082 / -19938`），确认没有共用。

**二、prefab 化要求它。** 舞台自带屏幕却要去场景里借一个播放器，就不是自足的 prefab。

代价是三个解码器和三张 RT——但**是懒分配的**：RenderTexture 在第一次 `WarmUp` 时按 clip 的实际尺寸建，没去过的舞台一张都不建。实测第三处在被踏上之前 `targetTexture` 是 `none`。

> ⚠️ **prefab 实例绝对不能共用一张 RenderTexture 资产。** 三个播放器会解码进同一块像素，屏幕上显示的是"最后写入的那一处"。所以 `DanceVideoScreen` 的规矩是：**`targetTexture` 留空就自己建一张私有的**；填了就用你填的（录制场景就是这么用的，保持不变）。

### 6.0b 三处的音频

`audioOutputMode = Direct`，三个播放器都能出声——但**没被站上的舞台是 `Park()`（暂停）状态，不出声**。只有当前这处在响。

> **海报目前没有素材，三个 take 的 `poster` 都是空的。** 空置时屏幕整块隐藏（而不是显示上一处残留的画面，这点是对的），所以远处看不到屏幕——**寻路靠的是灯柱**（§5.2），不是海报。补上海报只会让远景更好看，不是功能前提。
>
> 海报现在挂在 `DanceRecording` 上，不在 `DancePlace` 上（§2.1）。

已有的 `DanceVideoScreen` 规矩不变：**绝不 `Stop()`**，换舞台用 `Park()`（暂停 + seek）。

> ⚠️ **这里有个地雷。** 那 1.7 秒完全依赖视频**导入时转码成 VP8**。这台机器上 H.264 走系统解码器实测首帧要 **55 秒**。哪天有人丢一个没转码的 mp4 进来，"进入舞台"就会变成近一分钟的海报僵住，而且**不报任何错**。
>
> 新增视频必须：选中 `.mp4` → Inspector → 勾 Transcode → Codec 选 **VP8** → Apply。

### 6.1 循环播放

`DanceVideoScreen.loop`（默认开）。循环在解码器内部完成，**不走 `Stop()`，所以不会再付一次暖机**。

beat 模式必须开：那边没有任何东西会重启视频，不循环的话片子放完屏幕就定在最后一帧（现有素材最短只有 4.5 秒）。guide 模式每遍自己会 seek，不受影响。

### 6.2 绿幕抠像（运行时）

`Assets/Shaders/VideoChromaKey.shader`。参数存在 **take** 上，由 `DancePlace` 通过 MaterialPropertyBlock 推给屏幕材质（§2.1）。

**判定在 CbCr 色度平面做，不在 RGB 上做。** 色度距离忽略亮度，所以背景布上打光的亮部和阴影的暗部会一起被扣掉；RGB 距离会把它们当成不同颜色，暗部留下来。

实测（`Video2`，线性空间）：

| 区域 | 与背景的色度距离 |
|---|---|
| 背景（两处采样） | 0.000 / 0.024 |
| 荧光绿上衣 | **0.229**——看着绿，但安全 |
| 黑裤 | 0.215 |
| **灰地板** | **0.166**——真正卡住阈值的东西 |

所以阈值必须落在 **0.03 到 0.15** 之间。建议起手：阈值 0.08 / 柔边 0.05。

> ⚠️ **4:2:0 是这个方案的天花板。** 视频的色度只有一半分辨率，头发、手指、运动模糊都是拿被抹糊的色度数据去判定的。边缘不够好就改成**压缩前抠好、带 alpha 入库**（VP8+alpha 的 WebM，Unity 勾 `Keep Alpha`）——材质和场景部分不用返工。

> **v1 里列为"备选方案"的每处一个解码器，v2 已经采用**，理由见 §6.0。

## 7. 离场清零

走出离开半径（或传送去别处）时，**本轮作废**：

| 清理 | 说明 |
|---|---|
| 销毁在场的 `BeatTarget` | 复用现有 `TearDown()` |
| `BeatSpawner.StopSpawning()` | 同上 |
| `GuideOrb.ClearTrail()` ×2 | world-space 粒子不会因为隐藏而消失 |
| `BeatComboTrail.SetLevel(0)` | 已有 public API |
| **跟随率这一遍作废** | ⚠️ 见下 |
| 视频 `Park()`，屏幕换回海报 | |
| 本处舞者整组停用 | |

> ⚠️ **`DanceFollowScore` 目前没有"作废"这条路。** 它只有 `FinishPass()`，而那是**提交**成绩（写进 `LastPass*`）。直接调用会把一遍没跳完的残缺数据当成绩记下来。需要新增一条丢弃路径：清空累计值但**不**写 `LastPass*`、不置 `HasCompletedPass`。

## 8. 面板：世界固定，每处一块

面板立在每处舞台的屏幕旁边，**不再跟着头走**。

现状是 `Play UI` 挂在 `Main Camera` 下（头锁），走到哪跟到哪——那和"走到一个站点前"的设计是冲突的。

`PlayModeUI` 仍然**只有一个**（挂在 `Play Controller` 上，永不停用），由管理器把它的输出目标指向当前舞台的 `Text`。三块面板，一个写手。

**未就位时没有任何面板**——这是刻意的。寻路靠的是三个穹顶（§5.1），不是面板也不是海报。

## 8.5 穹顶与圆形地板

每处舞台外面套一个**只有内向面**的球（`ImmersiveSphere.prefab`，半径 8 m，球心在舞台原点上方 0.01 m），里面一块贴着球壁的圆形地板（`ImmersiveFloor.prefab`，`ImmersiveDomeFloor` 按 `r = √(R²-h²)` 自适应半径）。

原理见 `.ai/decisions/`（绕序反向，不是法线也不是 shader）与两个生成器的注释。这里只记跟舞台有关的三点：

**一、从外面能看进去。** 反向绕序下近侧半球被背面剔除，所以站在 Stage 1 能望见 Stage 2、3 的内部。**灯柱删掉之后，这一点从"顺带的好处"变成了寻路的唯一依据**（§5.1）。

**二、穹顶没有 collider。** 有 collider 的话任何跨舞台的射线都会打在自家墙上。灯柱删掉之后暂时没有东西需要被瞄准，但**补位移方案时这一条会立刻重新变成前提**（§5.0），所以别顺手给穹顶加碰撞体。

> v2 实测（灯柱尚在时）：从 Stage 1 手部高度（0, 1.2, 0）朝 Stage 2 的灯柱打射线，`RaycastAll` 只有 **1 个命中**，就是灯柱本身（23.71 m）。两侧穹顶壁、两块地板都不在路上。

**三、球心高 0.01 m，不是 0。** 地板落在 y = 0.01：压在地面 `Plane`（y=0）之上、站位圆板（y=0.02）之下。三者差 1 cm，避免 z-fighting。

半径 8 m 与 §3 的进出半径（6/8）正好对齐：**走到穹顶边缘 = 离开这处舞台**。

穹顶颜色来自 take 的 `domeColor`（§2.1）。注意它是**替换**材质颜色而不是相乘——从 `ImmersiveSphere.mat` 的蓝底靠相乘到不了暖棕和紫，所以这里用替换，默认值就是那个蓝。改了材质记得改默认值。

## 9. 改动了哪些现有代码

| 文件 | 改动 |
|---|---|
| `DancePlayer` | 能接收外部参考系，不再写死 `Capture(head)` |
| `DanceRecordingBeatSource` | 同上 |
| `DanceFollowScore` | 新增"作废本遍"路径（§7） |
| `PlayModeController` | `take` 从序列化字段改成运行时由舞台推入；`Mode` 增加 `None`（未就位）状态 |
| `PlayModeUI` | 输出目标可切换 |
| `DanceVideoScreen` | 海报 ↔ RT 的材质切换 |
| **新增** `DancePlace` | 一处舞台：take、屏幕、舞者、锚点、面板 |
| **新增** `DancePlaceManager` | 每帧选出当前舞台，处理进出 |

### v2（2026-09-01，prefab 化）

| 文件 | 改动 |
|---|---|
| `DanceRecording` | 新增 "Stage presentation" 段：`poster`、抠像五项、`domeMaterial`、`domeColor`（§2.1） |
| `DanceVideoScreen` | `targetTexture` 为空时按 clip 尺寸自建私有 RT，`OnDestroy` 释放；新增 `ClipAspect`（§6.0） |
| `DancePlace` | 多了 `videoScreen` / `domeRenderer`；抠像与海报改从 take 读；屏幕按视频比例自适应 |
| `DancePlaceManager` | 自动发现舞台；不再持有全场那个 `VideoPlayer`；`EnterStage` 改传整个 `DancePlace` |
| `PlayModeController` | 去掉 `videoScreen` 字段，视频转由当前舞台负责；进场时把舞台的 screen 推给 `DancePlayer` |
| `DancePlayer` | 新增运行时 `Screen` 属性；为空时回落到序列化的 `videoPlayer`（录制场景靠这条不受影响） |
| **删除** 场景根的 `Video Source` | 每处舞台自带 |
| **删除** 场景根的 `Immersive Sphere` | 内容进了舞台 prefab |

### v3（2026-09-01，移除传送）

| 文件 | 改动 |
|---|---|
| `DanceStage.prefab` | 删掉 `Beacon`（含 `TeleportationAnchor` + `CapsuleCollider`）；`Teleport Anchor` 改名 `Standing Anchor` |
| `DancePlace` | 删掉 `beacon` 字段与 `SetOccupied` 里关灯柱那一行 |
| **未动** rig 上的 `TeleportationProvider` / `Teleport Interactor` | 只删了 prefab 内的，射线还在，只是没有落点了（§5.0） |

### 9.1 take 的单一来源转移到舞台

`PlayModeController.take` 现在是序列化字段，是"选 take 的唯一一处"。改完之后**这个字段消失**，改由 `DancePlace.take` 提供、进场时推入。

单一来源的规矩不变，只是问法变了：**任一时刻只有一处舞台是激活的，它的 take 就是答案。** `DancePlayer` 和 `DanceRecordingBeatSource` 自己的 `recording` 字段仍然必须留空。

## 10. 可调变量

| 变量 | 位置 | 建议默认 |
|---|---|---|
| `enterRadius` | `DancePlace` | 6 m |
| `exitRadius` | `DancePlace` | 8 m（必须 > enter） |
| `take` | `DancePlace` | **每处不同——实例上唯一该改的东西** |
| `poster` | `DanceRecording` | 每处不同（目前均为空） |
| `loop` | `DanceVideoScreen` | true |
| `chromaKey` | `DanceRecording` | true |
| `keyColor` | `DanceRecording` | 每处不同，对着实际画面取 |
| `keyThreshold` | `DanceRecording` | 0.08（必须落在 0.03–0.15，见 §6.2） |
| `keySmoothness` | `DanceRecording` | 0.05 |
| `spillRemoval` | `DanceRecording` | 0.7 |
| `domeColor` | `DanceRecording` | 每处不同（1984 暖棕 / 2016 青 / 2017 紫） |
| `domeMaterial` | `DanceRecording` | 空（用 prefab 自带的） |
| `fitScreenToVideo` | `DancePlace` | true |
| ~~`Velocity`~~ | 手柄的 `Teleport Interactor` | 仍是 18，但**当前没有任何落点可打**（§5.0） |

## 11. 已知问题

**11.0 🔴 没有跨舞台的位移手段。** 传送已从 prefab 移除（§5），摇杆推 24 米又慢又晕。**这是当前最大的功能缺口**：不补的话，三处舞台里实际只有出生那一处能玩到。

**11.1 画面比例——已自动处理，但没有真素材验证过。** `DancePlace.fitScreenToVideo` 会按 clip 的实际宽高比，在 quad 原有尺寸内**等比内接**缩放（竖屏素材会变成 1.01×1.80 的窄高屏，而不是被拉扁）。

⚠️ **目前三个 take 挂的全是 1920×1080，所以这条逻辑实测下来是恒等变换（三处屏幕都仍是 3.20×1.80）——等于没被真正验证过。** `Video2` / `Video2-2` 那两段 612×1088 竖屏素材还没挂到任何 take 上；哪天挂上去，第一件事是看屏幕有没有变窄。

**11.1b 灰地板扣不掉。** `Video2` / `Video2-2` 脚下是灰白地板而非绿幕，扣完背景会剩一块灰板子在脚下。它的色度距离 0.166 太靠近人物（黑裤 0.215），提阈值去扣会连人一起吃掉。可行的做法是在 shader 里加一道**底部渐隐**。`Video1` 是全绿地面，没这个问题。

**11.1c 新素材的色彩元数据缺失。** 五段新视频导入时 Unity 都报 `Color primaries 0 is unknown ... may result in color shift`。意味着**引擎里看到的绿色可能与源文件不同**，key 颜色要对着实际画面调，不能照搬外部工具量的值。用 ffmpeg 重封装写入 bt709 元数据可以根治。

**11.2 舞者仍然不跟 take 对齐。** 角色 clip 29.93 s、take 48.67 s，中控走自己的时钟。三处各自独立，问题照旧（见 PlayScene 规格 §6.3）。

**11.3 模式选择仍然是 X 键。** 面板上选模式会更直观，但那是另一件事，不在本规格内。

**11.4 全部未在头显验证。** 传送落点、站位朝向提示、6/8 米的进出半径手感，都必须戴上头显实测——尤其是半径，坐着开发时完全试不出来。

## 12. 明确不在本规格范围内

- **每处舞台不同的玩法参数**（不同 BPM、不同判定宽容度）。目前三处共用同一套配置。
- **成绩存档 / 跨舞台累计**。离场即清零，不留记录。
- **面板上的选曲与模式切换**（见 11.3）。
