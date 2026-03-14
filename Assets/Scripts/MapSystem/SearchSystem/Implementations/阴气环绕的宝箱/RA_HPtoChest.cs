using UnityEngine;

[CreateAssetMenu(fileName = "开启阴气宝箱Action", menuName = "Reward/BasicAction/阴气宝箱")]
public class RA_HPtoChest : RewardAction
{
    public int itemNum; 
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        for(int i = 0; i < itemNum; ++i)
        {
            reward.currentOptions.Add(reward.Data.otherOptions[Random.Range(0, 3)]); //添加一个other中随机的选项
        }
    }
}
