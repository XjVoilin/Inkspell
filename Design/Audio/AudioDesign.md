# Inkspell Audio Design

## 声音方向

首版声音语言为“温暖纸艺、干涩书页、液态墨水、克制魔法”。所有交付音频均由
`Tools/Audio/generate_inkspell_audio.py` 程序生成，不包含第三方录音、采样包或受限音乐素材。

## 资源规格与预算

- BGM：24 kHz、16-bit、立体声 WAV，48 秒、精确 16 小节循环。
- SFX：32 kHz、16-bit、单声道 WAV，0.14–1.72 秒。
- 源资源预算：14 MiB；当前实际约 10.2 MiB。
- Unity 构建预算：2 MiB；BGM 使用 Vorbis 0.32 Streaming，SFX 使用 Vorbis 0.42 Decompress On Load。
- Unity 2022.3 当前导入后的 38 个 FSB 音频数据合计约 0.67 MiB，预算余量约 66%。
- BGM 不随普通关卡重启；Boss 关切换 Boss 循环，退出 Boss 关恢复主循环。

精确时长、声道、峰值、RMS、文件体积和 Address 见 `audio-manifest.json`。

## 分类

- `BGM`：主循环、Boss 循环。
- `SFX/UI`：点击、拖拽、非法输入、装备、锁定。
- `SFX/System`：法术生成、离线收益、升级。
- `SFX/Synthesis`：合成开始、成功、转化墨水。
- `SFX/Spell`：火球、雷链、冰环、护盾的施放与命中。
- `SFX/Battle`：敌人受击/死亡和魔法书受击。
- `SFX/Stage`：胜利、失败、重试与 Boss 登场。

## 混音约束

- BGM 默认播放音量 0.42；UI 0.45–0.65；战斗 0.35–0.60；结果提示 0.65–0.80。
- 同一资源由 `July.Audio.AudioSystem` 限制为一个活跃实例；高频反馈通过多个变体轮换。
- 战斗声音跟随稳定 AttackId、EffectId 和 Enemy RuntimeId 去重，不能由每帧 Render 重复触发。
- 合成失败必须表达“转化为墨水”，使用湿润下坠声和正向到账尾音，不做纯惩罚反馈。

## 再生成

使用 Codex 工作区 Python（包含 NumPy）执行：

```bash
/Users/july/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3 Tools/Audio/generate_inkspell_audio.py
```

生成脚本采用固定随机种子，相同版本会得到相同资源。运行后需让 Unity 重新导入音频，
`InkspellAudioImportProcessor` 会自动应用压缩和采样率设置。
