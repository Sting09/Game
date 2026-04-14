using UnityEngine;
using System.Collections.Generic;

public enum ItemType
{
    TypeA_PassiveLimited,  // 被动，有上限 (放背包或仓库)
    TypeB_PassiveInfinite, // 被动，无上限 (拾取即生效)
    TypeC_ActiveLimited    // 主动技能，有上限
}

[System.Serializable]
public class UpgradeNode
{
    public string NodeName;
    public int MaxUpgradeCount = 1;
    public List<EffectData> AdditionalEffects;
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Item/Item")]
public class ItemData : ScriptableObject
{
    public int ItemID;
    public string ItemName;
    public ItemType Type;
    public Sprite Icon;
    [TextArea] public string Description;

    [Header("装备限制 (策划配置)")]
    public bool AutoEquipOnPickup;         // 一旦拾取就立即装备
    public bool CannotUnequip;             // 一旦装备就不能脱下（如诅咒道具）
    public bool CannotEquipAfterUnequip;   // 一旦脱下就不能再次装备（如一次性消耗品）
    public bool CannotEquip;               // 根本不可装备（只能在仓库生效，或纯材料）

    [Header("基础效果 (0级时拥有)")]
    public List<EffectData> BaseEffects;

    [Header("升级路线")]
    public List<UpgradeNode> UpgradeTree;

    [Header("全局累计奖励")]
    public int UpgradeMilestoneCount = 3;
    public EffectData MilestoneRewardEffect;
}