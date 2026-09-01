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

**状态机**：`Idle →（挂了视频时）PreparingVideo → CountingDown（3s）→ Recording → Idle`。倒计时期间**不采样**，时间轴从倒计时结束那一刻算起（实测首帧 t≈0.008s）。

- 按键绑定是**代码里创建的独立 InputAction**，不写进共享的 XRI action 资产——这个开发工具不应该有能力干扰游戏本体依赖的输入映射。
- ⚠️ **键盘 X 是桌面调试用的便利绑定，在编辑器里容易误触**：只要 Game 视图有焦点，敲到的任意一个 `x` 都会开始录制。开发期间曾因此产生过一份 7 秒的意外录制。头显上不存在这个问题（那边是手柄 X 键）。如果觉得烦，去掉 `<Keyboard>/x` 绑定或改成组合键即可。
- 停止时自动保存为资产到 `Assets/DanceRecordings/`，**文件名带时间戳**：`Dance_2026-08-26_16-20-40.asset`。这样多次录制不会互相覆盖，文件名本身也说明了录制时间。`GenerateUniqueAssetPath` 作为兜底，处理同一秒内完成两次录制的极端情况。
- 开始录制时会自动 `Stop()` 回放器（`playerToStop` 引用），避免循环预览的音乐盖在正在录制的音乐上。
- UI 全英文，世界空间画布挂在头部下方，始终在视野内：
  - 录制待机：`● RECORD MODE` + `Press X to start recording` +（挂了音乐/视频时）`(video + music will play)` 等提示
  - 缓冲视频：`BUFFERING VIDEO...` + `The countdown starts once the video is ready`（黄色）
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

## 4c. 视频（可选）

场景里有一块 `Video Screen`（世界空间的 16:9 quad，位于 `(0, 1.6, 2.5)`，面朝 rig），挂着 `VideoPlayer`，渲染到 `Assets/DanceCapture/VideoRenderTexture.renderTexture`，quad 用一个 Unlit 材质采样这张 RT。它**不是**挂在头部下面的——是房间里的一块屏幕，不会跟着视线转。

用法和音乐对称：把 `VideoClip` 拖到 `DanceRecorder` 的 `Video (Optional) → Video Clip`，录制时播放，视频引用写进录制资产，回放时自动重播。

- **音画同步**由 `VideoPlayer` 自己保证：`audioOutputMode = Direct`，不走额外的 AudioSource，省掉"让 AudioSource 跟上解码器"这类同步问题。
- ⚠️ **同时挂了音乐和带声音的视频会同时出声**，两条音轨叠在一起。要么只用其中一个，要么用没有声音的视频。

### 视频的生命周期：`DanceVideoScreen` 与"永不 Stop"

`VideoPlayer` **没有 `PlayScheduled`**，无法像 `AudioSource` 那样对着 dsp 时钟精确调度；而且未缓冲时 `Play()` 会静默等待解码器。更要命的是这台开发机上实测：

```
空场景，1920x1080 H.264，playOnAwake，无任何项目脚本
t+0.0s  ~ t+18.0s   time=0.00  frame=-1   （isPrepared 早已为 true）
t+18.08s            *** 首帧送出 ***
t+20→28s            time 1.80 → 3.80 → 5.80 → 7.80 → 9.80   完全实时，零漂移
```

**解码器启动要 ~18 秒，之后播放精确实时。** 这一条解释了此前所有"只有第一帧、没有声音"的现象：录制和回放的旧代码每开一遍都 `Stop()` + `Prepare()`，而一段 12.5 秒的 take 循环一次就把解码器推倒重来——18 秒的启动永远走不完，画面自然永远停在第一帧。

所以现在把 `VideoPlayer` 的生命周期收进一个单独的组件 `DanceVideoScreen`，它只有一条铁律：

> **绝不调用 `VideoPlayer.Stop()`。** Stop 会丢掉已缓冲的解码器状态，等于重付一次 18 秒。需要"停"的地方一律用 `Pause()` + seek 回原位（`Park()`）。

它的接口很小：

| 方法 | 作用 |
|---|---|
| `WarmUp(clip)` | 装载并开始缓冲，首帧送出后暂停并停在 0，随时可秒起 |
| `IsReadyFor(clip)` | **就绪判据是 `frame >= 0`（真的送出过画面），不是 `isPrepared`** |
| `CueTo(t)` | seek 到指定时间并保持暂停，用 `seekCompleted` 确认落位 |
| `Resume()` | 从当前位置起播 |
| `Park()` | 暂停 + 回到 0 |

`DanceVideoScreen.For(videoPlayer)` 会在需要时自动挂到 `VideoPlayer` 所在物体上，**不需要在场景里手工连线**。

时序上因此有三处改动：

1. **场景加载时就开始暖机**（`Warm Up Video On Load`，默认开）。那 18 秒花在戴头显、走位的准备时间里，等真正按 X 时通常已经就绪。
2. **`PreparingVideo` 等的是 `IsReadyFor`**，不再是 `isPrepared`——后者在首帧出现前 18 秒就为 true，拿它当发令枪正是画面卡在第一帧的直接原因。UI 上 `BUFFERING VIDEO...` 现在带一个秒数，免得长时间缓冲看起来像卡死。
3. **循环不再重建解码器**。`DancePlayer.Restart Video Each Loop`（默认开）在每遍开头 seek 回 in-point；如果 take 比视频短很多、频繁 seek 有代价，可以关掉它让视频连续播下去。

> **⚠️ 视频与动作的同步精度尚未在真机上验证。** 音乐那条链路实测漂移只有 13 微秒（因为 `PlayScheduled` 走 dsp 时钟），但 `VideoPlayer` 的时钟跟随**渲染循环**，与 dsp 时钟不是一回事。请戴上头显实际录一段带视频的，确认动作和画面对得上；如果发现有稳定偏移，可以在录制资产里额外记录视频起始偏移量来补偿。

> **⚠️ 18 秒启动是这台机器上的实测值，不是通用值。** 别的机器上可能快得多。排查时先跑 `Assets/Scenes/MinimalVideoTest.unity`（空场景 + 一个 VideoPlayer），它是专门留下来的对照器材。

### ✅ 2026-08-29：长启动已经有解了——导入时转码成 VP8

那个“18 秒”根本不是个固定值：同一台机器上又量到过 **55.65 秒**。真正慢的是 **Unity 走操作系统的 H.264 解码器**（Windows 上是 Media Foundation）。

**把导入设置改成转码为 VP8，首帧从 55.65 秒降到 1.76 秒。** 转码后 Unity 用自带的解码器，完全绕开系统那条路。

> **以后新加的视频都要这么设。** 选中 `.mp4` → Inspector → 勾上 **Transcode** → `Codec` 选 **VP8** → Apply。这个设置存在 `.meta` 里，会跟着资产走，协作者不需要知道这件事。第一次导入会多花几十秒转码，一次性成本。

另外两件当时没搞清楚、现在确认了的事：

- **不存在渲染器问题。** 把 RenderTexture 读回 Texture2D 算平均亮度，一开始解码亮度就在变——RT、材质、quad 这条链一直都是好的。
- **锁 DX11 没用。** 这次确认编辑器确实跑在 `Direct3D11` 上，延迟依然是 55.65 秒。`ProjectSettings.asset` 里那个未提交的 DX11 固定不解决任何问题。

> **坑：黑屏不等于没渲染。** 第一轮量到 `frame=0` + 亮度全 0，看着像“解码了但画不出来”，实际上是这段视频开头本来就是黑的。下结论前先看一眼片子开头长什么样。

**已知限制**：保存用的是 `AssetDatabase`，**仅在 Unity 编辑器内可用**。需要在一体机上独立录制的话，得另外写 JSON/二进制的读写路径（代码里已标注）。目前的预期用法是 Link / Air Link 连编辑器录制。

> **2026-08-29 起另有 `PlayScene`**：`DanceCaptureScene` 的副本，去掉录制，把判定球和引导球两个模式放在一起供玩家二选一。本场景**仍然是唯一的录制入口**，保持原样。见 [PlayScene 双模式与节奏谱生成规格.md](PlayScene%20%E5%8F%8C%E6%A8%A1%E5%BC%8F%E4%B8%8E%E8%8A%82%E5%A5%8F%E8%B0%B1%E7%94%9F%E6%88%90%E8%A7%84%E6%A0%BC.md)。

## 4d. 角色动画（可选）

`DanceRecording.characterAnimation` 挂一条 `AnimationClip`，是同一段表演的**骨骼版本**，用来驱动场景里的舞者模型。留空就没有舞者动作，其余一切照常。

> **2026-09-01：`DanceRecording` 上多了一段 "Stage presentation"**（`poster`、抠像五项、`domeMaterial` / `domeColor`）。那些字段跟录制无关，录制器不写也不读它们——放在这里是因为 PlayScene 的舞台是 prefab，**一个 take 就是一处舞台的全部差异**。见 "Dance Place 三处舞台与就位判定规格" §2.1。

它和 `samples`（手柄轨迹）是**两套独立的数据**，不要当成一件东西：

| | 来源 | 驱动什么 | 参考系 |
|---|---|---|---|
| `samples` | VR 手柄录制 | 引导球、节拍位置 | 录制时的头部 |
| `characterAnimation` | 外部动捉 FBX | 舞者模型骨骼 | 模型自己的根节点 |

两者**长度不一定相等**，也没有任何机制保证它们对拍。具体怎么用见
[PlayScene 双模式与节奏谱生成规格.md](PlayScene%20双模式与节奏谱生成规格.md) §6。

## 5. 回放

- `DancePlayer` 按 dspTime 采样录制数据，**位置 Lerp、旋转 Slerp**，所以回放帧率和录制帧率无关。
- 每只手挂一个代理。**2026-08-29 起代理换成了 Guide Orb**（发光球 + 粒子拖尾，见 [Guide Orb 跟随引导球规格.md](Guide%20Orb%20%E8%B7%9F%E9%9A%8F%E5%BC%95%E5%AF%BC%E7%90%83%E8%A7%84%E6%A0%BC.md)）；原来的 box 代理已于 2026-08-29 **删除**。
- 另有两条 `LineRenderer`（`Left Path` / `Right Path`）。**2026-08-29 起它们不再由 `DancePlayer` 绘制**，改由 `GuideOrb` 画**玩家手柄自己的轨迹**——手进入球时开始记录，离开 1 秒后才停止。球已经把录制轨迹演出来了，线画玩家的手才是新信息。详见 [Guide Orb 跟随引导球规格.md](Guide%20Orb%20%E8%B7%9F%E9%9A%8F%E5%BC%95%E5%AF%BC%E7%90%83%E8%A7%84%E6%A0%BC.md) §6b。`DancePlayer` 里画录制路径的那套代码（`leftPath`/`rightPath`/`BuildPathWindow` 等）已整个删除，避免两个组件抢写同一条线。
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
