---
状态: v1（3.1/3.2 规则已确认，2026-08-20）
日期: 2026-08-20
关联文档: "Idea：RH Community Hack — VR节奏音游交互范式（Ring-Sphere + 真人录制映射）.md"（同目录，高层概念）
---

# Ring-Sphere 交互判定与美术规格

这份文档是对设计文档 2.1 节"单个 beat 的呈现"的展开，聚焦在**单个 beat 的命中判定逻辑和视觉呈现**上，是给程序和策划的实现规格，不涉及"如何从真人录制生成关卡"（那部分见设计文档 2.2/3）。

## 1. 判定状态机

```
Spawned → Approaching → 触碰发生 → Resolved(Perfect / Good / Miss-Touch)
                       ↘ 到期无触碰 → Expired → Miss-Timeout
```

- **Spawned**：球体 + ring 在指定时间 `T_spawn`、指定位置生成。Ring 初始半径 = `R_ring_start`，球体半径固定 = `R_sphere`。
- **Approaching**：ring 半径随时间从 `R_ring_start` 收缩到 `R_sphere`，收缩曲线由 `Ring Shrink Curve` 控制。理论上在 `T_perfect` 时刻 ring 半径 == `R_sphere`。
- 玩家手柄进入球体判定体积（碰撞体，可以略大于视觉球体半径，见变量表）的瞬间记为触碰时刻 `t`：
  - `t` 落在 `[T_perfect - PerfectWindow, T_perfect + PerfectWindow]` → **Perfect**，大范围粒子特效 + 强震动 + Perfect 音效，进入 Resolved。
  - `t` 落在 Good Window 内但不在 Perfect Window 内（早/晚窗口可不对称）→ **Good**，小范围粒子特效 + 中等震动 + Good 音效，进入 Resolved。
  - `t` 在 Good Window 之外但判定体积仍激活（早太多或晚太多，但还没到 `Expire`）→ **Miss-Touch**（见下），进入 Resolved。
  - `t` 晚于 `Expire` 时刻（判定体积已关闭）→ **不响应**。判定体积必须在 `Expire` 时刻当帧立即禁用，与"开始播放 Miss-Timeout 消失动画"同一帧发生，杜绝消失动画播放期间还能意外触发判定。
- **Expired**：到 `T_perfect + GoodWindow(晚)` 之后仍未 Resolved（完全没有任何触碰）→ 判定体积立即禁用，同一帧开始播放 **Miss-Timeout** 收缩消失动画（独立时长 `Vanish Duration`），播放弱反馈（无/轻微 miss 音效，无震动）。

两种"没打好"在视觉上刻意做成相反的动效，方便玩家凭直觉分辨"我碰太糟了"和"我根本没碰到"：

- **Miss-Timeout**（完全没触碰）→ **缩小**消失（跟 ring 收缩的视觉语言呼应）。
- **Miss-Touch**（有触碰，但时机在 Good Window 之外）→ **放大 + 透明度降低**直至消失（跟 Perfect/Good 的"炸裂"感区分开，是"膨胀消散"而不是爆炸）。

## 2. 判定等级与反馈对照表

| 判定等级 | 触发条件 | 视觉反馈 | 音效 | 震动 |
|---|---|---|---|---|
| Perfect | `\|t - T_perfect\| ≤ PerfectWindow` | 大范围炸裂粒子特效 | Perfect 音效 | 强 |
| Good | `PerfectWindow < \|t - T_perfect\| ≤ GoodWindow` | 小范围炸裂粒子特效 | Good 音效 | 中 |
| Miss-Touch（触碰但脱靶） | 触碰了，但 `\|t - T_perfect\|` 超出 GoodWindow（早或晚） | 球体放大 + 透明度降低至消失（无炸裂粒子） | 待定，推荐给一个比 Perfect/Good 弱、但比纯静音明显的负反馈音（下沉/泄气感） | 待定，推荐轻微/短促，跟 Perfect/Good 的"强/中"区分开 |
| Miss-Timeout（完全未触碰，超时） | 到 Expire 仍未有任何触碰 | 球体 + ring 收缩至消失（无炸裂） | 弱/无 | 无 |

`Miss-Touch` 的音效/震动强度目前未定，先按"比 Miss-Timeout 明显、比 Good 弱"的原则给默认值，实际数值留给策划在变量表里调（见第 6 节新增行）。

## 3. 规则边界（2026-08-20 已确认）

### 3.1 早/晚触碰但超出 Good Window，算什么？
**已确认：不是"不响应"，而是独立的第三种判定 `Miss-Touch`**——球体放大、透明度降低直至消失，视觉上明确区别于 Perfect/Good 的炸裂和 Miss-Timeout 的缩小消失。（此前草案推荐的"选项 A：不响应"未采用。）

### 3.2 判定窗口关闭后（Expired 播放消失动画期间），手柄还能不能碰到球？
**已确认：不能。** 判定体积禁用和"开始播放 Miss-Timeout 消失动画"必须同一帧发生。

## 4. Ring 渲染方案（已定）

Ring 做成 **billboard**（始终朝向玩家的平面圆盘/圆环），不做真实 3D torus。球体保持真实 3D 网格（Fresnel 边缘发光在任意角度都成立）。理由见 [../.ai/decisions/ring-art-direction.md](../.ai/decisions/ring-art-direction.md)。

## 5. 美术方向（已定：方案 A ——霓虹能量脉冲）

- 球体：Fresnel 边缘发光 + emission。
- Ring：径向渐变发光圆环 + additive blending。
- 判定等级用发光颜色区分：Perfect = 金/白，Good = 蓝，超时 Miss = 灰暗消散。

方案 B（卡通糖果泡泡）、方案 C（粒子原生 ring）已讨论但暂不采用，理由见 [../.ai/decisions/ring-art-direction.md](../.ai/decisions/ring-art-direction.md)。

## 6. 策划可调变量表

建议做成 ScriptableObject 配置（每个难度/曲风一份），而不是写死在 prefab 上。

| 分类 | 变量 | 说明 |
|---|---|---|
| 时间 | Ring 提前量（lead time） | 球生成到"完美时刻"的时长，按 BPM/难度分档 |
| 时间 | Perfect 判定窗口（±ms） | |
| 时间 | Good 判定窗口（早/晚可不对称） | |
| 时间 | 超时时长 | 生成后多久没命中开始收缩消失 |
| 时间 | Miss-Timeout 消失动画时长 + 曲线 | 独立于超时判定本身（缩小消失） |
| 时间 | Miss-Touch 消失动画时长 + 曲线 | 独立控制（放大+淡出消失），可以跟 Miss-Timeout 时长不同 |
| 视觉 | Ring 初始半径 / 球体半径 | 直接影响难度和"够得着"的手感 |
| 视觉 | Ring 收缩曲线（AnimationCurve） | 线性 vs 缓动 |
| 视觉 | Miss-Touch 放大倍率 | 球体膨胀到视觉半径的多少倍再完全透明 |
| 视觉 | 各判定等级的粒子特效 prefab | 不硬编码，方便换皮肤（Miss-Touch/Miss-Timeout 无粒子，只有 Perfect/Good 需要） |
| 视觉 | 命中判定体积 vs 视觉球体大小 | 判定体积可比视觉球略大，做宽容度 |
| 反馈 | 各判定等级音效（含 Miss-Touch，数值待定） | |
| 反馈 | 各判定等级手柄震动强度/时长（含 Miss-Touch，数值待定） | |
| 规则 | 允许触发的手柄（左/右/双手皆可） | 为双手/多球并行谱面留接口 |

## 7. 明确不在本规格范围内（需要单独设计）

- 分数/连击（combo）系统如何消费判定等级
- 双手/多球并行谱面的判定冲突规则
- 从真人录制轨迹自动提取节拍的算法（设计文档里最大的未解决问题）

## 8. 开发测试模式（非 VR，2026-08-20 起）

第一阶段先不接 VR/头显做测试，用键盘模拟输入，只验证**判定时间窗和反馈是否符合预期**，不测试"伸手够球"的空间维度。

- **E 键**：生成一个测试球（含 ring），出现在摄像机前方固定位置，使用一份默认策划变量集。
- **空格键**：模拟"手柄触碰"——立即以按键时刻作为触碰时刻 `t`，对当前存活（未 Resolved/Expired）的测试球走正常判定流程（Perfect/Good/Miss-Touch）。若同时有多个未结算的球，先命中最早生成的那个。

**架构上的硬要求（呼应 [.ai/decisions/modular-portable-interaction.md](../.ai/decisions/modular-portable-interaction.md)）**：键盘模拟必须通过一个独立的"测试输入适配器"脚本调用和真实 VR controller collider **完全相同**的公开触发接口（例如 `BeatTarget.TryTouch(t)`），核心判定逻辑里不允许为键盘输入单独开分支。这个适配器脚本本身不算进"可迁移到别的项目"的核心 prefab 范围内，是编辑器/测试专用的外挂脚本，将来接入真实 VR controller 时只需要换输入源（collider 触发替代按键），不用碰判定逻辑。

**局限（刻意接受）**：这个模式不测空间位置判定（ring 视觉大小感知、伸手距离手感），只测时间轴判定和反馈节奏。空间维度的手感验证要等接入真实 VR controller/XR Rig 之后再做。
