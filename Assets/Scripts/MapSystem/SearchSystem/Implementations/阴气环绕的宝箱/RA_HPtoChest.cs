using UnityEngine;

[CreateAssetMenu(fileName = "¿ªÆôÒõÆø±¦ÏäAction", menuName = "Reward/BasicAction/ÒõÆø±¦Ïä")]
public class RA_HPtoChest : RewardAction
{
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        GameManager.Instance.player.PlayerTakeDamage(10f);

        reward.ChangeState(RewardState.Opened);
    }
}
