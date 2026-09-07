# 启动画面

采用已确认的“魔法书苏醒”方向，复用 MainWindow 的暖纸、活体魔法书、墨线和漩涡印记。标题为《墨法奇书》与 INKSPELL。画布为 1080 × 1920，留白优先。

启动场景直接序列化 Canvas、Sprite 和 TMP 字体引用；所有文字使用 TextMeshProUGUI，状态字段使用 TMP_Text，不依赖资源下载、热更代码或运行时语言表。启动文字从 Language.xlsx 对应的导出配置写入场景。更新这些文字后，通过 July/Inkspell/Rebuild Launch Scene 重新生成。

魔法书缓慢浮动、轻微摇摆，背后的金色印记呼吸，六个墨点轻微漂移。动画使用不受 timeScale 影响的时间。细墨线表示已完成的启动阶段，不显示虚假的下载百分比。现有下载接口未暴露字节进度，因此下载期间显示准备篇章的状态文字。

场景画面作为持久入口的子对象，跨 Main 场景加载保留。主窗口打开完成后等一帧，再用 0.3 秒淡出。无需用户点击，不设置最短展示时长。失败时停止动效、显示重新启动提示，并保留原有错误日志。

美术源目录内的 Launch 资源为现有 MainWindow 文件的原样复用副本，仅用于 authoring JSON；Unity 场景直接引用既有 Assets 资源，避免新增运行时纹理副本。authoring JSON 记录静态布局；程序化规则线、墨点、Canvas、动态进度和状态绑定由 LaunchSceneAuthoring 完成，非导入器自动层级。
