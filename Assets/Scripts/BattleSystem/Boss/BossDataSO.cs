using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "BossData")]
public class BossDataSO : ScriptableObject
{
    public List<BossPhaseData> phases;
}

[System.Serializable]
public class BossPhaseData
{
    public int maxHP;
    public DanmakuSO phaseDanmaku;

    [Header("阶段开始时执行的动作序列")]
    public List<BossTransitionActionSO> onPhaseStartActions;
    [Header("阶段结束(血量归零)时执行的动作序列")]
    public List<BossTransitionActionSO> onPhaseEndActions;
}
