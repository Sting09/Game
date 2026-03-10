using System.Collections.Generic;
using UnityEngine;

// --- 条件基类：策划可自由扩展各种条件 ---
public abstract class RewardCondition : ScriptableObject
{
    // 判断条件是否满足。可传入reward上下文进行判断
    public abstract bool IsMet(RewardInstance reward);
}

// --- 行为基类：策划可自由扩展各种点击/生成行为 ---
public abstract class RewardAction : ScriptableObject
{
    // 在UI生成选项时调用。用于预先计算代价（如：随机选中一个道具并记录，修改显示文本）
    public virtual void OnGenerate(RewardInstance reward, OptionRuntimeState optionState) { }

    // 在玩家点击选项时调用。执行实际的逻辑（如：扣除道具，发放奖励，改变状态等）
    public abstract void Execute(RewardInstance reward, OptionRuntimeState optionState);
}