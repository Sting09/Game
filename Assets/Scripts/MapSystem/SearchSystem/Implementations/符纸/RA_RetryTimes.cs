using UnityEngine;

[CreateAssetMenu(fileName = "·ûÖ½Action", menuName = "Reward/BasicAction/·ûÖ½")]
public class RA_RetryTimes : RewardAction
{
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        GameManager.Instance.player.AddRetryTimes(1);
        reward.ChangeState(RewardState.Closed);
    }
}
