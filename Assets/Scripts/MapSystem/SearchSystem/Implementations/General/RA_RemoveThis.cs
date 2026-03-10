using UnityEngine;

[CreateAssetMenu(fileName = "移除选项Action", menuName = "Reward/GeneralAction/移除选项")]
public class RA_RemoveThis : RewardAction
{
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        reward.currentOptions.Remove(optionState.Def);
    }
}
