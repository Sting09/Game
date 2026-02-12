场景清单
Main：主场景，时刻激活，保存摄像机、EventSystem、需要时刻激活的Manager
Title：标题场景，启动游戏时进入，点击Continue或New Game进入地图场景后可卸载
Map：地图场景
Battle：战斗场景



Additive 加载策略
Map Scene和Battle Scene共存，通过Additive模式加载
由map到battle时，锁定玩家输入、重置摄像机位置和拍摄范围、隐藏地图、隐藏地图上的玩家、显示战斗场景的玩家，加载完成后恢复玩家输入
由battle到map时，锁定玩家输入，清空对象池（子弹、敌人、玩家子弹三个对象池），结束它们的Job，确保不要有内存泄漏，隐藏战斗场景的玩家，显示地图、显示地图上的玩家，摄像机对准玩家位置，加载完成后恢复玩家输入
场景加载应有黑屏渐入渐出动画，动画时长可在SceneLoader中配置。