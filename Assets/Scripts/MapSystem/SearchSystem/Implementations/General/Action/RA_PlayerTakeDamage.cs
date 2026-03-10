using UnityEngine;



//----------------------------------------------------
// 玩家受到value点伤害（阳气）
//----------------------------------------------------



[CreateAssetMenu(fileName = "损失阳气Action", menuName = "Reward/GeneralAction/损失阳气")]
public class RA_PlayerTakeDamage : RewardAction
{
    public override void Execute(RewardInstance reward, OptionRuntimeState optionState)
    {
        GameManager.Instance.player.PlayerTakeDamage(value);
    }
}
