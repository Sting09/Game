using UnityEngine;



//----------------------------------------------------
// 切换奖励状态到开启状态
//----------------------------------------------------



[CreateAssetMenu(fileName = "OpenAction", menuName = "Reward/GeneralAction/Open状态")]
public class RA_OpenState : RewardAction
{
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        //切换状态会导致清空所有选项，然后添加新状态的默认选项
        reward.ChangeState(RewardState.Opened);
    }
}
