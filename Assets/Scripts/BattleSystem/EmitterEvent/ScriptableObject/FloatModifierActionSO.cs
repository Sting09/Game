using NaughtyAttributes;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle System/Emitter Event/Float Modifier")]
public class FloatModifierActionSO : ScriptableObject
{
    [Header("Modification")]
    public EmitterPropertyType targetProperty;
    public EventModificationType modificationType;
    public float value;
    public bool needMod360 = false;


    [Header("Tween Info")]
    [Tooltip("变化过程需要多长时间")]
    public float duration = 0f;
    [Tooltip("变化曲线 (0~1)")]
    public AnimationCurve curve = AnimationCurve.Linear(0, 1, 1, 1);


    [Header("Dynamic Growth")]
    [Tooltip("是否启用基于曲线的动态增长")]
    public bool useGrowthCurve = false;
    [Tooltip("动态变化基本值，可以用来实现波粒等效果")]
    public float dynamicValue = 0f;
    [Tooltip("自定义增长函数。\n X轴：执行次数\n Y轴：value + y * dynamicValue")]
    public AnimationCurve growthCurve = AnimationCurve.Linear(0, 1, 10, 1);

}