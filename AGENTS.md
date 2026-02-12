# AGENTS.md

## 项目技术栈
- Unity 6000.0.54f1
- URP + Input System

## 架构约束
- 场景切换统一走 SceneLoader，不允许在业务脚本直接 LoadScene
- 管理器遵循 SingletonMono<T> 约定
- 新增 ScriptableObject 需在 docs/data-contracts.md 追加字段说明

## 代码风格
- 类名/方法名 PascalCase，字段 camelCase
- 公共 API 必须有 XML 注释
- 不要提交临时调试代码和注释掉的大段逻辑

## 修改后最小验证
1. Title -> Map 正常
2. Map 触发 Battle 正常，有黑屏渐入渐出
3. Battle 结束回到 Map 正常，有黑屏渐入渐出
4. 控制台无新增 Error

## 架构约束
- 凡使用中文字符，例如注释、tooltip等，一律使用utf-8各式编码