---
状态: v7（自动推进：溶解 + 线生长 + 传送，编辑器实测，未在头显验证，2026-09-01）
日期: 2026-09-01（v1: 2026-08-31）
关联文档: "PlayScene 双模式与节奏谱生成规格.md"（玩法本体）；"Dance Capture 录制与回放规格.md"（take 数据）
---

# Dance Place 舞台与就位判定规格

> **文件名里的"三处"是历史遗留。v4 起是六处。**

场景里有**六处舞台**，排在一条**之字形时间线**上，彼此 55.3 米。**站上某一处才会出现面板和玩法**。每处是时间线上的一个年代。

## 0. 之字形时间线（v4）

这不是随手选的形状，是剧本自己的画面（第 115 行）：

> 「IFEL 的物件退到远方时，我们正站在一条**之字形时间线**的起点上，它从脚下延伸，标着许多年份。」

v3 之前是等边三角形，传送 = 选一支舞；现在是一条线，**沿线前进 = 沿历史前进**。这条分歧在 `Wage Love 剧本翻译与实现对照.md` §5 里挂了很久，v4 按剧本这一侧解决。

### 0.1 六处舞台（按年代，`Dance Places` 下的顺序就是年代顺序）

| # | 舞台 | take | 内容 | 穹顶色 |
|---|---|---|---|---|
| 1 | `Stage 1 - Ancestors` | `Dance_1700sAncestraldances` | ⚠️ **空** | 深红 |
| 2 | `Stage 2 - 1964` | `Dance_1964Dancinginthestreet` | ⚠️ **空** | 金 |
| 3 | `Stage 3 - 1984` | `Dance_1984Dancinginstreets` | 完整 | 亮青（手调） |
| 4 | `Stage 4 - 2016` | `Dance_2016compnoholdingback` | 完整 | 墨绿 |
| 5 | `Stage 5 - 2017` | `Dance_2017Wiacwagelove` | 完整 | 紫 |
| 6 | `Stage 6 - 2024 MIT` | `Dance_2024Mitancestors` | ⚠️ **空** | 蓝 |

> **三个"空" take 是刻意的占位。** 没有 samples / video / characterAnimation，走进去只有穹顶、面板和三个立正不动的舞者，控制台会出一条 `[DancePlayer] No recording assigned` 警告——这是诚实的缺内容提示，不是 bug。每个的 `label` 字段写了它对应剧本第几页要什么素材。补内容就是往这个 SO 上挂东西，舞台不用动。

### 0.2 坐标与朝向

X 在 0 / 36 之间交替，Z 每级 +42：

```
  Stage 1 (0,   0)   Stage 2 (36,  42)   Stage 3 (0,  84)
  Stage 4 (36,126)   Stage 5 (0,  168)   Stage 6 (36, 210)
```

相邻间距 **√(36²+42²) = 55.32 m**，满足"至少 50"。整条线跨 210 m。

**每处舞台朝向"你走进来的方向"**（`LookRotation(pos[i] - pos[i-1])`，第一处借用第一段的方向），所以沿线走过去时屏幕总在正前方。之字形下 yaw 在 **40.6° / 319.4°** 之间交替。这一条同时决定了引导球和节奏球的落点（§4 的"朝向取舞台"）。

### 0.3 地面被放大了

原来的 `Plane` 只有 ±50 m，在第三处舞台底下就断了。现在 position `(18, 0, 105)`、scale `(14, 1, 32)`，覆盖 x −52..88 / z −55..265。

## 1. 玩家循环

```
空场（无面板、无玩法）
  → 脚下有一条 2 米宽的白线，之字形伸向远方   ← 这就是寻路
  → 线上串着六个不同颜色的穹顶
  → 过去（⚠️ 怎么过去现在是空的，见 §5）
  → 走进 6 米：面板亮起，屏幕开始放视频，舞者开始跳
  → 打 beat / 跟引导球
  → 走掉 → 本轮成绩清零
```

**这条线就是选曲界面，也是叙事。** 用走路代替菜单——这是这个设计最值钱的地方，不要在它上面再叠一层选曲 UI。v4 之后它还多了一层意思：**往前走 = 往后的年代走**。

> ✅ **v7：位移的缺口补上了，但不是靠传送锚点。** 玩家不再自己选择去哪——`TimelineDirector` 带着他沿时间线走：驻留 → 穹顶溶解 → 白线生长 → 传送到下一处。见 §14。

## 2. 组成：一套玩法 + 六份布景

> **只有布景是多份的，玩法只有一套。**

| | 份数 | 内容 |
|---|---|---|
| **布景**（`DanceStage.prefab`） | ×6 | 穹顶、圆形地板、视频屏 + **自带 VideoPlayer**、舞者 ×3、站位锚点（`Standing Anchor`）、世界面板 |
| **玩法** | ×1 | `BeatSpawner`、引导球 ×2、combo trail ×2、判定体积、`DancePlayer` |

> ⚠️ **v4 把布景从 3 份变成 6 份，蒙皮角色也就从 9 个变成 18 个。场景加载时 18 个 SkinnedMeshRenderer 都要实例化**，一体机上的加载时间和内存必须重新量。见 §11.5。

### 2.3 舞者按距离渲染（v5）

**舞者的开关脱离了"就位"，改成纯距离。**

| | 半径 | 归谁管 |
|---|---|---|
| 舞者渲染 | **20 m**（`dancerRenderRadius`，滞后 +3 m） | `DancePlace.UpdateDancerProximity` |
| 就位（面板、玩法、视频） | 进 6 m / 出 8 m | `DancePlaceManager.Resolve` |

**为什么渲染半径要比进场半径大。** 绑在就位上的话，舞者会和面板同一瞬间冒出来——六米开外凭空出现三个人，读起来是"弹出"。20 米下你先在穹顶外面看见他们在跳，然后走进去。**运行时代价没变**：舞台间距 55.3 m，中点离两边都是 27.7 m，所以任何时刻至多一处舞台在 20 m 内，还是三个舞者。

关掉的是**整个 GameObject**，所以 `SkinnedMeshRenderer`、`Animator` 和中控的 `PlayableGraph` 一起停，不只是省一个 draw call。

**滞后 3 m 是必须的**：站在边界上会让中控每帧重建一次 playable graph。

> **顺带把所有权理顺了。** 舞者原来由 `PlayModeController` 通过 `SetRecording(take)` / `SetRecording(null)` 驱动，现在完全归 `DancePlace`。`PlayModeController` 上的 `characters` 字段整个删掉了——舞者本来就不分模式，它没有理由知道舞者存在。
>
> 附带效果：**走出舞台时舞者不会立刻停**。你回头还能看见他们在跳，直到走出 23 米。这比原来"一转身人就没了"更像回事。

### 2.1 舞台是一个 prefab，实例之间只差一个 `take`

`Assets/Prefabs/DanceStage.prefab`。加第四处舞台的完整步骤是：

```
拖进场景 → 摆位置 → 把 take 换成另一个 DanceRecording → 加进 Stage Timeline 的 stops → 完
```

就位判定不用连线也不用登记：`DancePlaceManager` 在 `Start()` 里自己找齐场上所有 `DancePlace`（见 §3.3）。**白线是唯一要手工登记的地方**，因为年代顺序推不出来（见 §5.2）。

**实测（v4，六处）：每一处相对 prefab 只有 `DancePlace` 和 `Transform` 两处 override**——也就是 take 加上摆位。

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

### 5.0 由此产生的缺口（v7 已由自动推进填上，见 §14）

场景里**已经不存在任何 `TeleportationAnchor` 或 `TeleportationArea`**（实测：全场只剩 rig 上的 `TeleportationProvider` 和 `ClimbTeleportInteractor`，都没有可落点）。所以：

- 传送射线还能打出来，但**打不到任何合法落点**，按下去什么都不会发生
- 唯一的位移手段是**摇杆**，24 米大约要推十几秒，而且 VR 里长距离连续位移容易晕

**这一条必须补，否则三处舞台实际上只剩你出生的那一处能玩。** 可能的补法（都没做）：

| 方向 | 说明 |
|---|---|
| 在地板上放 `TeleportationArea` | 最省事，但落点自由，玩家可能站歪，§4 的朝向锚定会失效 |
| 把 `TeleportationAnchor` 挂回 `Standing Anchor` | 保留精确落点与朝向，但那就是把刚删掉的东西加回来 |
| 换一种旅行方式 | 剧本要的是"沿时间线前进"，也许根本不该是传送——见 `Wage Love 剧本翻译与实现对照.md` §5 |

### 5.1 寻路现在靠白线和穹顶

灯柱原本兼任跨房间路标。它没了之后，路标是**脚下那条白线**（§5.2）加上**六个不同颜色的穹顶**（§8.5）。穹顶比柱子大得多，而且反向绕序让你从外面能直接看进去，所以远处也能看出里面在放什么；白线则回答了柱子从来回答不了的问题——**下一处在哪个方向**。

### 5.2 白线：`Stage Timeline`

场景根节点 `Stage Timeline`，`LineRenderer` + `StageTimeline` 组件。

| | |
|---|---|
| 宽度 | **2 m**（`width`，直接写进 `widthMultiplier`） |
| 高度 | y = **0.015**（`height`） |
| 材质 | `Assets/Materials/StageTimeline.mat`，URP/Unlit 纯白 |
| 折点 | 每段舞台之间**再插 3 个**（`foldsPerLeg`），左右各偏 **9 m**（`foldAmplitude`） |
| 点 | 6 处舞台 + 15 个中间折点 + 首尾引线 = **23 个点** |
| 拐角 | `numCornerVertices = 6` |

**用 `LineRenderer` 而不是生成 mesh**：之字形的难点全在拐角，`numCornerVertices` 免费解决；而且线是**跟着舞台走的**——`[ExecuteAlways]` 下拖动任何一处舞台，线立刻跟过去，不用重新生成资产。

首尾的引线是顺着第一段/最后一段的方向延长的，所以尾巴仍在之字上，不会拐向别处——注意 v5 之后"第一段"指的是第一个**折点**而不是第一处舞台。

#### 5.2.0 中间折点（v5）

只在舞台处折一次的话，两处舞台之间是 55 米的一条直线，读起来是"连线"而不是"路"。现在每段之间再插 3 个折点，交替向左右偏 9 米：

```
lead-in ─╮   ╭─╮   ╭─ Stage2 ─╮   ╭─╮   ╭─ Stage3 ...
      Stage1 ╰─╯   ╰─         ╰─╯   ╰─
```

**左右的符号是跨段连续计数的**，不是每段从头开始。每段重置的话，一处舞台两侧会出现两个同向的折，之字在那里会"卡壳"。

偏移方向是 `Cross(up, 段方向)`——**与该段垂直且水平**，所以折点只往旁边走，不会爬高。折点落在 t = 0.25 / 0.5 / 0.75，离最近的舞台 13.8 m 以上，**不会插进 8 m 的穹顶里**。

#### 5.2.1 三个高度只差 1 厘米

```
0.03   Facing Arrow      朝向箭头
0.02   Standing Marker   站位圆板    ← 压在白线之上（它得能看见）
0.015  白线                          ← 就挤在这一格
0.01   Dome Floor        穹顶地板
0     Plane             地面
```

白线 2 米宽、正好穿过站位圆板（1.6 米），所以两者必然重叠。**圆板在上是对的**——它告诉你站哪儿。实测这个 5 mm 的间隔在穹顶地板上没有出现深度冲突。

#### 5.2.2 ⚠️ 材质必须双面

`StageTimeline.mat` 的 `_Cull = 0`（Off）。

**这是踩过的坑，而且症状极具误导性。** `TransformZ` 对齐下带状面的正面朝**下**，普通的背面剔除材质**在任何角度都什么都不画**——从天上看不到，贴地看也看不到。同时组件报告的点数、宽度、bounds 全部正常，控制台一片干净，所以每一种错误猜想看起来都一样成立。第一次排查时先怀疑的是"0.015 米高差被深度精度吃掉了"，把线抬到 0.6 米重拍——截图**字节完全相同**，这才排除了深度这条路。

**线要是哪天又不见了，先查 `_Cull`。**

### 5.3 位移方案要重新算距离

§5.0 列的三个补法在 v4 之后代价变了：舞台间距从 24 米变成 **55.3 米**。如果补回抛物线传送，`Velocity` 18 的最大射程是 34.4 米——**够不到下一处**。要么再提 `Velocity`（约需 25 以上），要么改成沿线连续位移，后者其实更贴剧本（第 93 行：站着不动时间线会自动推进，也可以沿线传送加速）。

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

### v4（2026-09-01，六处舞台 + 之字形时间线）

| 文件 | 改动 |
|---|---|
| **新增** `StageTimeline` | 白线：按给定顺序把舞台串起来，`[ExecuteAlways]` 跟着舞台走，间距不够会警告 |
| **新增** `Assets/Materials/StageTimeline.mat` | URP/Unlit 纯白，**`_Cull = Off`**（必须，见 §5.2.2） |
| **新增** 三个占位 take | `Dance_1700sAncestraldances` / `Dance_1964Dancinginthestreet` / `Dance_2024Mitancestors`，全空，只有 `label` 和 `domeColor` |
| `PlayScene` | 舞台 3 → 6，重排为之字形并各自转向；`Plane` 放大到 x −52..88 / z −55..265；新增 `Stage Timeline` |
| **未动** `DancePlace` / `DancePlaceManager` / prefab | 六处舞台全部是同一个 prefab 的实例，一行代码没改 |

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
| `dancerRenderRadius` | `DancePlace` | 20 m（必须 > enterRadius，见 §2.3） |
| `dancerRenderHysteresis` | `DancePlace` | 3 m |
| `width` | `StageTimeline` | 2 m |
| `height` | `StageTimeline` | 0.015（只有 1 cm 余量，见 §5.2.1） |
| `foldsPerLeg` | `StageTimeline` | 3 |
| `foldAmplitude` | `StageTimeline` | 9 m |
| `leadIn` / `leadOut` | `StageTimeline` | 24 m |
| `minSpacing` | `StageTimeline` | 50 m（低于就警告） |
| `stops` | `StageTimeline` | **按年代排**，唯一要手工维护的列表 |
| ~~`Velocity`~~ | 手柄的 `Teleport Interactor` | 仍是 18，但**当前没有任何落点可打**（§5.0） |

## 11. 已知问题

**11.5 舞者加载代价——v6 已优化一轮，见 §12。** 场景 Transform 从 1368 降到 198，网格从 13.22 MB 降到 10.19 MB。**仍未在一体机上量过。**

**11.6 🔴 三角形预算才是真正的问题，而且没动。** 单个舞者 91,258 三角 × 3 = **273,774 三角**，一体机整帧预算通常在 100–200k——**光舞者就超了**，还没算穹顶、屏幕、UI、绿幕视频。66.3k 顶点是 Tripo 生成模型的原始密度，对 8–20 米外的背景角色高了 5–10 倍。要么减面到 10–15k，要么做 LODGroup。**这需要改美术资产，不在本次优化范围内。**

**11.0 位移——v7 已解决，方式是自动推进（§14）。** 玩家被带着沿线走，不再自己选。**代价是"走路即选曲"没有了**：这一版是纯线性导览。剧本两种都要（第 93 行：站着不动自动推进，也可沿线传送加速），所以手动那条路以后能加回来，但现在不存在。

**11.1 画面比例——已自动处理，但没有真素材验证过。** `DancePlace.fitScreenToVideo` 会按 clip 的实际宽高比，在 quad 原有尺寸内**等比内接**缩放（竖屏素材会变成 1.01×1.80 的窄高屏，而不是被拉扁）。

⚠️ **目前三个 take 挂的全是 1920×1080，所以这条逻辑实测下来是恒等变换（三处屏幕都仍是 3.20×1.80）——等于没被真正验证过。** `Video2` / `Video2-2` 那两段 612×1088 竖屏素材还没挂到任何 take 上；哪天挂上去，第一件事是看屏幕有没有变窄。

**11.1b 灰地板扣不掉。** `Video2` / `Video2-2` 脚下是灰白地板而非绿幕，扣完背景会剩一块灰板子在脚下。它的色度距离 0.166 太靠近人物（黑裤 0.215），提阈值去扣会连人一起吃掉。可行的做法是在 shader 里加一道**底部渐隐**。`Video1` 是全绿地面，没这个问题。

**11.1c 新素材的色彩元数据缺失。** 五段新视频导入时 Unity 都报 `Color primaries 0 is unknown ... may result in color shift`。意味着**引擎里看到的绿色可能与源文件不同**，key 颜色要对着实际画面调，不能照搬外部工具量的值。用 ffmpeg 重封装写入 bt709 元数据可以根治。

**11.2 舞者仍然不跟 take 对齐。** 角色 clip 29.93 s、take 48.67 s，中控走自己的时钟。三处各自独立，问题照旧（见 PlayScene 规格 §6.3）。

**11.3 模式选择仍然是 X 键。** 面板上选模式会更直观，但那是另一件事，不在本规格内。

**11.4 全部未在头显验证。** 传送落点、站位朝向提示、6/8 米的进出半径手感，都必须戴上头显实测——尤其是半径，坐着开发时完全试不出来。

## 12. 舞者加载优化（v6）

改的全是 `Assets/MotionCaptures/SuperFusionAncestor (1).fbx` 的导入设置，**一行代码、一处场景都没动**（导入设置存在 `.meta` 里）。

| 设置 | 改前 | 改后 | 收益 |
|---|---|---|---|
| **`Optimize Game Objects`** | Off | **On** | 每个舞者 **67 → 2** 个 Transform |
| `Import BlendShapes` | On | Off | 该网格 `blendShapeCount = 0` |
| `Import Cameras` / `Import Lights` | On | Off | FBX 里的相机灯光没人用 |
| `Skin Weights` | 4 骨/顶点 | **2** | 网格 **13.22 → 10.19 MB** |

实测结果：

```
场景 Transform    1368  →  198      （舞者骨骼原本占 88%）
每个舞者          67    →  2        （Animator 根 + SkinnedMeshRenderer）
网格              13.22 →  10.19 MB
```

### 12.1 `Optimize Game Objects` 是有前提的，别随手关掉

打开它之后**骨骼不再是 GameObject**，改为存在 Animator 内部。所以：

- `SkinnedMeshRenderer.bones` 变成空数组，`rootBone` 是 null
- 任何"把东西挂到舞者手上"的做法都会失效

**这个项目可以打开，是因为查过没有任何代码读骨骼**（`mixamorig` / `.bones` / `GetBoneTransform` 在 `Assets/Scripts` 下 0 命中）。中控只绑 `Animator`，走 Playables；引导球和节奏球用的是玩家的手，不是舞者的骨头。

将来真要挂东西，用 Rig 页的 **Extra Transforms to Expose** 单独暴露那一根，**不要把整个开关关掉**——那会把 1170 个 Transform 加回来。

### 12.2 骨权重那 3 MB 一直是白装的

`QualitySettings` 当前只有一个等级 `Mobile`，`skinWeights = 2`。也就是说**运行时本来就只用 2 根骨头**，网格里多存的那 2 组权重被加载进来然后忽略掉。所以这一项是**零画质代价**的 3.04 MB。

> ⚠️ 但**提交版**的 `QualitySettings.asset` 里还有第二个等级 `PC`，`skinWeights = 4`（你本地把 PC 那级删了，而且那个删除没有提交）。谁要是在 PC 等级下跑，蒙皮会从 4 骨降到 2 骨。这个项目的目标是一体机，`Mobile` 才是生效的那个。

### 12.3 没做的那一步：共用一组舞者

18 份实例里永远只有 3 份是活的（间距 55.3 m、渲染半径 20 m，中点离两边 27.7 m，至多一处在范围内）。把 3 个舞者做成全场共用一组、按当前舞台重新挂载，可以做到：

```
18 实例 → 3      Animator 18 → 3      而且与舞台数量无关（O(1)）
```

**没做，因为 v6 这一轮已经把 Transform 打到 198，再优化的收益远小于它带来的代价**：舞者会变成和 beat/guide 玩法同一类东西（全场一套、按舞台重挂），这和"每个 stage 都有自己的 dancer avatars"是冲突的。等一体机上实测发现 198 个 Transform 还是不够，再动。

## 14. 自动推进：溶解、生长、传送（v7）

剧本第 93 行：「如果玩家站着不动，时间线会**自动线性推进**，作为一部交互式纪录片来看。」v7 把这个做出来了，顺带填上了删掉传送锚点留下的洞。

### 14.1 每一处的状态机

`TimelineDirector`（挂 `Play Controller`，和其他常驻组件一起）：

| 阶段 | 时长 | 发生什么 |
|---|---|---|
| Dwell | 3 s | 站在台上，面板/视频/舞者/玩法照常跑 |
| Dissolve | 1.5 s | 穹顶和地板一起溶解，然后整个 `SetActive(false)` |
| Grow | 3 s | 白线从本处长到下一处 |
| Settle | 2 s | 停一拍，让人看清线去了哪 |
| Travel | 2×0.35 s | 淡出 → 移动+转向 → 淡入 |

一处约 9.5 s，全程实测 **60.5 s** 走完六处。

> **`dwellSeconds` 是占位。** 将来换成"immersive video 播完"时，**只需要改 `StageComplete()` 这一个方法**——状态机其余部分不需要知道区别。这是刻意留的缝。

### 14.2 它不决定"当前是哪个舞台"

**只负责移动玩家。** 就位仍然由 `DancePlaceManager` 的距离判定接管——传送到中心自然落进 6 米内，面板、视频、玩法自己起来。

两个组件都自认为拥有"当前舞台"是这类系统最容易烂的地方，所以只留一个权威。

### 14.3 顺带做掉的两件

**生长那 3 秒里预热下一处的视频**（`WarmVideo()`）。传送过去当场有画面，省掉解码器 1.7 秒的暖机黑屏。

**被带走时提交成绩而不是作废。** `LeaveStage()` 走的 `Abandon()` 是为"玩家自己走掉"设计的；被系统带走是一次完成。新增 `PlayModeController.CompleteStage()`，先 `FinishPass()` 再走正常拆卸；`DanceFollowScore.FinishPass()` 因此改成 public。

### 14.4 溶解 shader

`Assets/Shaders/DomeDissolve.shader`，穹顶和地板**共用一个 shader、两个材质**。

| | 穹顶 | 地板 |
|---|---|---|
| `_Cull` | 2（Back，面朝内） | 0（Off，双面） |
| `_Sweep` | 0.55（顶部先开） | 0（平的圆盘，扫掠没意义） |
| `_NoiseScale` | 8 | 14 |

**噪声在物体空间、程序化生成，不用贴图。** 物体空间是因为两个网格都是单位尺寸，同一个 `_NoiseScale` 在 8 米穹顶和 3 米地板上读数一致，而且物体移动时图案不会跟着漂。不用贴图是因为等距柱状投影的球在两极会把贴图挤成一点——而那正是从穹顶内部最显眼的地方。

**它采样 `_BaseMap`**，虽然穹顶现在是纯色。球的 UV 当初就是为 360 视频做的等距柱状投影，将来接 immersive video 不该意味着重写这个 shader。

**Alpha test 而不是透明**：穹顶是个房间，在消失之前必须一直写深度、一直遮住后面。

阈值按 `_Dissolve × (1 + _EdgeWidth)` 过冲，保证 `_Dissolve = 1` 时**一点残留都不剩**（实测确认）；边缘发光乘了 `step(0.0001, _Dissolve)`，否则每个穹顶在什么都还没开始时就顶着一圈发光的边。

### 14.5 ⚠️ 淡入淡出不能用 Canvas

`ScreenFade` 是**挂在相机下的世界空间面片**（0.1 m 处，0.6 m 见方，约覆盖 143°），不是 `Screen Space - Overlay` 的 Canvas。

**Overlay Canvas 在头显里根本不渲染**——用它做淡入淡出，在显示器上完美，戴上头显就什么都没有。这是那种"两个月后才会有人发现"的坑。

不透明度为 0 时整个 Renderer 关掉：一块盖住双眼的全屏透明面片在一体机上不是免费的。

### 14.6 踩过的坑：用帧宽度开窗判断时刻

第一版在 `Travel()` 里用 `elapsed < half + Time.deltaTime` 判断"刚跨过淡出中点"。**这是一个一帧宽的窗口，取决于帧时间落在哪里，可能触发零次，也可能触发两次。**

触发两次时 `index` 连加两次，**整整一站的 Dwell 和 Dissolve 被跳过**。实测症状：跑完一遍后 Stage 3 的 `dissolve` 仍是 0、穹顶还立着，另外五处都正常——而玩家确实走到了终点，所以从外面看一切"差不多是对的"。

改成显式 `arrived` 标志。**任何"某一帧刚好跨过某个时刻"的判断都要用标志，不要用 `Time.deltaTime` 开窗。**

### 14.7 可调变量

| 变量 | 位置 | 默认 |
|---|---|---|
| `dwellSeconds` | `TimelineDirector` | 3 s（占位，见 §14.1） |
| `dissolveSeconds` | `TimelineDirector` | 1.5 s |
| `growSeconds` | `TimelineDirector` | 3 s（**每段固定时长**，不是速度） |
| `settleSeconds` | `TimelineDirector` | 2 s |
| `fadeSeconds` | `TimelineDirector` | 0.35 s（单程） |
| `matchFacing` | `TimelineDirector` | true |
| `_EdgeColor` / `_EdgeWidth` | 两个穹顶材质 | 暖橙 / 0.08 |

### 14.8 已知问题

**14.8a 线的生长速度可能太快。** 每段实际路径约 78.4 m，3 秒 = **26 m/s**。头显里没看过。

**14.8b 走完第 6 处就停。** 不循环、不回头，玩家站在线的尽头。

**14.8c 全程没有在头显里验证过**——尤其是被动转向 + 淡入淡出的舒适度，那是坐着开发试不出来的。

## 13. 明确不在本规格范围内

- **每处舞台不同的玩法参数**（不同 BPM、不同判定宽容度）。目前三处共用同一套配置。
- **成绩存档 / 跨舞台累计**。离场即清零，不留记录。
- **面板上的选曲与模式切换**（见 11.3）。
