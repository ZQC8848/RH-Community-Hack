---
状态: v1（2026-08-26 实现）
日期: 2026-08-26
关联文档: "Idea：RH Community Hack — VR节奏音游交互范式（Ring-Sphere + 真人录制映射）.md"（同目录，高层概念）
---

# Dance Capture 录制与回放规格

设计文档 2.2 节的核心机制——「让真人戴 VR 跳舞，录下手柄轨迹用来生成关卡」——的第一步实现：**把轨迹录下来、存成资产、能回放看**。

**明确不在本规格范围内**：从轨迹里自动提取节拍（设计文档里最大的未解决问题）。本系统只负责产出干净的时间序列，节拍提取是它的**下游消费者**。

## 1. 参考系：快照一次，然后冻结

这是整个系统最重要的设计决定。

- **录制真正开始的那一刻**（倒计时结束后，不是按下 X 的那一刻），快照一次玩家参考系：原点 = 头部位置，朝向 = 头部前方投影到水平面（只取 yaw）。此后**冻结不变**，不再跟随玩家移动或转头。
- **回放侧只在第一次播放时校准一次**，之后**循环重播不再重新采样**，保证每一遍都落在完全相同的位置，便于横向对比。
- 需要重新锚定时：**长按 B 键 3 秒**（右手柄 `secondaryButton`，键盘 B 同样可用）。重新校准后会从头重播，而不是在乐句中间跳变。UI 上有按住进度条。

**为什么冻结而不是持续跟随**：如果参考系持续跟着头走，跳舞时扭头看一眼旁边，整个坐标系就跟着转了，录出来的动作会被污染。冻结之后，头部旋转彻底变成一个非问题——不需要任何低通滤波或身体朝向估计。

**原点用头部位置（含高度），不是投影到地面**：这样录下来的含义是「距离我的头多远」而不是「离地多高」，在不同身高的人之间迁移性明显更好。

**实现**：`DanceReferenceFrame`（`Assets/Scripts/DanceCapture/`）。已处理垂直朝上/朝下时水平投影退化的边界情况。

## 2. 数据模型

```
DanceRecording (ScriptableObject)
├─ label / capturedDuration / averageSampleRate
├─ samples[]: { time, headPosition, leftPosition, leftRotation, rightPosition, rightRotation }
└─ inPoint / outPoint      ← 裁切，非破坏式
```

- **不记录头部旋转**：参考系已经在开始时固定了身体朝向，跳舞时头看哪里不属于编舞内容。
- **记录头部位置**：用来知道舞者移动了多远，判断一段舞是否适配场地大小。
- **时间戳基于 `AudioSettings.dspTime`**，和判定系统同一个时钟。整个玩法的前提是轨迹要对得上音乐，用 `Time.time` 会漂。
- **采样率上限 `maxSampleRate`（默认 90Hz）**：实际采样率是 `min(帧率, maxSampleRate)`。**这个上限是必须的**——编辑器里不锁帧会跑到 800+ Hz，实测 15 秒录制产生 2.9MB 资产；加上限后 14 秒只有 155KB。头显本身只有 72-120Hz，超出部分纯属浪费。

## 3. 裁切：非破坏式

`inPoint` / `outPoint` 只是**收窄回放范围，永不删除采样帧**。

- 随时可以改回去，也能从同一份录制裁出多个不同片段
- `outPoint <= 0` 表示「播放到结尾」
- **不要通过删除 samples 来「应用」裁切**

按需求确认：**只做裁切，不做变速**。

## 4. 录制流程

| 操作 | 触发 |
|---|---|
| 开始录制（进入 3 秒倒计时） | 左手柄 **X 键**（`<XRController>{LeftHand}/primaryButton`），键盘 **X** 同样可用 |
| 取消倒计时 | 倒计时期间再按一次 **X**（视为误触，不是提前开始） |
| 停止并保存 | 录制中按 **X** |
| 重新校准回放原点 | **长按 B 键 3 秒**（右手柄 `secondaryButton` / 键盘 B） |

**状态机**：`Idle → CountingDown（3s）→ Recording → Idle`。倒计时期间**不采样**，时间轴从倒计时结束那一刻算起（实测首帧 t≈0.008s）。

- 按键绑定是**代码里创建的独立 InputAction**，不写进共享的 XRI action 资产——这个开发工具不应该有能力干扰游戏本体依赖的输入映射。
- ⚠️ **键盘 X 是桌面调试用的便利绑定，在编辑器里容易误触**：只要 Game 视图有焦点，敲到的任意一个 `x` 都会开始录制。开发期间曾因此产生过一份 7 秒的意外录制。头显上不存在这个问题（那边是手柄 X 键）。如果觉得烦，去掉 `<Keyboard>/x` 绑定或改成组合键即可。
- 停止时自动保存为资产到 `Assets/DanceRecordings/`，**文件名带时间戳**：`Dance_2026-08-26_16-20-40.asset`。这样多次录制不会互相覆盖，文件名本身也说明了录制时间。`GenerateUniqueAssetPath` 作为兜底，处理同一秒内完成两次录制的极端情况。
- 开始录制时会自动 `Stop()` 回放器（`playerToStop` 引用），避免循环预览的音乐盖在正在录制的音乐上。
- UI 全英文，世界空间画布挂在头部下方，始终在视野内：
  - 录制待机：`● RECORD MODE` + `Press X to start recording` +（有音乐时）`(music will play)`
  - 回放模式：`▶ PLAY MODE` + 当前播放的名字与进度 + `Clear the Recording field on Dance Player to record again`，**面板底部以分隔线隔开一行页脚**：`Hold B for 3s to recalibrate origin`（蓝色）
  - 长按 B 的提示**只出现在回放模式**：重新校准锚定的是**回放原点**，录制时录制器会在开录那一刻自己快照参考系，这行提示在录制模式下指向的是不存在的东西
  - 倒计时：`GET READY` + 大号秒数 + `Press X to cancel`（黄色）
  - 录制中：`● RECORDING 0:12.4` + 采样数 + `Press X to stop and save`（红色）
  - 保存后：`SAVED <名字>` + 时长/采样率/音乐名，几秒后回到待机
  - 长按 B 时：`RECALIBRATING ORIGIN` + 剩余秒数 + 进度条（绿色）

## 4a. 两种模式：由「回放器上有没有挂数据」决定

`DanceCaptureModeController` 用一条规则区分模式：

| DancePlayer 的 Recording 字段 | 模式 | 行为 |
|---|---|---|
| **空** | `● RECORD MODE` | 录制器启用，X 可用 |
| **有数据** | `▶ PLAY MODE` | **录制器被整个禁用**，自动开始播放该数据 |

- 想回到录制模式：**把 DancePlayer 上的 Recording 字段清空**。UI 上直接写明了这句话。
- 运行中在 Inspector 里赋值/清空都会即时生效，不需要退出播放模式。
- **为什么这个逻辑不放在 DanceRecorder 自己身上**：一个禁用了自己的组件，它的 `Update()` 就不再运行，也就永远没机会在数据被清空时把自己启用回来。所以模式开关必须由一个在两种模式下都持续运行的组件持有。
- `DanceRecorder.StartCountdown()` 里另有一道防护：组件被禁用时直接拒绝并给出提示。否则外部代码调用 `Toggle()` 会让它停在 `CountingDown` 状态而 `Update()` 又不运行，等模式切回来时会突然触发一次陈旧的录制。

## 4b. 音乐（可选）

`DanceRecorder.musicClip` 挂上音频后：

1. 按 X 时，音频用 **`PlayScheduled(倒计时结束的 dspTime)`** 调度，**和录制时间轴用同一个时钟对齐**——实测漂移 **1.3e-05 秒（13 微秒）**。如果改用 `Play()` 在某一帧启动，每份录制都会带上一个未知偏移。
2. 音频引用会一并写进保存的 `DanceRecording` 资产。
3. 回放时自动播放该音频，并按 `inPoint` 偏移起播位置，裁切后音画仍然同步。
4. 不挂音频就留空，录制和回放都静音，不影响任何其他功能。

录制和回放各自使用**独立的 AudioSource**（场景里的 `Recording Music` / `Playback Music`），避免两者争抢同一个 clip 和播放头。

**已知限制**：保存用的是 `AssetDatabase`，**仅在 Unity 编辑器内可用**。需要在一体机上独立录制的话，得另外写 JSON/二进制的读写路径（代码里已标注）。目前的预期用法是 Link / Air Link 连编辑器录制。

## 5. 回放

- `DancePlayer` 按 dspTime 采样录制数据，**位置 Lerp、旋转 Slerp**，所以回放帧率和录制帧率无关。
- 每只手挂一个 box 代理 + `TrailRenderer`（拖尾）。
- 另有 `LineRenderer` 在回放开始时**一次性画出整条路径**——只看 box 移动很难判断动作质量，看到完整路径才能一眼看出走位是否舒展。
- 循环播放：**不重新快照参考系**，每一遍都落在完全相同的位置。循环间隙会隐藏代理，避免拖尾从终点直接连一条直线回起点。
- 切换不同录制：`LoadRecording(recording)`，或直接在 Inspector 里换资产。

## 6. 场景

`Assets/Scenes/DanceCaptureScene.unity`——从 `SampleScene` 复制而来，beat 相关逻辑（`BeatSpawner`、两个测试 harness、手柄上的 `Beat Hit Volume`）全部**禁用而非删除**，保持和主场景的可比性，需要时能直接开回来。

## 7. 架构约束

- **和 `BeatTarget` 完全解耦**：录制/回放不知道 beat 的存在。它是未来节拍生成器的**输入**，不是玩法的一部分。
- **不引用任何 XR 类型**：`DanceRecorder` / `DancePlayer` 只接受普通 `Transform`。换 rig（XRI → Meta XR SDK）不用动核心代码，和 `HandTouchSource` 同一思路。
- `DanceRecording.TrySample(time, out sample)` 这个接口**后面会被节拍提取直接复用**——从轨迹里找节拍点，本质就是在密集采样这个函数。

## 8. 下一步（未实现）

- 从轨迹提取节拍：手柄速度极小值 / 方向骤变，或与音乐 onset 融合
- 身高 / 臂展归一化
- 音乐与录制的对齐：目前录制起止是手动按键，没有和音乐播放绑定
- 一体机上录制（需要 JSON 存储路径）
