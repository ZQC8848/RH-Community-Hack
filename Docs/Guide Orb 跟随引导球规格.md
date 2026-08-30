---
状态: v1（已实现并接入 DanceCaptureScene，2026-08-29）
日期: 2026-08-29
关联文档: "Dance Capture 录制与回放规格.md"（同目录，录制与回放）；"Ring-Sphere 交互判定与美术规格.md"（同目录，判定球——和本文档是两种不同的东西，见 §1）
---

# Guide Orb 跟随引导球规格

两颗球沿着录制好的手柄轨迹自动移动，一路发射粒子拖出 trail。玩家把手柄伸进球里时，球的表面浮现涟漪、体积涨大、粒子变艳。

用途是**引导和教学**：让玩家看见"这段舞该怎么动"，并且用身体确认自己跟上了。

## 1. 它不是 BeatTarget

最容易做错的一件事，是把它当成 [BeatTarget](../Assets/Scripts/Interaction/BeatTarget.cs) 的一个变体来实现。两者的时间模型根本不同：

| | BeatTarget（判定球） | GuideOrb（引导球） |
|---|---|---|
| 时间模型 | 围绕**一个时刻** `T_perfect` | **持续存在**，没有特殊时刻 |
| 生命周期 | Spawn → Approaching → Resolved/Expired → 销毁 | 跟着整段 take 存在，随 take 循环 |
| 玩家状态 | 二值：碰了 / 没碰 | 连续：手离球心多近 |
| 结果 | Perfect / Good / Miss-Touch / Miss-Timeout | 无判定结果，只有跟随率统计 |
| 失败形态 | 有（Miss） | **没有**。跟不上就是没点亮，不惩罚 |

`BeatTarget` 的每个字段都在回答"现在离那一刻还有多远"。硬套过来会得到一个所有时序字段都填废值的怪物。

**复用的边界划在美术层，不在逻辑层**：共用霓虹能量脉冲的方向、共用"右手偏冷 / 左手偏暖"的配色关系；但**色相和粒子材质都是独立的一套**（见 §3.3），状态机也全新写，而且小得多。理由见 [../.ai/decisions/guide-orb-not-a-beat-target.md](../.ai/decisions/guide-orb-not-a-beat-target.md)。

## 2. 分层：球不知道自己在跟一段录制数据

```
DancePlayer          （已有，DanceCapture/）  dsp 时钟、参考系、采样，写 proxy 的 world pose
  └─ GuideOrb        （新，Interaction/）      挂在 proxy 上。只知道"手离我多近"
       ├─ 视觉        scale / 材质 / 粒子
       └─ HandTrail  （新，Interaction/）      画玩家手部轨迹，挂在 LineRenderer 那个物体上
DanceFollowScore     （新，DanceCapture/）     订阅两颗球的 IsFollowing，累计跟随率
```

`HandTrail` 同样不知道球、手、录制数据的存在——驱动方每帧调一次 `Track(position, gateOpen)`，它自己负责缓冲、老化、渲染。这让 `GuideOrb` 不必在"判定接触"之外再长出第二份工作。

`GuideOrb` **完全不知道有"录制数据"这回事**——它只需要自己的 transform 和两个手部 Transform。这意味着它能挂在任何会动的东西上（沿样条走的物体、跟随 NPC 的能量球……），符合 [modular-portable-interaction](../.ai/decisions/modular-portable-interaction.md) 那条既定原则。

"一段 take 多长""现在播到第几遍"这类知识全部留在 `DanceFollowScore` 里，它才是那个绑死在录制系统上的组件。

**`DancePlayer` 需要一处改动**：加一个 `OnPassStarted` 事件。**循环走的是 `StartPass`**，而 `Play()` 只在最开始调用一次，所以没有它，跟随率没法在循环边界清零。

### 2.1 场景接线（DanceCaptureScene）

两颗球是 **top-level 物体**，`DancePlayer` 的 `leftProxy` / `rightProxy` 指向它们。

**它们不能挂在原来的 box proxy 底下**——那两个 box 的 localScale 是 **0.08**，挂上去球会缩到十二分之一，而检测半径不会跟着缩，两者当场脱节（正是 §9.1 那个坑）。`GuideOrb` 在 `Update` 里检查 `lossyScale`，偏离 1 会打一次警告。

原来的 box proxy 已于 2026-08-29 **删除**（确认不会切回）。它们身上还挂着 `TrailRenderer`——连同粒子拖尾和手部轨迹线，场景里本来有三套拖尾概念，其中一套已经没有任何角色。

`DanceFollowScore` 挂在 `Dance Capture` 物体上（和 `DancePlayer` 同一个）。

## 3. 激活判定：固定半径的阈值判定

### 3.1 为什么用距离而不是 trigger collider

现有的 [HandTouchSource](../Assets/Scripts/Interaction/HandTouchSource.cs) 走 `OnTriggerEnter`，那是**进入事件**；这里要的是**持续的 inside/outside 状态**，语义不同。

2 球 × 2 手 = 每帧 4 次距离比较（平方距离，不开根号），开销可忽略，还省掉了 Rigidbody / isTrigger 那套配置陷阱。

### 3.2 判定是二值的，半径是固定的

```
inside = distance(hand, orbCenter) <= ActivationRadius
ActivationRadius = orbRadius × scaleExcited        // 球最大时的半径，0.15 × 1.35 = 0.2025 m
```

**两条都很关键：**

1. **进出是阈值，不是渐变。** 小于这个半径就是进入，大于就是退出，中间没有"插得更深所以更亮"。
2. **这个半径恒定，不随球当前大小变化。** 球待机时会缩小，但玩家要瞄的东西**不能跟着缩**——否则判定范围会随着自己的视觉反馈一起漂移，越接近越难瞄准。

> ⚠️ **由此产生一个耦合：`scaleExcited` 现在是玩法参数，不只是美术参数。** 改它会移动判定边界。这在代码里标了注释，改之前请知情。

**每只手各判一次，取 max**：

- 用**对的手** → 目标 excite 1.0
- 用**错的手** → 目标封顶在 `wrongHandExciteCap`（默认 0.35）

取 max 是唯一不会出现"用对了手反而变暗"的规则（两只手同时在同一颗球里时）。

**平滑只作用于视觉，不作用于判定。** `excite` 朝二值目标做时间平滑（attack 0.08s / release 0.25s），让球不是"啪"地跳变；但 `IsFollowing`（进而跟随率）用的是**未平滑的原始布尔值**。

> ⚠️ **单阈值没有迟滞。** 手正好悬在 0.2025 m 边界上会来回抖动。视觉上被平滑吃掉了，但跟随率统计会抖——对一个比率指标来说会被平均掉，影响很小。真成问题再加进出双半径。

### 3.3 手部规则：错手是"被拒绝"，不是"能量不足"

引导球用**自己的一套色相**：**green = 右手 / amber = 左手**。

判定球是 cyan（右）/ magenta（左）。两套刻意不共用色相——**引导球不是可以打的目标**，如果两者长得一样，玩家很可能对着引导球挥拳（这条风险记在 [../.ai/decisions/guide-orb-not-a-beat-target.md](../.ai/decisions/guide-orb-not-a-beat-target.md) 里）。

但**保留了"右手偏冷、左手偏暖"这层关系**（判定球 cyan 冷 / magenta 暖，引导球 green 冷 / amber 暖），所以左右手的映射在两套系统里读起来是一致的，玩家不用记两套规则。

| | 判定球 | 引导球 |
|---|---|---|
| 右手（冷） | cyan `(0.10, 0.50, 0.80)` | green `(0.20, 0.58, 0.18)` |
| 左手（暖） | magenta `(0.55, 0.06, 0.48)` | amber `(0.70, 0.38, 0.05)` |

任意手都能点亮球，但反馈在**质**上不同——否则玩家分不清"我用错手了"和"我插得不够深"，两者看起来都只是"没那么亮"。

| | 对的手 | 错的手 |
|---|---|---|
| 颜色 | 本色加亮 | **去饱和**，往灰白偏（`wrongHandDesaturation`，默认 0.8） |
| 体积 | 涨大 | **不涨**（scale 保持 idle 值） |
| 粒子 | 更密更艳 | 更稀更暗 |
| 计入跟随率 | 是 | **否**（见 §7） |

去饱和是**颜色 lerp，不是着色逻辑**，所以留在 C# 侧算，shader 只认一个 `_Excite` 标量。不需要给 shader 加第二个通道。

## 4. 反馈映射：一个 excite 驱动三件事

三套反馈只有一个真实状态，不会出现"球胀了但粒子没变"的不同步。

| 反馈 | 驱动方式 |
|---|---|
| 体积 | `scale = lerp(scaleIdle, scaleExcited, correctExcite)`，对手才生效。**待机 0.9、满激活 1.35** |
| 表面 | `_RimIntensity` / `_CoreAlpha` / `_Excite` 从 idle 值 lerp 到 excited 值 |
| 粒子 | `startColor` / `startSize` / `rateOverDistance` 同步 lerp |

**待机态也是灰的，不只是错手。** 去饱和度取两者较大值：

```
greyness = max( idleDesaturation × (1 - excite),  wrongness × wrongHandDesaturation )
```

没有手 = 最惰性的状态，所以 `idleDesaturation`（0.9）比 `wrongHandDesaturation`（0.8）**更深**。同时 `rimIntensityIdle` 降到 1.2、`coreAlphaIdle` 降到 0.18——待机的球又灰又暗又小，激活时才亮起来涨大。

取 max 而不是相加，保证**用对的手靠近永远让球变亮，用错的手永远不会**。

**只有手部拖尾线跟随球的颜色，粒子不跟随。** 线由 `GuideOrb.handTrail` 每帧驱动 `startColor`/`endColor`——起点全透明、终点满色，球变灰时线一起变灰。详见 §6b。

**粒子保持自己的颜色**，由粒子系统上 authored 的 `startColor` 决定，运行时不改写——待机变灰的只有球和线。

> `GuideOrb` 只知道"有一条线该跟我同色"，`LineRenderer` 是 Unity 通用类型，不牵扯任何录制系统的知识——模块可迁移性没有被破坏。

材质属性一律走 **`MaterialPropertyBlock`**，不碰 `renderer.material`——后者会为每个实例克隆一份材质。这是项目已有的约定。

## 5. 表面样式：从接触点扩散的涟漪

激活时球面浮现**同心涟漪，波纹从手插进去的那一点往外扩**。手在球里移动，涟漪源头跟着移动。

距离场直接就是 `distance(worldPos, _ContactPoint)`，`frac(d * frequency - time * speed)` 就是一圈圈外扩的波纹。

**为什么不是六边形能量网格**：球面铺网格的两条路都有可见瑕疵——球面 UV 在两极挤压变形（球顶能看到明显收缩），三平面投影有接缝，都得额外花时间遮丑。涟漪的距离场零 UV 问题、零两极瑕疵，而且天然表达了因果关系：波纹是**你手插进去的那一点**长出来的，不是整个球均匀亮起来。完整取舍见 [../.ai/decisions/guide-orb-contact-ripple.md](../.ai/decisions/guide-orb-contact-ripple.md)。

网格是加法，随时能叠在涟漪之上，先做涟漪看效果。

**新写 `Assets/Shaders/GuideOrb.shader`，不改 `BeatSphere.shader`。** 判定球和引导球现在是两种东西，共用一个 shader 会让两边的属性互相拖累。

新增属性：

| 属性 | 作用 |
|---|---|
| `_Excite` | 0..1，总强度，由 C# 每帧写入 |
| `_ContactPoint` | 世界坐标，涟漪的源点。C# 为了算 excite 本来就在算距离，接触点顺手就有 |
| `_RippleFrequency` / `_RippleSpeed` / `_RippleWidth` | 波纹的密度、速度、单条波纹的宽度 |
| `_RippleColor` | HDR，波纹本身的颜色 |

Fresnel 底层沿用 `BeatSphere.shader` 的做法（rim glow + emissive core），涟漪叠在上面。

## 6. Trail 粒子

`ParticleSystem`，`simulationSpace = World`，只走一条发射通道：**`rateOverDistance`（3 → 60 每米，随 `correctExcite`）**。

按距离而不是按时间发射：球停下来时 `rateOverTime` 会在原地堆成一坨；按距离发射让粒子密度跟着**运动速度**走——跳得快的段落 trail 自然更密，那个密度本身就是舞蹈信息，不只是装饰。

待机 3/m 对激活 60/m 是 **20 倍**的反差。粒子本身很小（0.006 → 0.014 m），整体是细碎的星尘感，不是浓密的烟雾。

**已知取舍**：只按距离发射意味着手插进一颗**恰好静止**的球时不会有任何粒子。曾经加过一条按时间发射的通道来补这个洞，**2026-08-29 撤销**——它带来的持续迸发不是想要的观感。如果以后觉得静止时缺反馈，这是已知的补法。

粒子的颜色由粒子系统 authored 的 `startColor` 决定，**运行时不改写**，不跟随球的状态变色（球和路径线会变灰，粒子不会）。

粒子材质是**独立的一份** `Assets/GuideOrbs/_Base/GuideOrbParticle.mat`（从 `HitBurstParticle.mat` 复制），不共用判定球那份——否则改一边会悄悄改到另一边。

生命周期 2.5s，`maxParticles` 500（估算 `60/m × 2m/s × 2.5s ≈ 300`）。上限要跟着速率一起调——留一个远高于实际需求的数，只会掩盖这套粒子真正要多少。Alpha 曲线"先保持、末尾才褪"，尺寸曲线出生微弹后保持在 0.55 不缩到 0——让 alpha 负责消失、尺寸负责存在感，粒子才读作滞留而不是转瞬即逝。

## 6b. 手部拖尾线：画的是玩家的手，不是球

**2026-08-29 起，`Left Path` / `Right Path` 两条 `LineRenderer` 改由 `GuideOrb` 驱动，画的是玩家手柄的实际轨迹**，不再是录制数据里那条球正在走的路径。

理由：球本身已经把录制轨迹演出来了，再画一条同样的线是重复信息。画玩家自己的手，才是那条线唯一能提供的新信息——**你实际划出了什么**。

**生成的门控：**

| 时刻 | 行为 |
|---|---|
| 手进入球（对的手） | 开始逐帧记录手的世界坐标 |
| 手在球内 | 持续记录 |
| 手离开球 | **继续记录 `graceSeconds`（默认 1s）** |
| 宽限期结束 | 停止记录；已有的点按 `pointSeconds` 老化掉，线自己排空 |

宽限期存在的理由：手快速穿过球时，只在"球内"那几帧记录会得到一个点而不是一笔——留 1 秒才画得出一道能看的笔画。

**点的老化**：每个点存活 `pointSeconds`（默认 0.6s）后被丢弃，所以线长有界，不会在长时间激活中无限增长。想让笔画留得更久就调大它。

`minDistance`（0.005 m）避免手不动时在同一点堆积上百个点；`maxPoints`（200）是硬上限。

**这套逻辑住在独立组件 `HandTrail` 里**（挂在 `LineRenderer` 所在物体上），上面四个参数也在它身上，不在 `GuideOrb`。`GuideOrb` 每帧只调一次 `Track(手的世界坐标, 对的手是否在球内)`，外加 `SetColor()`。

`HandTrail` 的工作放在 **`LateUpdate`**：驱动方在自己的 `Update` 里调 `Track`，而组件间的 `Update` 顺序是未定义的，放 `LateUpdate` 才能保证读到的是本帧的值。

> **`DancePlayer` 里原来那套画录制路径的代码已整个删除**（`leftPath` / `rightPath` / `pathTrailSeconds` / `pathResolution` / `BuildPathWindow`）。两个组件同时往一条 `LineRenderer` 写 `positionCount`，谁赢取决于 `Update` 顺序——那是未定义行为，不是可以并存的两个功能。

### 6b.1 材质陷阱：URP/Unlit 会静默忽略 LineRenderer 的颜色

原来两条线用的是 **`Universal Render Pipeline/Unlit`**，而且 Surface 是 **Opaque**。

**`URP/Unlit` 不采样顶点色**，而 `LineRenderer` 的 `startColor`/`endColor`/`colorGradient` 正是以顶点色的形式传下去的。所以之前"让线跟随球的颜色"那次改动**一行代码都没有生效**——C# 侧的值全部写对了，`lr.startColor` 读回来也是对的，但渲染出来始终是纯白；Opaque 还让 alpha 渐隐完全无效。

改成 **`Universal Render Pipeline/Particles/Unlit`**（Transparent + 直通 alpha 混合），它采样顶点色，颜色和渐隐才真正出现。

> **教训**：`LineRenderer` / `TrailRenderer` 的颜色是顶点色。给它配材质前先确认那个 shader 采样顶点色，否则改颜色的代码会**安静地什么也不做**——C# 侧的值全对，这一点特别有欺骗性。

## 7. 跟随率统计

`DanceFollowScore` 负责，**不在 GuideOrb 里**（§2）。

- **什么算"跟上了"**：对的手在判定半径（0.2025 m）以内，就这么简单。判定二值化之后不再有"多深才算"的问题。
- **统计用的是未平滑的原始布尔值**（`GuideOrb.IsFollowing`），不是驱动视觉的那个平滑 `excite`。平滑是为了好看，测量手实际在不在里面不该继承 0.25s 的回落尾巴。
- **只有对手算数**。错手能点亮球，但不计入——否则"用哪只手都行"会退化成"乱挥就行"。
- **左右手各一个比率，另给一个总计**。两只手跟随率差得多本身就是有用的信息。
- 分子 = 满足阈值的帧时长累加（用 `Time.deltaTime` 即可，这里不需要和音频对齐）；分母 = take 的 trimmed duration。
- **每遍开头清零**，靠 `DancePlayer.OnPassStarted`（§2）。

`DanceFollowScore` 还负责在每遍开头调用两颗球的 `ClearTrail()`（§9.2）——它是唯一同时知道"球在哪"和"一遍什么时候开始"的组件。

`DanceFollowScore` 只暴露数值属性，**由谁显示是别人的事**——UI 的事情留给 `DanceCaptureUI`，保持模块边界干净。

## 8. 可调变量表

| 变量 | 默认值 | 说明 |
|---|---|---|
| `orbRadius` | 0.15 | 视觉半径，米。和 `BeatTargetConfig.sphereRadius` 默认值一致 |
| — | — | `activationRadiusMultiplier` 已移除：判定半径固定等于 `orbRadius × scaleExcited`，再乘一个系数会和"等同于球最大半径"这条规则矛盾 |
| `wrongHandExciteCap` | 0.35 | 错手的 excite 上限 |
| `wrongHandDesaturation` | 0.8 | 错手时颜色往灰白偏的程度，0..1 |
| `exciteAttack` | 0.08 s | excite 上升的平滑时间 |
| `exciteRelease` | 0.25 s | excite 回落的平滑时间。刻意比 attack 慢 |
| `scaleIdle` / `scaleExcited` | **0.9** / 1.35 | 体积倍率。待机小于激活半径，见 §9.1。注意脚本里的默认值是 0.5，预制体上是 0.9——预制体的值生效 |
| `idleDesaturation` | 0.9 | 无手时的去饱和度。比错手的 0.8 更深 |
| `rimIntensityIdle` / `Excited` | **1.2** / 6.0 | 沿用 `BeatSphere` 的 dial 区间。待机压暗 |
| `coreAlphaIdle` / `Excited` | **0.18** / 0.55 | 同上 |
| `rippleFrequency` | 12 | 每米几圈波纹 |
| `rippleSpeed` | 2.5 | 波纹外扩速度 |
| `rippleWidth` | 0.15 | 单条波纹的宽度，0..1 |
| `_RippleFalloff` | 0.3 m | 波纹随距接触点的距离衰减。没有它整颗球会一起闪，读作"球在发光"而不是"有东西从接触点扩散"。只在材质上，不在 C# |
| `particleRateIdle` / `Excited` | **3** / **60** 每米 | `rateOverDistance`，拖尾密度。20 倍反差 |
| `particleSizeIdle` / `Excited` | **0.006** / **0.014** m | 单颗粒子直径。相对 0.15m 的球很细碎 |
| `particleLifetime` | 2.5 s | 滞留时间。配合 `maxParticles` 控制上限 |
| `maxParticles` | 500 | 每颗球。上限估算：`每米速率 × 手速 × lifetime`，激活态 60/m × 2m/s × 2.5s ≈ 300。**改速率时必须一起改这个数** |
| — | — | `followThreshold` 已移除：判定二值化后它没有意义，`IsFollowing` 就是"对的手在球内" |

## 9. 两个已经踩过的坑

**9.1 检测半径和视觉半径必须同源——但不等于恒等。**

自 2026-08-29 起球会随状态缩放，所以**可见半径会变，判定半径不会**：

| | 半径 |
|---|---|
| 判定半径（恒定） | **0.2025 m** = `orbRadius × scaleExcited` |
| 可见半径・待机 | `orbRadius × scaleIdle` |
| 可见半径・满激活 | **0.2025 m**，正好等于判定半径 |

**判定半径固定在球最大时的尺寸**：球待机缩小时，玩家要瞄的东西不能跟着缩，否则判定范围会随视觉反馈一起漂移，越靠近越难瞄。

于是待机时球比它的判定范围小——手还没"碰到"那颗小球，它就已经亮起来涨大迎上来，读作"球感应到你在靠近"而不是"你撞到了它"。满激活时两者正好重合。

不变式：**两者都由同一个 `orbRadius` 推出来**，判定额外固定乘 `scaleExcited`，可见的那个乘当前状态系数。绝不能出现"改了视觉大小、忘了改检测"那种两个各自维护的数字。

原来的坑： 项目里栽过一次：ring 半径和 sphere 真实半径差了 4 倍，因为父节点带缩放，local scale 一路乘上去（[记录](../.ai/debug/2026-08-20-ring-radius-wildly-off-from-sphere-radius.md)）。这次两个半径**必须**由同一个序列化字段推出来，根节点保持 scale 1。

**9.2 循环时要显式 `Clear()` 粒子。** `DancePlayer` 在 loop 间隙会 `SetProxiesVisible(false)`，但 **world-space 粒子不会因为物体隐藏而消失**——它们会活到 lifetime 结束。不清的话，下一遍开头会看到上一遍的残留尾巴悬在空中。

## 10. 明确不在本规格范围内

- **不做判定、不做失败**。跟不上就是没点亮，没有 Miss。要节奏判定请用 `BeatTarget`。
- **不做震动反馈**。手柄震动是另一条反馈通道，值得单独设计（强度、时长、和音乐的关系）。
- **跟随率的呈现方式**。本规格只定义怎么算出这个数，不定义它显示在哪、长什么样、要不要影响难度。
- **从录制数据自动生成 beatmap**。那是设计文档 2.2/3 的事，和引导球无关。
