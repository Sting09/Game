using UnityEngine;


//----------------------------------------------------
// 切换奖励状态到关闭状态
//----------------------------------------------------


[CreateAssetMenu(fileName = "CloseAction", menuName = "Reward/GeneralAction/Close状态")]
public class RA_CloseState : RewardAction
{
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        //切换状态会导致清空所有选项，然后添加新状态的默认选项
        reward.ChangeState(RewardState.Closed);
    }
}
