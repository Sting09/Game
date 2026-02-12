场景清单
Main：主场景，时刻激活，保存摄像机、EventSystem、需要时刻激活的Manager
Title：标题场景，启动游戏时进入，点击Continue或New Game进入地图场景后可卸载
Map：地图场景
Battle：战斗场景

Additive 加载策略
Map Scene 和 Battle Scene 共存，通过 Additive 模式加载。
由 Map 到 Battle 时：
1. SceneLoader 先渐入到全黑。
2. 黑屏后再隐藏 Map 元素，并 Additive 加载 Battle Scene。
3. 切换 Battle 为 Active Scene。
4. 全部完成后再渐出恢复画面。

由 Battle 到 Map 时：
1. SceneLoader 先渐入到全黑。
2. 黑屏后执行战斗结算回调（触发胜负逻辑、恢复 Map 元素显示）。
3. 卸载 Battle Scene，并把 Map Scene 设为 Active Scene。
4. 全部完成后再渐出恢复画面。

黑屏动画配置
- fadeInDuration：渐入黑屏时长。
- fadeOutDuration：渐出黑屏时长。
- blackoutHoldDuration：全黑后的额外停留时长。
- minWaitTime：场景异步加载完成后的最小等待时长。
