using UnityEngine;

[CreateAssetMenu(fileName = "开启阴气宝箱Action", menuName = "Reward/BasicAction/阴气宝箱")]
public class RA_HPtoChest : RewardAction
{
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        GameManager.Instance.player.PlayerTakeDamage(10f);

        reward.ChangeState(RewardState.Opened);     //切换状态后，unknown的选项全清空了，添加所有opened的选项，但是为空

        reward.currentOptions.Add(reward.Data.otherOptions[Random.Range(0,3)]); //接下来添加一个other中随机的选项
    }
}
