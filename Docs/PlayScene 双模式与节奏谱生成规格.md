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

场景里有 `Beat Mode` 和 `Guide Mode` 两个空物体作为分组根，切换就是每侧一次 `SetActive`。两个组都保持 identity（位置 0、旋转 0、缩放 1）——`GuideOrb` 把 `orbRadius` 当**世界**半径，父节点带缩放会让它报警。

命中体积（`Beat Hit Volume`）是手柄的子物体，没法分进组里，由控制器单独开关。

### 2.2 `PlayModeController` 必须挂在不会被停用的物体上

> **这是硬约束。** 自我停用的组件永远无法把自己重新启用。项目为此已经栽过一次——`DanceCaptureModeController` 当初就是为了解决同一个问题才存在的。

它挂在 `Play Controller` 上，那个物体在任何模式下都不会被关掉。

### 2.3 切换时必须清理的四件事

每一件都是"所有者被关掉之后仍然活着"的状态：

| 清理 | 不做的后果 |
|---|---|
| 销毁在场的 `BeatTarget` | 它们继续跑自己的状态机，在模式已经切走之后逐个判成 Miss-Timeout |
| `BeatSpawner.StopSpawning()` | 定时器继续跑 |
| `GuideOrb.ClearTrail()` ×2 | world-space 粒子**不会**因为发射体被隐藏而消失，会挂在空中 |
| **禁用 `DancePlayer` 组件本身** | 它自带长按 B 的 InputAction，留着启用会在 beat 模式中途重锚并重启播放 |

最后一条容易漏：只关引导球是不够的，`DancePlayer` 还活着就会被 B 键唤醒。

### 2.4 `BeatSpawner.startDelay` 的含义变了

`StartSpawning()` 在**每次切换**都会重新套用 `startDelay`。原来的 5 秒是为"按 Play 然后戴头显"准备的，但 beat 模式从来不是启动模式，那 5 秒只会出现在主动切换时——切过去然后五秒钟什么都不发生，读起来像坏了。已改成 **1.5s**（一个生成间隔）。

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

## 6. 可调变量表

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
| `startMode` | `PlayModeController` | Guide |

## 7. 已知问题

**7.1 原始动作数据不等于好谱面。** 实测整段 take 里手到头部的距离在 **0.18–1.09 m** 之间，而正常臂展约 0.3–0.8 m：

- 0.18 m → 球生成在离脸不到 20 公分处，既难打也不舒服
- 1.09 m → 超出手臂能及范围，不迈步够不着

**目前没有距离过滤，是明确的选择**。要加的话，做法是只在 0.3–0.75 m 之间的采样点生成，之外跳过这一拍或把位置钳制到球壳上。

**7.2 扣分速率可能偏严。** 每 1.5 秒生成 2 颗球，漏掉就是每 1.5 秒 −2；而从 0 升到 5 需要 3 次 Perfect。漏几拍拖尾就清空了。符合规则，但手感要实测。

**7.3 没有音轨。** 现有三段 take 的 `music` 和 `video` 都是 none。音游没有音乐，节拍就只是个定时器。

**7.4 全部未在头显验证。** 模式切换、清理、UI、combo 等级都只通过组件状态和真实判定在编辑器里验证过。

## 8. 明确不在本规格范围内

- **从录制数据提取节奏**。时间仍然是固定间隔的定时器，只有位置来自录制。这是设计文档 2.2/3 的核心机制，仍未实现，也是整个设计里最大的未知数。
- **距离过滤**（见 §7.1）。
- **计分与结算**。`DanceFollowScore` 只统计 guide 模式的跟随率；beat 模式除了 combo 等级之外没有分数。
