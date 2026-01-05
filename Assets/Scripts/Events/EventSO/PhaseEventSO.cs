using UnityEngine;

//专门用于传递object类型参数的事件
[CreateAssetMenu(fileName = "New Object Event", menuName = "Events/Phase Event")]
public class PhaseEventSO : BaseEventSO<GamePhase>
{
    // 专门处理游戏阶段类型的事件
}