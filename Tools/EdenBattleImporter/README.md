# Eden 战斗 Spine 导入器

运行下面的命令，可从解析后的 APK 文件目录同步画廊人物的战斗 Spine：

```text
node Tools/EdenBattleImporter/importBattleSpines.js
```

导入器以 `gallery.json` 的角色列表为范围，只复制完整具备
`battle_spine` 和 `battle_texture` 的角色，并生成 Unity 2018 可识别的
`.meta`。缺少战斗 Spine 的角色会列在终端报告中，不会使用其他角色
作为替身。

还原单个角色的原始技能 AssetBundle 与奥义视频时运行：

```text
node Tools/EdenBattleImporter/prepareSkillBundles.js 11301023
```

脚本会去除 APK/OBB 包在 `UnityFS` 前面的自定义包头，并复用已经验证过的
公共依赖。角色专属额外依赖保存在脚本的 `extraDependencies` 表中。

若首次 Unity 检查报告仍显示 `missing=...`，让脚本按资源名自动补齐
对应的 `st_*` 依赖：

```text
node Tools/EdenBattleImporter/prepareSkillBundles.js 11301006 \
  --missing-report /private/tmp/eden_skill_11301006_inspect.txt
```

脚本会自动收集该角色全部 `eft_fx_<ID>_*` 与 Timeline 包；只有检查报告
暴露出的共用贴图、Mesh 依赖才需要第二次运行。完整的人工还原流程、字段解释、
批处理检查命令和 11301006 实例见 [RECOVERY_GUIDE.md](RECOVERY_GUIDE.md)。

为避免每次重新导入整个项目，项目根目录保留了被 Git 忽略的
`.eden-unity-validation` 工作区及其 `Library`。后续检查应复用它。
