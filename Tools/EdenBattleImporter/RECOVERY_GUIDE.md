# Eden 角色战斗技能还原指南

本文记录从原始 APK/XAPK 数据还原一个角色的普通攻击、爆气、奥义和战斗语音的完整方法。
当前已验证的实例是 11301023 弗泽与 11301006 梅伊。

## 1. 先判断哪些内容可以直接复用

一个角色的战斗展示通常由四层数据共同决定，不能只复制战斗 Spine：

1. `m_cardspine_<ID>.aab`：战斗角色 Spine 与动作。
2. `eft_fx_<ID>_attack*`、`skill*`：普通攻击和爆气的粒子、材质、Mesh。
3. `eft_fx_timeline_<ID>_xp.aab`、同名 `.m4v`：奥义 Timeline 与中间插入的视频。
4. `baseskillshowdata`、`skillscript<ID>.lua.bytes`：使用哪个效果、伤害段数、显隐、归位、受击时刻。

语音是第五层独立数据。`character_info.json` 给出语音名，WAV 文件提供音频本体。

## 2. 本项目中的输入和输出位置

现有解析数据：

- 人物解析目录：`/Users/zhuhaiming/Desktop/edenAssets/file/<ID>`
- 原始 ResEx：`/Users/zhuhaiming/Desktop/edenAssetsObb/main.19.jp.co.yoozoo.projectyellow/assets/ResEx`
- 配置表：`/Users/zhuhaiming/Desktop/edenAssetsObb/jp.co.yoozoo.projectyellow/files/Config/TextAsset - 副本`
- 语音源：`/Users/zhuhaiming/Desktop/edenAssets/voice`

项目输出：

- 战斗包：`Assets/StreamingAssets/Skill<ID>Original`
- 奥义视频：`Assets/StreamingAssets/Skill<ID>`
- 战斗语音：`Assets/EdenBattle/Resources/EdenBattle/Voices/<ID>`
- 角色配置目录：`Assets/EdenBattle/Scripts/Characters/EdenBattle<ID>.cs`
- 可复查的原始依据：`Tools/EdenBattleImporter/RecoveredSources/<ID>`

运行所需资源已经复制进项目，所以实际播放不依赖桌面上的解包目录。解包目录只在还原下一个角色时作为输入。

## 3. 每个角色的标准还原步骤

### 3.1 确认战斗 Spine 动作

检查 `battle_metadata/CardSpine_<ID>_SkeletonData.json` 或在 Unity 中读取 SkeletonData，至少确认：

- `idle`
- `attack`
- `skill`
- `uniqueskill`

缺少某一动作时，相应按钮应该无反应，不要借用其他角色动作或特效。

### 3.2 从技能表确定普通攻击与爆气

在 `baseskillshowdata.lua.bytes` 中搜索角色 ID或 `FX_<ID>_`。重点字段：

- `effect_attack`：攻击方/相对目标效果。
- `effect_attack_target`：通常是带 `_2` 的目标侧效果。
- `hurt_section`：用冒号分隔，元素数量就是伤害段数；数值是各段伤害权重，不是毫秒。
- `card_show` 或 `SkillScript<ID>`：奥义会进入独立 Lua/Timeline 流程。

普通攻击和爆气的精确受击毫秒经常不在表内。先按 Spine 动作长度和段数均匀放置，再以原版录像微调；不能把 `hurt_section` 的权重当作时间。

`move_type` / `move_pos_type` 不能单独证明角色一定移动。11301006 的普通攻击和爆气都写着 `move_type=3, move_pos_type=1`，但逐帧录像显示施法者原地不动。最终应以原版录像和实际 Spine 位移为准。

### 3.3 第一次准备原始包

在项目根目录执行：

```bash
node Tools/EdenBattleImporter/prepareSkillBundles.js <ID>
```

脚本会：

- 从每个 AAB 内查找 `UnityFS` 签名并去掉游戏自定义包头。
- 复制已经验证过的公共材质/Prefab 依赖。
- 自动收集角色全部 `eft_fx_<ID>_*`、Timeline、角色 Spine 包。
- 复制大小写完全匹配的奥义 `.m4v`。
- 生成含来源、前缀长度、大小和 SHA-256 的 `manifest.json`。

### 3.4 只做一次基础检查，再自动补依赖

项目根目录的 `.eden-unity-validation` 是持久化检查项目，保留自己的 `Library`，不要放到 `/private/tmp`，也不要每次删除。它通过符号链接读取主项目 `Assets`，并已写入 `.gitignore`。

通用批处理检查命令：

```bash
EDEN_SKILL_CARD_ID=<ID> \
  /Applications/Unity/Hub/Editor/2018.3.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/zhuhaiming/Desktop/unity/unityEden/.eden-unity-validation \
  -executeMethod EdenGallery.Editor.EdenSkillBundleInspector.InspectConfiguredCardBundles \
  -logFile /private/tmp/eden_skill_<ID>_unity.log
```

详细报告位于 `/private/tmp/eden_skill_<ID>_inspect.txt`。如果报告包含 `missing=texture:...` 或 `missing=mesh:...`，运行：

```bash
node Tools/EdenBattleImporter/prepareSkillBundles.js <ID> \
  --missing-report /private/tmp/eden_skill_<ID>_inspect.txt
```

脚本会把资源名映射到 `st_<资源名>.aab` 或 `<资源名>.aab`。随后再运行一次检查，所有核心 Prefab 应为 `missing=none`。正常角色因此只需要一次初始导出、一次基础检查、一次自动补齐和一次快速复查。

如果仍显示 `unresolved`，再手工检查 ResEx 中的实际包名，并只把确认过的公共包加入 `prepareSkillBundles.js` 的 `extraDependencies`，不要把整个 ResEx 全复制进项目。

### 3.5 按 Lua 还原奥义，而不是猜时间

打开 `script/skillscript<ID>.lua.bytes`，逐项映射到角色自己的
`EdenBattle<ID>.cs` 中的 `EdenRecoveredSkillConfiguration`：

| Lua 指令 | 配置/运行行为 |
|---|---|
| `timelineName` | `timelineName` |
| `totalHitCount` | `ultimateTotalHitCount` |
| `MoveAtkCard(... positionInvisible ...)` | 第一次或第二次隐藏时间 |
| `MoveAtkCard(... positionZero ...)` | 中途重新出现时间 |
| `MoveAtkCard(time, nil, 0)` | `ultimateReturnTime`，回原战斗站位 |
| `SetDefCardsActive(time, true)` | `ultimateDefendersVisibleTime` |
| `ChangeDefState(time, "hit_n")` | `ultimateHits` |
| `ChangeAtkState(time, "idle", true)` | `ultimateIdleTime` |
| `ClearSkillMask` | `ultimatePresentationEndTime` / 清理时间 |

注意同一角色可能隐藏两次，而且攻击方可能在最后一段伤害之前就归位。播放器必须按时间合并这些事件，不能简单地“播完所有 hit 后再显示角色”。

Timeline JSON 或 Unity 检查报告中的 `VideoControlPlayableAsset` 给出视频片段的开始和持续时间，对应 `ultimateVideoStartTime` 与 `ultimateVideoEndTime`。文件名在 macOS 上看似不敏感，但 Android 打包后可能敏感，配置必须保持实际文件大小写。

### 3.6 导入战斗语音

在 `character_info.json` 中搜索 `voicePath`。当前数据的类别为：

- `*_Battle_N_*`：普通攻击。
- `*_Battle_H_*`：爆气/特殊技能。
- `*_Battle_C_*`：奥义。

把实际 WAV 复制到 `Assets/EdenBattle/Resources/EdenBattle/Voices/<ID>`，然后在配置中填写不带扩展名的 `Resources` 路径。当前播放器会在同类语音中随机播放，并避免连续两次选中同一条。

导入后至少检查 `AudioClip` 非空，并确认声道数、采样率与原文件一致。若原始文件规格不同，应以原文件为准调整校验，不能为了通过校验重编码后冒充原始数据。梅伊的普通/爆气语音是单声道 44100 Hz，奥义两条原音是双声道 44100 Hz。

### 3.7 增加运行配置

新增 `Assets/EdenBattle/Scripts/Characters/EdenBattle<ID>.cs`：

1. 继承 `EdenRecoveredCharacterBattle` 并实现 `CardId`。
2. 在 `CreateConfiguration()` 填入效果包、语音、普通/爆气段数和 Lua 的奥义时间轴。
3. 在 `EdenRecoveredCharacterBattleRegistry` 注册 ID 与角色类。
4. 如果角色有特殊挂点或 Timeline 对齐，只在该角色类中覆盖相应扩展点。
5. 逐时刻采样 Timeline 发射物与 Spine 武器挂点，再设置 `timelineContainerYOffset`；不能只看 Prefab 根节点。

`EdenRecoveredBattlePreview` 是通用原版技能播放器。除非发现新的事件类型，
否则后续角色只增加自己的 `EdenBattle<ID>.cs`，不复制一套播放器。

### 3.8 坐标与显示的两个常见坑

1. `_2` 目标效果经常在 Prefab 根节点中写了原版绝对 X 坐标，但不同角色的 Y 可能是 `0` 或 `-4`。播放器保留 X，只按敌人站位补偿 Y。
2. Timeline 根节点只能说明整体原点，不能说明发射物的最终位置；动画轨道还会改写子节点坐标。应在实际出现时刻采样目标子节点，并和同刻 Spine 挂点比较后计算偏移。

不要通过统一缩放或统一归零来“修”这些差异，会破坏已经正确的角色。

## 4. 11301006 梅伊的已验证实例

- 普通攻击：角色不移动；`FX_11301006_attack` 在施法者位置生成、`FX_11301006_attack_2` 在目标位置生成，3 段约为 0.33/0.46/0.59 秒。
- 爆气：角色不移动；`FX_11301006_skill` 在施法者位置生成，因此能在头顶凝聚球体，`FX_11301006_skill_2` 保持在目标位置；其收尾粒子延迟到 3.00/3.30/3.40 秒，清理时间为 4.10 秒，不能按前半段球体消失时间提前清理。
- 普通攻击的完整 `FX_11301006_attack_grp/glow (17)` 光效组跟随 `wuqi*` Region 第 1 组边缘顶点；不能只移动它下面的 `FX_daoguang_004_sh` 子粒子。爆气 1.70–1.90 秒出现的六个武器发光节点跟随第 2 组边缘顶点。两种动作的法杖朝向不同，不能共用同一端。普通攻击的最终人工修正只在 `Characters/EdenBattle11301006.cs` 的 `NormalWeaponEffectOffset` 中调整，不会影响其他角色。整套 Prefab 不缩放，头顶蓄力球与目标侧 `_2` 都保持原尺寸。
- 奥义：`Fx_timeline_11301006_xp`，16 段；Timeline 仍为 `timelineContainerYOffset=-4`，4.5 秒时只动态校正 `FX_zidan01` 和 `Shoot-baofa` 的 Y，使发射起点与法杖头同高。
- 视频：1.500–4.500 秒。
- 攻击方：1.499 秒隐藏，4.499 秒出现，5.799 秒再次隐藏，8.665 秒归位，10.000 秒待机。
- 受击方：5.999 秒出现。
- 受击：7.432–9.999 秒，`hit_1`/`hit_2` 交替，最后一击为 `hit_2`。
- 表现结束：10.665 秒，资源清理：10.667 秒。
- 语音：普通 5 条、爆气 2 条、奥义 2 条。
- Timeline Prefab 根 Y 为 0，因此容器补偿 `-4`。
- 自动闭包结果：198 个 AAB（其中 99 个是基础/角色包，99 个由检查报告补齐），所有核心 Prefab `missing=none`。

本次保留的原始依据位于 `RecoveredSources/11301006`：技能 Lua、Timeline 元数据、人物/语音清单和人工摘要。运行时 AssetBundle 的逐文件来源与哈希在 `Assets/StreamingAssets/Skill11301006Original/manifest.json`。

## 5. 完成一个角色后的检查清单

- [ ] 角色战斗 Spine 是自己的，不是 11300018 替身。
- [ ] 普通攻击、爆气、奥义按钮只在对应动作存在时响应。
- [ ] 普通攻击与爆气的效果包、段数和目标位置正确。
- [ ] 奥义视频起止、角色显隐、敌人出现、每段受击、归位、待机和清理均来自 Lua/Timeline。
- [ ] 奥义结束后攻击方必定可见且回到自己的站位。
- [ ] 普通、爆气、奥义语音分类正确。
- [ ] Inspector 所有核心 Prefab 为 `missing=none`。
- [ ] `Validate Battle Scene` 批处理通过。
- [ ] 最后在 Unity 中手动连续播放每个按钮两次，检查第二次播放及随机语音。
