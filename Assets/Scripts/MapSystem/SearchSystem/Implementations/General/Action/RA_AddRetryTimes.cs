using UnityEngine;



//----------------------------------------------------
// 玩家增加重试次数
//----------------------------------------------------



[CreateAssetMenu(fileName = "增加重试次数Action", menuName = "Reward/GeneralAction/增加重试次数")]
public class RA_AddRetryTimes : RewardAction
{
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        GameManager.Instance.player.AddRetryTimes((int)value);
    }
}
