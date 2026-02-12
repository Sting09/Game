项目介绍：
这是一款基于Unity开发的STG + Roguelike游戏。
玩家将在六边形网格地图中移动，地图形状固定，但网格上房间内的敌人、奖励随机生成。玩家可以选择一个地图上的敌人战斗，之后将进入战斗场景。战斗中，敌人的攻击采用STG战斗模式，敌人将发射运动轨迹复杂、变化丰富，视觉效果华丽的弹幕。玩家胜利后，将获得随机奖励，提高战力；玩家失败后，可以消耗挑战次数重新战斗，只有当玩家挑战次数归零后，才算游戏失败。



Unity 版本：
6000.0.54f1
依赖InputSystem



目录结构导览（Assets/Scripts 各子系统职责）
BattleSystem——战斗系统相关代码，包括：弹幕编辑器、玩家操控、伤害计算
逻辑：一个敌人配置若干个发射器Shooter，每个发射器发射一种弹幕Danmaku。一个Danmaku由多种发射器Emitter组成，一个发射器有若干发弹点Shooting Point，每个发弹点发射子弹或敌人，具体发射什么物体、发射什么形状由Pattern控制。
各种物体交由BaseObjManager和它的子类管理。
Events——项目使用的事件系统和事件监听器
Gameplay——暂未实现，未来道具系统、奖励系统、Buff系统在这里编写
MapSystem——地图系统相关代码，包括地图生成、房间逻辑等
System——其他游戏管理类
UI——各场景UI元素的代码
Utilities——工具类




场景流程图
Title → Map →Battle → Map → Battle → ……往复循环
Map Scene和Battle Scene通过Additive方式添加，两个场景同时存在，切换时要控制场景内元素的显示与隐藏。



如何运行：
如果只有一个已装载场景，则认为是在体验游戏，此时：
游戏启动后，直接运行SceneLoader的StartGameProcess()协程，硬编码启动Title Scene。暂时未用到GameBootstrapper。注意不要启动GameBootstrapper，这会导致游戏启动后有两个Title Scene。
如果有两个已装载场景，则认为是在开发游戏，不会再自动装载其他场景。
