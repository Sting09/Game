using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(RangedFloat))]
[CustomPropertyDrawer(typeof(RangedInt))] 
public class RangedFloatDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 1. 关键修复：获取 BeginProperty 返回的 label
        // BeginProperty 会处理 Prefab 的蓝色标记，并返回包含正确 Tooltip 的 label 对象
        label = EditorGUI.BeginProperty(position, label, property);

        // 绘制左侧的标签（变量名），同时保留右侧区域给 position
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // 恢复缩进，防止输入框跑偏
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // --- 布局计算 ---
        float symbolWidth = 12f;
        float fieldWidth = (position.width - 2 * symbolWidth) / 3f;

        Rect baseRect = new Rect(position.x, position.y, fieldWidth, position.height);
        Rect plusRect = new Rect(baseRect.xMax, position.y, symbolWidth, position.height);
        Rect offsetRect = new Rect(plusRect.xMax, position.y, fieldWidth, position.height);
        Rect pmRect = new Rect(offsetRect.xMax, position.y, symbolWidth, position.height);
        Rect randomRect = new Rect(pmRect.xMax, position.y, fieldWidth, position.height);

        // --- 获取子属性 ---
        var baseProp = property.FindPropertyRelative("baseValue");
        var offsetProp = property.FindPropertyRelative("offsetValue");
        var randomProp = property.FindPropertyRelative("randomRange");

        // --- 2. 关键修复：创建一个带有 Tooltip 但没有文字的 GUIContent ---
        // 这样鼠标悬停在三个输入框上时，也能看到 Tooltip
        GUIContent subLabel = new GUIContent("", label.tooltip);

        // --- 绘制 ---

        // Base 值
        EditorGUI.PropertyField(baseRect, baseProp, subLabel);

        // "+" 号
        var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
        EditorGUI.LabelField(plusRect, new GUIContent("+", "Base + Offset"), style); // 这里也可以加个小提示

        // Offset 值
        EditorGUI.PropertyField(offsetRect, offsetProp, subLabel);

        // "±" 号
        EditorGUI.LabelField(pmRect, new GUIContent("±", "Random Range"), style);

        // Random 值
        EditorGUI.PropertyField(randomRect, randomProp, subLabel);

        // 结束绘制
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}