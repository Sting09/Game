using UnityEngine;

[CreateAssetMenu(menuName = "Battle System/Emitter Event/Float Modifier")]
public class FloatModifierActionSO : ScriptableObject
{
    [Header("Modification")]
    public EmitterPropertyType targetProperty;
    public EventModificationType modificationType;
    public float value;
    [Tooltip("从BattleManager里读数据。自然数表示索引，负数表示使用上面的value")]
    public int valueIndex = -1;

    [Header("Tween Info")]
    [Tooltip("变化过程需要多长时间")]
    public float duration = 0f;
    [Tooltip("变化曲线 (0~1)")]
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Save Info")]
    [Tooltip("任务执行第多少秒保存数据到BattleManager")]
    public float saveTime = -1;
    [Tooltip("数据保存到BattleManager第几个参数")]
    public int saveIndex = -1;
}