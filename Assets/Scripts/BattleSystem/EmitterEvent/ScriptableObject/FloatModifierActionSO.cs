using UnityEngine;

[CreateAssetMenu(menuName = "Battle System/Emitter Event/Float Modifier")]
public class FloatModifierActionSO : ScriptableObject
{
    [Header("Modification")]
    public EmitterPropertyType targetProperty;
    public EventModificationType modificationType;
    public float value;

    [Header("Tween Info")]
    [Tooltip("变化过程需要多长时间")]
    public float duration = 0f;
    [Tooltip("变化曲线 (0~1)")]
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
}