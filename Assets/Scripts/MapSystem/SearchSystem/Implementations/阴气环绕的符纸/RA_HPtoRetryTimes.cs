using UnityEngine;

[CreateAssetMenu(fileName = "ÒõÆø·ûÖ½Action", menuName = "Reward/BasicAction/ÒõÆø·ûÖ½")]
public class RA_HPtoRetryTimes : RewardAction
{
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        GameManager.Instance.player.PlayerTakeDamage(10f);
        GameManager.Instance.player.AddRetryTimes(10);
        reward.ChangeState(RewardState.Closed);
    }
}
