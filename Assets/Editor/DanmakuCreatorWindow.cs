using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

public class DanmakuCreatorWindow : EditorWindow
{
    // === UI 参数 ===
    private string baseName = "NewDanmaku";
    private int emitterCount = 1;
    private bool createEmitterEvent = false;
    private bool createEntityEvent = false;

    // === 类型选择缓存 ===
    private int selectedEmitterTypeIndex = 0;
    private List<Type> emitterTypes;
    private string[] emitterTypeNames;

    private int selectedPatternTypeIndex = 0;
    private List<Type> patternTypes;
    private string[] patternTypeNames;

    [MenuItem("Tools/Danmaku Creator Wizard")]
    public static void ShowWindow()
    {
        GetWindow<DanmakuCreatorWindow>("弹幕生成器");
    }

    private void OnEnable()
    {
        // 反射获取所有继承自 AbstractEmitterConfigSO 的非抽象类
        emitterTypes = GetSubClasses(typeof(AbstractEmitterConfigSO));
        emitterTypeNames = emitterTypes.Select(t => t.Name).ToArray();

        // 反射获取所有继承自 ShootPatternSO 的非抽象类
        patternTypes = GetSubClasses(typeof(ShootPatternSO));
        patternTypeNames = patternTypes.Select(t => t.Name).ToArray();
    }

    private void OnGUI()
    {
        GUILayout.Label("弹幕一键生成配置", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 1. 基础设置
        baseName = EditorGUILayout.TextField("基础名称 (如 Boss1_Spell1)", baseName);

        EditorGUILayout.Space();
        GUILayout.Label("组件设置", EditorStyles.label);

        // 2. 发射器设置
        if (emitterTypes != null && emitterTypes.Count > 0)
        {
            selectedEmitterTypeIndex = EditorGUILayout.Popup("发射器类型", selectedEmitterTypeIndex, emitterTypeNames);
        }
        else
        {
            EditorGUILayout.HelpBox("未找到具体的发射器配置类 (AbstractEmitterConfigSO 的子类)", MessageType.Warning);
        }

        emitterCount = EditorGUILayout.IntSlider("创建发射器数量", emitterCount, 1, 5);
        createEmitterEvent = EditorGUILayout.Toggle("同时创建发射器事件", createEmitterEvent);
        createEntityEvent = EditorGUILayout.Toggle("同时创建子弹事件组", createEntityEvent);

        EditorGUILayout.Space();

        // 3. 子弹样式设置
        if (patternTypes != null && patternTypes.Count > 0)
        {
            selectedPatternTypeIndex = EditorGUILayout.Popup("子弹样式类型", selectedPatternTypeIndex, patternTypeNames);
        }
        else
        {
            EditorGUILayout.HelpBox("未找到具体的子弹样式类 (ShootPatternSO 的子类)", MessageType.Warning);
        }

        EditorGUILayout.Space(20);

        // 4. 生成按钮
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("一键生成并链接", GUILayout.Height(40)))
        {
            CreateAssets();
        }
        GUI.backgroundColor = Color.white;
    }

    private void CreateAssets()
    {
        // 1. 获取当前选中的文件夹路径
        string path = GetSelectedPathOrFallback();
        string folderName = baseName;
        string fullPath = Path.Combine(path, folderName);

        // 创建子文件夹以保持整洁
        if (!Directory.Exists(fullPath))
        {
            AssetDatabase.CreateFolder(path, folderName);
        }

        // 2. 创建 DanmakuSO
        DanmakuSO danmakuSO = CreateAsset<DanmakuSO>(fullPath, $"{baseName}_Danmaku");
        danmakuSO.emitterList = new List<AbstractEmitterConfigSO>();

        // 3. 循环创建 Emitter 并链接
        for (int i = 0; i < emitterCount; i++)
        {
            string suffix = emitterCount > 1 ? $"_{i + 1}" : "";

            // 创建 Pattern
            Type patternType = patternTypes[selectedPatternTypeIndex];
            ShootPatternSO patternSO = (ShootPatternSO)CreateAsset(patternType, fullPath, $"{baseName}_Pattern{suffix}");

            // 创建 Emitter
            Type emitterType = emitterTypes[selectedEmitterTypeIndex];
            AbstractEmitterConfigSO emitterSO = (AbstractEmitterConfigSO)CreateAsset(emitterType, fullPath, $"{baseName}_Emitter{suffix}");

            // 链接 Pattern -> Emitter
            emitterSO.bulletPattern = patternSO;
            EditorUtility.SetDirty(emitterSO); // 标记修改

            // (可选) 创建 EmitterEvent
            if (createEmitterEvent)
            {
                EmitterEventSO eventSO = CreateAsset<EmitterEventSO>(fullPath, $"{baseName}_Event{suffix}");
                emitterSO.emitterEvents = new List<EmitterEventSO> { eventSO };
                EditorUtility.SetDirty(emitterSO);
            }

            // 链接 Emitter -> Danmaku
            danmakuSO.emitterList.Add(emitterSO);

            // (可选) 创建 EntityEvent
            if (createEntityEvent)
            {
                CreateAsset<EntityEventGroup>(fullPath, $"{baseName}_Bullet{suffix}");
            }
        }

        EditorUtility.SetDirty(danmakuSO);

        // 4. 保存并刷新
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 选中生成的 DanmakuSO
        Selection.activeObject = danmakuSO;
        Debug.Log($"<color=green>成功创建弹幕组：{baseName}</color> \n位置：{fullPath}");
    }

    // === 辅助方法 ===

    private T CreateAsset<T>(string path, string name) where T : ScriptableObject
    {
        return (T)CreateAsset(typeof(T), path, name);
    }

    private ScriptableObject CreateAsset(Type type, string path, string name)
    {
        ScriptableObject asset = CreateInstance(type);
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath($"{path}/{name}.asset");
        AssetDatabase.CreateAsset(asset, uniquePath);
        return asset;
    }

    private string GetSelectedPathOrFallback()
    {
        string path = "Assets";
        foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
        {
            path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
                break;
            }
        }
        return path;
    }

    private List<Type> GetSubClasses(Type baseType)
    {
        var types = new List<Type>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            try
            {
                var subTypes = assembly.GetTypes().Where(t => t.IsSubclassOf(baseType) && !t.IsAbstract);
                types.AddRange(subTypes);
            }
            catch { /* 忽略某些动态程序集的加载错误 */ }
        }
        return types;
    }
}