using UnityEditor;
using System.Collections.Generic;
using UnityEngine;
using System;

//专门为ObjectEventSO类型创建的编辑器
[CustomEditor(typeof(ObjectEventSO))]
public class PhaseEventSOEditor : Editor
{
    private PhaseEventSO phaseEventSO;

    private void OnEnable()
    {
        phaseEventSO = target as PhaseEventSO;
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
                    EditorGUILayout.LabelField("• " + listener.name, EditorStyles.miniLabel);
                    if (GUILayout.Button("选择", GUILayout.Width(50)))
                    {
                        Selection.activeGameObject = listener.gameObject;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        // 显示最后发送者信息
        if (!string.IsNullOrEmpty(phaseEventSO.lastSender))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("最后发送者：", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(phaseEventSO.lastSender, EditorStyles.miniLabel);
        }
    }

    private List<MonoBehaviour> GetListeners()
    {
        List<MonoBehaviour> listeners = new();

        if (phaseEventSO == null || phaseEventSO.OnEventRaised == null)
        {
            return listeners;
        }

        try
        {
            var subscribers = phaseEventSO.OnEventRaised.GetInvocationList();

            foreach (var subscriber in subscribers)
            {
                var obj = subscriber.Target as MonoBehaviour;
                if (obj != null && !listeners.Contains(obj))
                {
                    listeners.Add(obj);
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
