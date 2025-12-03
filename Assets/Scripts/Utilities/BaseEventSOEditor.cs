using UnityEditor;
using System.Collections.Generic;
using UnityEngine;
using System;

//意思是，根据BaseEventSO这个类型，重写Inspector窗口中的样式
//注意：Unity不支持泛型CustomEditor，所以我们需要为具体的类型创建编辑器
public class BaseEventSOEditor : Editor
{
    private ScriptableObject baseEventSO;

    private void OnEnable()
    {
        baseEventSO = target as ScriptableObject;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("监听者信息：", EditorStyles.boldLabel);
        
        var listeners = GetListeners();
        
        if (listeners.Count == 0)
        {
            EditorGUILayout.HelpBox("当前没有监听者订阅此事件", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField($"监听者数量: {listeners.Count}");
            
            foreach (var listener in listeners)
            {
                if (listener != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("? " + listener.name, EditorStyles.miniLabel);
                    if (GUILayout.Button("选择", GUILayout.Width(50)))
                    {
                        Selection.activeGameObject = listener.gameObject;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        
        // 显示最后发送者信息
        var lastSenderField = baseEventSO.GetType().GetField("lastSender");
        if (lastSenderField != null)
        {
            var lastSender = lastSenderField.GetValue(baseEventSO) as string;
            if (!string.IsNullOrEmpty(lastSender))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("最后发送者：", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(lastSender, EditorStyles.miniLabel);
            }
        }
    }

    private List<MonoBehaviour> GetListeners()
    {
        List<MonoBehaviour> listeners = new();

        if(baseEventSO == null)
        {
            return listeners;
        }

        try
        {
            // 使用反射获取OnEventRaised字段
            var onEventRaisedField = baseEventSO.GetType().GetField("OnEventRaised");
            if (onEventRaisedField == null)
            {
                return listeners;
            }

            var onEventRaised = onEventRaisedField.GetValue(baseEventSO);
            if (onEventRaised == null)
            {
                return listeners;
            }

            // 获取委托的调用列表
            var subscribers = onEventRaised.GetType().GetMethod("GetInvocationList")?.Invoke(onEventRaised, null) as Delegate[];
            
            if (subscribers != null)
            {
                foreach (var subscriber in subscribers)
                {
                    var obj = subscriber.Target as MonoBehaviour;
                    if(obj != null && !listeners.Contains(obj))
                    {
                        listeners.Add(obj);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"获取监听者时出错: {e.Message}");
        }

        return listeners;
    }
}