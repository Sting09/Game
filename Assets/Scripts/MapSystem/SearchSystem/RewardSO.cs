using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewReward", menuName = "Reward/RewardSO")]
public class RewardSO : ScriptableObject
{
    public string rewardName;
    public int weight = 100;
    public RewardState defaultState = RewardState.Opened;

    [Header("出现条件 (Spawn Conditions)")]
    public List<RewardCondition> mustAppearConditions;
    public List<RewardCondition> mustNotAppearConditions; [Header("状态提示文本 (State Texts)")]
    [TextArea] public string unknownText = "发现了一个未知的奖励。";
    [TextArea] public string openedText = "奖励已开启。";
    [TextArea] public string closedText = "这里什么都没有了。";

    [Header("不同状态下的选项配置")]
    public List<RewardOptionDef> unknownOptions;
    public List<RewardOptionDef> openedOptions;
    // Closed状态按需求没有任何选项，无需配置
}

// 选项配置定义
[System.Serializable]
public class RewardOptionDef
{
    public string optionID; // 用于代码中识别或互相排斥
    [TextArea] public string rawText; // 原始描述，例如 "交出 {0} 来开启"

    [Header("条件")]
    public List<RewardCondition> visibilityConditions; // 可见条件
    public List<RewardCondition> interactableConditions; // 可点击条件

    [Header("行为")]
    public List<RewardAction> actions; // 包含生成时预处理和点击时执行的行为
}