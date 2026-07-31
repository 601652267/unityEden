# Eden 立绘 2 特效导入工作区

本目录保存从 APK/OBB 还原立绘 2 特效时使用的 Unity Editor 导入逻辑。实际导入在独立、持久化的 Unity 2018.3.5f1 项目中运行，以便复用 `Library`，避免每次重新导入整个工程。

## 持久化工作区

- Unity 项目：`/Users/zhuhaiming/Desktop/edenproject/unity-effect-importer-cache`
- 原始 AssetBundle：`/Users/zhuhaiming/Desktop/edenAssetsObb/eft_fx_mainback`
- 原始依赖：`/Users/zhuhaiming/Desktop/edenAssetsObb/main.19.jp.co.yoozoo.projectyellow/assets/ResEx`
- 已解析纹理：`/Users/zhuhaiming/Desktop/edenAssets/edenAssets/edenAssets2/Texture2D`

不要清理工作区的 `Library`。新增角色时先将本目录的 `EdenEffectBatchImporter.cs` 同步到工作区的 `Assets/EdenGallery/Editor/`，然后只执行一次批量导出：

```text
/Applications/Unity/Hub/Editor/2018.3.5f1/Unity.app/Contents/MacOS/Unity
  -batchmode -quit
  -projectPath /Users/zhuhaiming/Desktop/edenproject/unity-effect-importer-cache
  -executeMethod EdenEffectBatchImporter.Run
```

导入器会复用已有公共纹理和兼容材质，移除立绘背景、角色 Spine、Animator、丢失脚本等不应重复进入特效 Prefab 的对象，并持久化运行时依赖的 Mesh、Material 和 Sprite。每批只做一次导出，导出后统一检查：

- Prefab 可以加载；
- ParticleSystem 数量大于 0；
- 没有丢失组件；
- Renderer 的材质、Shader、主纹理和 Mesh 均有效。

## 首轮试导入

首轮有效特效角色为：

- `11300041` 芸芸：8 个粒子系统；
- `11301003` 伊莎贝拉：8 个粒子系统；
- `11301004` 艾凡缇：3 个粒子系统；
- `11301005` 翔：4 个粒子系统；
- `11301006` 梅伊：6 个粒子系统。

`11202014`、`11202016`、`11300032`、`11300036` 的对应包没有 `ParticleSystem`，属于背景和 Spine 组合，不应生成空的特效 Prefab。`11300041` 的有效粒子在 `eft_fx_mainback_11300041.aab` 中，不能误用仅包含立绘组合的 `eft_fx_mainback_11300041_3.aab`。

本轮完整导出和基础检查记录保存在
`reports/first-five-2026-07-31.txt`。

## 无粒子角色的组合层补全

对 `11202014`、`11202016`、`11300032`、`11300036` 重新加载 mainback
包及外部背景依赖后确认，这四个包的粒子系统数量均为 0。它们不生成
`FX_MainBack_*_Effect.prefab`，而是直接在 `gallery.json` 中还原原始
Sprite 与 Spine 层：

- `11202014`：原包除全屏 `bg_3` 外还有建筑前景 `bg_1` 与窗框
  `bg_2`；现已按原坐标、比例和 Z 深度补入。
- `11202016`：`BG1`、`BG2` 和两套 Spine 已完整，无需修改。
- `11300032`：一张背景和两套 Spine 已完整，无需修改。
- `11300036`：原包的 `BG1` 缺失；现已补入，并把原 Sprite 的
  `pixelsPerUnit=93.94495` 换算到画廊运行时的 100 PPU，最终比例为
  `0.817500036`。

## 11301003 / 11301005 视觉修正

针对首轮目视检查发现的问题，可以只运行：

```text
-executeMethod EdenEffectBatchImporter.RunVisualFixes
```

该入口只重导 `11301003` 和 `11301005`，但仍会统一检查首轮五个角色。

- `11301003`：`PrefabRenderHolder` 明确把 `sfx_inspace_hexin` 的
  `_MainTex` 映射到 `flare_red_mask01`。之前误用整块
  `sfx_huihui_hexin` 才会出现半透明方块。原始
  `SoulGames/Effects/Additive` 的 `_DeadStrength=0.01` 暗像素裁切也已保留。
- `11301005`：`FX_tex_xiao_liuguang_mainback` 的原始 Shader 来自
  `common.aab`，类型为 `SoulGames/Effects/Mask Additive`。现在会加载该
  Shader 依赖，并按 `PrefabRenderHolder` 保留刀身网格、主纹理
  `sfx_tx_light_xiao_mainback`、遮罩 `beam_mask004` 和
  `_DeadStrength=0.01`。原网格没有第二套 UV，导入时会复制 UV0 到 UV1；
  运行时沿 `_MaskTex` 的 X 方向滚动窄条遮罩，恢复附着在刀上的流光。
  APK 内 5 秒 Animator 片段没有曲线或采样变化，因此不再把它当作流光驱动。

修正后的后台导出和基础检查记录保存在
`reports/visual-fixes-11301003-11301005-2026-07-31.txt`。
