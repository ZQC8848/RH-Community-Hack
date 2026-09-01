---
状态: v1（已实现并接入 PlayScene，未在头显验证，2026-08-29）
日期: 2026-08-29
关联文档: "Ring-Sphere 交互判定与美术规格.md"（判定球本身）；"Guide Orb 跟随引导球规格.md"（引导球）；"Dance Capture 录制与回放规格.md"（录制与回放）
---

# PlayScene 双模式与节奏谱生成规格

玩家在同一个场景里二选一：**Beat 模式**（打从录制数据生成的球）或 **Guide 模式**（把手放进沿录制轨迹移动的引导球）。**X 键**切换，两个模式读同一段 take。

## 1. 三个场景的分工

| 场景 | 用途 | 是否录制 |
|---|---|---|
| `SampleScene` | 判定球的开发测试场 | 否 |
| `DanceCaptureScene` | **录制场景**，发给协作者用 | 是 |
| `PlayScene` | **本文档的对象**，双模式游玩 | 否 |

`PlayScene` 是 `DanceCaptureScene` 的**副本**，删掉了录制侧（`DanceRecorder`、`DanceCaptureModeController`、`DanceCaptureUI`、录制用的 AudioSource）。

> **`DanceCaptureScene` 刻意保持原样。** 录制是整条数据管线的入口——没有它就没有 take，而 [DANCE_RECORDING_GUIDE.md](../DANCE_RECORDING_GUIDE.md) 是发给外部协作者的完整流程。"合并场景里不需要录制"不等于"项目里不要录制了"。

## 2. 模式切换

### 2.1 分组

**一条规则：只服务某一个模式的东西，就必须放在那个模式的组里。** 分组不只是为了好看——它是强制执行清理的机制。

```
Play Controller          永不停用。只放集成层
  ├─ PlayModeController
  └─ PlayModeUI

Beat Mode                分组根，随模式 SetActive
  ├─ BeatSpawner        → BeatSpawner + DanceRecordingBeatSource
  ├─ Left Combo Trail
  └─ Right Combo Trail

Guide Mode               分组根，随模式 SetActive
  ├─ Dance Player       → DancePlayer + DanceFollowScore
  │    └─ Music         回放用 AudioSource
  ├─ Left / Right Guide Orb
  └─ Left / Right Hand Trail
```

两个组都保持 identity（位置 0、旋转 0、缩放 1）——`GuideOrb` 把 `orbRadius` 当**世界**半径，父节点带缩放会让它报警。

> **两个组在存盘时必须都是激活的。** 场景加载时所有 `Awake` 先跑一遍，然后 `PlayModeController.Start()` 才去关掉一侧。存盘时就关着的那侧，`Awake` 永远不会在加载时跑。

命中体积（`Beat Hit Volume`）是手柄的子物体，没法分进组里，由控制器单独开关。

### 2.2 `PlayModeController` 必须挂在不会被停用的物体上

> **这是硬约束。** 自我停用的组件永远无法把自己重新启用。项目为此已经栽过一次——`DanceCaptureModeController` 当初就是为了解决同一个问题才存在的。

它挂在 `Play Controller` 上，那个物体在任何模式下都不会被关掉。

### 2.3 切换时必须清理的三件事

每一件都是"所有者被关掉之后仍然活着"的状态：

| 清理 | 不做的后果 |
|---|---|
| 销毁在场的 `BeatTarget` | 它们继续跑自己的状态机，在模式已经切走之后逐个判成 Miss-Timeout |
| `BeatSpawner.StopSpawning()` | 定时器继续跑 |
| `GuideOrb.ClearTrail()` ×2 | world-space 粒子**不会**因为发射体被隐藏而消失，会挂在空中 |

`TearDown()` 在 `SetActive(false)` **之前**跑，所以清理的时候对象还是活的。顺序反了就清不到。

> **原来还有第四件：手动 `player.enabled = !beat`。** 现在 `DancePlayer` 挂在 `Guide Mode` 组内，分组开关自然把它一起关了，那行已删。这不只是少一行代码：原先那行是**一条必须被记住的规矩**，任何人新增一个只属于 guide 模式的组件都得想起来再加一行；现在只要把它放进组里就行了。

### 2.4 `BeatSpawner.startDelay` 的含义变了

`StartSpawning()` 在**每次切换**都会重新套用 `startDelay`。原来的 5 秒是为"按 Play 然后戴头显"准备的，但 beat 模式从来不是启动模式，那 5 秒只会出现在主动切换时——切过去然后五秒钟什么都不发生，读起来像坏了。已改成 **1.5s**（一个生成间隔）。

### 2.6 视频由 take 决定，两个模式都放

视频只有一个来源：`take.video`。`DanceVideoScreen` 上**没有**任何自己的 clip 字段。

| 模式 | 谁驱动 | 行为 |
|---|---|---|
| Beat | `PlayModeController.ApplyVideo()` → `PlayFreely()` | 没有要对齐的东西，直接放 |
| Guide | `DancePlayer` | 每遍开头 `CueTo(inPoint)`，跟 take 对拍 |

**take 没挂视频 → 整块屏幕的 Renderer 关掉**，而不是留一块黑板。RenderTexture 没人写的时候保留的是上一次的内容，正是这一点让"没配视频"长期看起来像"渲染器坏了"。

> **视频必须由 `PlayModeController` 而不是 `DancePlayer` 起头。** `DancePlayer` 现在在 `Guide Mode` 组里，beat 模式下是关着的。只让它管视频的话，从默认的 beat 模式启动时屏幕全黑——这是分组重构引入的回归，实测才发现。

### 2.5 take 只在一处指定

`PlayModeController.take` 是**唯一**指定 take 的地方，切模式时往下推：

```
StartBeat()  → beatSource.SetRecording(take)
StartGuide() → player.LoadRecording(take)
```

所以 `DancePlayer.recording` 和 `DanceRecordingBeatSource.recording` 在本场景里**必须留空**。字段本身要保留——这两个组件在 `DanceCaptureScene` / `SampleScene` 里是独立工作的，那时候靠自己的字段。

> **为什么这不只是"冗余"。** 之前三处都指向同一个资产，于是 `StartGuide()` 里那句守卫 `player.Recording != take` 恒为 `false`——**分发路径从来没执行过**。真正该生效的机制被重复赋值掩盖着，只有等到两边不一致那天才会第一次跑到那条分支。现在它每次切到 guide 模式都会真的执行。

## 3. 节奏谱的位置来源

**位置**来自录制数据，**时间**仍然是定时器。

### 3.1 必须按 T_perfect 采样，不是 T_spawn

> ⚠️ **这是整套机制里最容易做错、而且错了不会报错的一点。**

`BeatTarget.Initialize(config, perfectTimeDsp)`，ring 收缩 `ringLeadTime`（1.2s）之后才是击打时刻。所以球必须放在**手在 T_perfect 那一刻的位置**：

```
perfectTimeDsp = 生成时刻 + ringLeadTime
采样时间       = perfectTimeDsp 对应的录制时间
```

按生成时刻采样的话，整张谱面会**系统性地比舞蹈早 1.2 秒**：球出现在手 1.2 秒前待过的地方，然后要求玩家 1.2 秒后回到那里。画面上一切正常——球在动、位置来自真实数据——但它和舞蹈完全对不上，而且没有任何报错。

因此 `BeatPlacementSource.GetPlacements()` 的参数刻意命名为 `perfectTimeDsp` 并在注释里写明"这是击打时刻，不是生成时刻"。

### 3.2 手别 → flavour 由配置自己决定

`BeatConfig_Cyan.allowedHands = Right`、`BeatConfig_Magenta.allowedHands = Left`，这个映射早就存在。所以采样自哪只手，就用哪个 flavour——**原来随机选颜色那段已经删掉**，数据本身决定了颜色。

`BeatPlacement.hand` 为 `Either` 时（随机散布模式）才回退到随机选 flavour。

### 3.3 一次生成两颗球

每一拍同时取左右手的位置，各生成一颗。48.7s 的 take、1.5s 间隔 → 32 拍 × 2 手 = **每循环 64 颗球**。

### 3.4 参考系：开场采样一次玩家头部

录制里的位置是**相对于当时舞者头部**的，所以用 `DanceReferenceFrame.Capture(head)` 锚定到玩家头部，球就会出现在"当时舞者的手相对头部的位置"。玩家站哪里、朝哪边都自适应。

**锚定发生在第一次放置时，不是 `Start()`**：XR 头显姿态在第一帧之前是无效的，在 `Start()` 里锚定会把整张谱面钉在摄像机的占位姿态上。

### 3.5 模块边界

`BeatSpawner` 在 `Interaction/`，按既定决策不能依赖录制系统；`DanceRecording` 在 `DanceCapture/`。所以中间隔了一层抽象：

```
BeatPlacementSource（抽象基类，Interaction/）
  ├─ BeatSpawnArea            （Interaction/）  随机散布，保留作测试用
  └─ DanceRecordingBeatSource （DanceCapture/） 从录制取位置
```

用抽象 `MonoBehaviour` 而不是 interface，是为了能在 Inspector 里当普通组件引用序列化。

## 4. Combo Trail：beat 模式的拖尾要挣

和引导球同款的 `HandTrail`，但不是白给的——打得好才长出来。

| | 变化 |
|---|---|
| Perfect | 等级 **+2** |
| Good | 等级 **+1** |
| Miss-Touch / Miss-Timeout | 等级 **−1** |

等级 0–5，**长度和颜色鲜艳度**都按 `level / 5` 插值。等级 0 时**直接清空**而不只是缩短——`HandTrail` 有 1 秒宽限期，光缩短的话最后一笔还会继续画一秒。

颜色跟 beat flavour 对齐：左手 magenta、右手 cyan，取的是 rim 色（线条读的是亮部，和引导球同一条规矩）。低等级往灰暗方向拉，高等级走向本色。

进入 beat 模式时等级**重置为 0**——跨模式带过来的拖尾不是这一轮挣到的。

`BeatSpawner.OnBeatSpawned` 事件是为此加的：计分方挂到每颗球的 `OnResolved` 上，而 spawner 不需要知道谁在听。

> ⚠️ **加分是瞬时的，扣分不是。** `BeatTarget` 对 Perfect/Good 在 `TryTouch` 里同步结算，但 Miss-Touch 和 Miss-Timeout 要**先播完消失动画**（约 0.25–0.3s）才触发 `OnResolved`。所以打中时拖尾立刻变长，打错时慢半拍才缩。这不是 bug，是沿用判定球原有的设计——但测试时它看起来非常像"扣分不生效"。

## 5. 长按 B 重锚

同一个手势在两个模式下要落到**不同的对象**上——beat 模式锚定在 `DanceRecordingBeatSource`，guide 模式锚定在 `DancePlayer`。两者互不知情，所以手势归 `PlayModeController` 管。

| 场景 | 谁处理 | 时长 |
|---|---|---|
| `PlayScene` | `PlayModeController` | **1s** |
| `DanceCaptureScene` | `DancePlayer`（原样） | 3s |

靠 `DancePlayer.handleRecalibrateInput` 这个开关区分。**不加这个开关的话**，guide 模式下会同时有两个处理器、还是两个不同的时长，按下去会连续触发两次重锚。

beat 模式重锚时**会先销毁在场的球**——它们是按旧参考系放置的，留着会让画面上同时存在两套坐标系。

## 6. 舞台角色：一条 clip 驱动三个人

**每处舞台**站着三个 `SuperFusionAncestor (1)` 实例，由该舞台的 `DanceCharacterDirector`（挂在 `Stage N/Dancers` 上）统一驱动。三处共 9 个，但同时只有被占用的那一处在跑（见 Dance Place 规格 §2）。

| | |
|---|---|
| 模型 | `SuperFusionAncestor (1)`，66,303 顶点 / 1 个 SkinnedMesh |
| clip 来源 | `DanceRecording.characterAnimation`，由 `PlayModeController` 推下来 |
| clip 长度 | 29.93 s / 30 fps |
| 站位 | `(-2.4, 0, 2.2)`、`(2.4, 0, 2.2)`、`(0, 0, 1.8)`，面朝玩家 |
| 尺寸 | 模型原高 1.091 m，**scale ×1.639** → 1.788 m |

**不需要 AnimatorController。** 中控用 Playables 图把 clip 直接喂进 `Animator`。所以加第 4、第 5 个舞者只是往数组里多拖一个引用。

### 6.0 ⚠️ 模型和 clip 不同源，靠 Humanoid 重定向

clip 来自 `LaunchPad_Compassion_Dance_test`，模型是 `SuperFusionAncestor (1)`——**两套骨架毫无关系**：

| | clip 来源 | 模型 |
|---|---|---|
| 根链 | `Newton/Root/Hips` | `mixamorig:Hips` |
| 腿 | `LeftThigh / LeftShin` | `mixamorig:LeftUpLeg / LeftLeg` |
| 脊椎 | Spine1→2→3→4（4 节） | Spine→Spine1→Spine2（3 节） |
| 骨骼数 | 79 | 67 |

> **所以两边的 FBX 都必须是 `Animation Type = Humanoid`，而且场景里每个 `Animator` 都必须挂上模型的 Avatar。**

Generic clip 是**按骨骼路径名**绑定的，光是 `mixamorig:` 前缀就让所有路径失配，一根骨头都绑不上。转成 Humanoid 后 clip 变成肌肉空间的（`clip.humanMotion == True`），才与具体骨架无关。

⚠️ **这三件事错了都不报错，人就是站着不动**——和 §6.1 的共享 playable、以及 take 重复赋值是同一类静默失败：

1. 任一侧 FBX 忘了设 Humanoid
2. `Animator.avatar` 忘了挂
3. 换了模型但没重连 `DanceCharacterDirector.dancers`

排查时先看 `Animator.isHuman` 和 `clip.humanMotion`，两个都必须是 `True`。

### 6.0b 贴图是内嵌在 FBX 里的，要手动提取

模型进来是纯白的，**不是漏配材质，是贴图还锁在 FBX 里**。Unity 在你手动提取之前不会把内嵌媒体暴露成子资产——所以"查子资产列表发现没有贴图"证明不了贴图不存在。

选中 FBX → Inspector → **Materials** 标签 → **Extract Textures** → 存到 `Assets/MotionCaptures/Textures/`。`Color` 和 `Normal` 会自动接上。

提取后还要修两处导入设置：`Normal.png` 要改成 **NormalMap** 类型；metallic / roughness 要**关掉 sRGB**（它们是数据不是颜色，走 gamma 解码会错）。

> **metallic / roughness 目前刻意没接。** URP 的 `_MetallicGlossMap` 要求金属度在 R、光滑度在 A **同一张图**里；Tripo 导出的是两张独立贴图，而且给的是 roughness（smoothness 的反值）。直接把 metallic 那张拖上去，它不透明的 alpha 会被当成 smoothness=1，整个角色变镜面。要接得先做通道打包（metallic → R，1−roughness → A）。在那之前材质是纯哑光。

### 6.1 ⚠️ 三个人必须各自持有一个 playable

> **共用同一个 `AnimationClipPlayable`、接三个 `AnimationPlayableOutput` 是不行的，而且它不报错。**

看起来更漂亮的写法是：一个 clip playable，三个 output 都 `SetSourcePlayable` 指向它——同步就成了结构上的必然。**实测只有第一个角色会动**，另外两个停在 bind pose，控制台干干净净。

所以现在是**每人一个 playable，每帧喂同一个时间值**。共享的是**时间**，不是 playable：

```csharp
for (int i = 0; i < clipPlayables.Length; i++) clipPlayables[i].SetTime(t);
graph.Evaluate();
```

实测三人 `LeftForeArm` 与 `Hips.y` 逐位相同。

### 6.2 PlayableGraph 必须手动销毁

`PlayableGraph` 不走 GC。`OnDisable` / `OnDestroy` 里不 `Destroy()` 就会泄漏，并且继续往它绑定过的 `Animator` 上写。

### 6.3 舞者不跟 take 对齐

角色 clip 29.93 s、take 48.67 s，长度对不上，所以中控用自己的时钟独立循环（实测 take 在 11.95 s 时舞者在 18.34 s）。**三人彼此严格同步，但整体不跟 take 同步。** 要对齐得先决定用哪种方式：裁剪 clip、变速、还是让中控跟着 `DancePlayer.PlayheadSeconds` 走。

## 7. 可调变量表

| 变量 | 位置 | 默认值 |
|---|---|---|
| `spawnInterval` | `BeatSpawner` | 1.5 s |
| `startDelay` | `BeatSpawner` | 1.5 s（见 §2.4） |
| `ringLeadTime` | `BeatTargetConfig` | 1.2 s，**同时决定采样提前量** |
| `sphereRadius` | `BeatTargetConfig` | 0.12 m |
| `loop` | `DanceRecordingBeatSource` | true，take 放完从头开始 |
| `placeLeft` / `placeRight` | `DanceRecordingBeatSource` | 都开 |
| `maxLevel` | `BeatComboTrail` | 5 |
| `perfectGain` / `goodGain` / `missPenalty` | `BeatComboTrail` | +2 / +1 / −1 |
| `maxTrailSeconds` | `BeatComboTrail` | 0.8 s |
| `lowLevelDullness` | `BeatComboTrail` | 0.85 |
| `recalibrateHoldSeconds` | `PlayModeController` | 1 s |
| `startMode` | `PlayModeController` | Beat |
| `loop` | `DanceCharacterDirector` | true，角色 clip 放完从头开始 |
| `dancers` | `DanceCharacterDirector` | 3 个 Animator |

## 8. 已知问题

**8.1 原始动作数据不等于好谱面。** 实测整段 take 里手到头部的距离在 **0.18–1.09 m** 之间，而正常臂展约 0.3–0.8 m：

- 0.18 m → 球生成在离脸不到 20 公分处，既难打也不舒服
- 1.09 m → 超出手臂能及范围，不迈步够不着

**目前没有距离过滤，是明确的选择**。要加的话，做法是只在 0.3–0.75 m 之间的采样点生成，之外跳过这一拍或把位置钳制到球壳上。

**8.2 扣分速率可能偏严。** 每 1.5 秒生成 2 颗球，漏掉就是每 1.5 秒 −2；而从 0 升到 5 需要 3 次 Perfect。漏几拍拖尾就清空了。符合规则，但手感要实测。

**8.3 节拍不来自音乐。** `Dance_1984Dancinginstreets` 现在挂了视频（带 AAC 音轨，`audioOutputMode = Direct`），所以场景里有声音了；但 `music` 仍是 none，而且**生成节拍的依据仍然只是定时器**，跟音乐没有任何关系。另外两段 take 仍然无音无视频。

**8.4 全部未在头显验证。** 模式切换、清理、UI、combo 等级都只通过组件状态和真实判定在编辑器里验证过。

## 9. 明确不在本规格范围内

- **从录制数据提取节奏**。时间仍然是固定间隔的定时器，只有位置来自录制。这是设计文档 2.2/3 的核心机制，仍未实现，也是整个设计里最大的未知数。
- **距离过滤**（见 §8.1）。
- **计分与结算**。`DanceFollowScore` 只统计 guide 模式的跟随率；beat 模式除了 combo 等级之外没有分数。
