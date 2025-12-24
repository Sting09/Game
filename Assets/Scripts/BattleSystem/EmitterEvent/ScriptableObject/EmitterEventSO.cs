using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle System/Emitter Event/Emitter Event")]
public class EmitterEventSO : ScriptableObject
{
    [Header("Timing")]
    [Tooltip("发射器启动后多少秒开始第一次执行")]
    public float startDelay = 0f;
    [Tooltip("执行间隔 (-1表示只执行一次)")]
    public float interval = -1f;
    [Tooltip("重复次数 (0表示无限循环)")]
    public int loopCount = 0;

    [Header("Conditions")]
    [Tooltip("可选：执行条件列表 (全部满足才执行)")]
    public List<BulletConditionSO> conditions; // 复用你现有的Condition系统

    [Header("Actions")]
    public List<FloatModifierActionSO> actions;
}