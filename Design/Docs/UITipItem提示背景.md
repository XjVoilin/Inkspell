# UITipItem 提示背景

参考 GooseMarket 的 common_tips_bg.png（深色、横向拉伸提示条），结合 GDD 和现有 MainWindow 的墨色、旧金边风格。通过内置图片生成工具制作，最终提示词保存在 UITipItem_art_plan.json。原始生成图保留在工具输出目录；透明留白裁切后规格化为 1024×128 PNG。

- 美术源：Design/AIArt/ArtSource/UITipItem/bg_inkToast.png
- 运行时：Assets/Game/Arts/Textures/UITipItem/bg_inkToast.png
- 预制体：Assets/Game/Res/Prefabs/UITipItem.prefab
- Image 使用 Sliced，边界 left/bottom/right/top = 64/24/64/24。
- 根节点和背景 990×128，修复原先根节点高度为 0 导致的堆叠间距错误。
- 显式绑定现有 TMP 组件，文字区域 810×88，字号 28–40 自动调整，允许换行；超出两行容量时截断。
- 图片不含文字，运行时提示继续使用现有本地化；关闭背景和文字的射线接收。
- 预览展示现有本地化文案的成功、失败以及 700 像素宽度拉伸检查。
