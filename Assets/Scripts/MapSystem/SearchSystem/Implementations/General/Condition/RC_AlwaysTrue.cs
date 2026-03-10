using UnityEngine;

public class RC_AlwaysTrue : RewardCondition
{
    public override bool IsMet(RewardInstance reward)
    {
        return true;
    }
}
