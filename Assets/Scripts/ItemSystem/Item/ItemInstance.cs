using System.Collections.Generic;

public class ItemInstance
{
    public ItemData Data { get; private set; }
    public int TotalUpgradeCount { get; private set; }

    public bool IsEquipped { get; private set; }    //是否正在装备中
    public bool HasBeenUnequipped { get; private set; }     // 记录是否被脱下过

    private List<RuntimeEffect> activeEffects = new List<RuntimeEffect>();  //效果列表

    public ItemInstance(ItemData data)
    {
        Data = data;
        TotalUpgradeCount = 0;
        IsEquipped = false;
        HasBeenUnequipped = false;

        // 初始化基础效果，但【不触发】Equip
        foreach (var effectData in data.BaseEffects)
        {
            AddEffect(effectData);
        }
    }

    // 首次拾取时调用（仅触发一次）
    public void TriggerPickUp()
    {
        foreach (var effect in activeEffects) effect.OnPickUp();    // 触发拾取效果
    }

    // 装备道具
    public void Equip()
    {
        if (IsEquipped) return;
        IsEquipped = true;
        foreach (var effect in activeEffects) effect.OnEquip();     // 注册装备中效果，使之生效；触发装备效果
    }

    // 卸下道具
    // force参数用于处理丢弃道具时的强制卸下
    public void Unequip(bool force = false)
    {
        if (!IsEquipped) return;
        if (!force && Data.CannotUnequip) return; // 检查不可脱下限制

        IsEquipped = false;
        HasBeenUnequipped = true; // 标记已被脱下过
        foreach (var effect in activeEffects) effect.OnUnequip();       //注销装备中效果，触发脱下效果
    }

    // 彻底销毁/移除道具时调用
    public void RemoveItemInstance()
    {
        if (IsEquipped) Unequip(true); // 强制卸下
        foreach (var effect in activeEffects) effect.OnRemove();
    }

    public void UseActiveSkill()
    {
        if (Data.Type != ItemType.TypeC_ActiveLimited || !IsEquipped) return;
        foreach (var effect in activeEffects) effect.OnActivate();
    }

    public void UpgradeItem(int upgradeNodeIndex)
    {
        if (upgradeNodeIndex < 0 || upgradeNodeIndex >= Data.UpgradeTree.Count) return;

        UpgradeNode node = Data.UpgradeTree[upgradeNodeIndex];

        foreach (var effectData in node.AdditionalEffects)
        {
            AddEffect(effectData);
        }

        TotalUpgradeCount++;

        if (TotalUpgradeCount == Data.UpgradeMilestoneCount && Data.MilestoneRewardEffect != null)
        {
            AddEffect(Data.MilestoneRewardEffect);
        }
    }

    // 根据一个效果Data，添加一个效果Runtime。注意：一个道具有多个效果，要遍历调用
    private void AddEffect(EffectData effectData)
    {
        RuntimeEffect runtimeEff = effectData.CreateRuntimeEffect();
        activeEffects.Add(runtimeEff);

        // 当后续升级添加新效果时，立刻触发它的 PickUp 事件
        runtimeEff.OnPickUp();

        // 如果道具当前在装备状态，新增的效果立刻生效
        if (IsEquipped)
        {
            runtimeEff.OnEquip();
        }
    }
}