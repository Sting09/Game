using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class ItemPool
{
    public string poolName;
    public List<ItemData> allItems;
}

public class InventoryManager : SingletonMono<InventoryManager>
{
    [Header("配置")]
    public int maxPassiveLimitedSlots = 6; // 有限被动槽位上限

    public List<ItemPool> allItemPools;

    [Header("玩家当前装备槽位")]
    public List<ItemInstance> passiveLimitedItems = new List<ItemInstance>();
    public List<ItemInstance> passiveInfiniteItems = new List<ItemInstance>();
    public ItemInstance currentActiveItem;

    [Header("玩家仓库")]
    public List<ItemInstance> inventoryPassiveItems = new List<ItemInstance>();
    public List<ItemInstance> inventoryActiveItems = new List<ItemInstance>();

    // 缓存字典，加速查找，避免遍历匹配
    private Dictionary<int, ItemData> itemDictByID = new Dictionary<int, ItemData>();
    private Dictionary<string, ItemData> itemDictByName = new Dictionary<string, ItemData>();

    protected override void Awake()
    {
        base.Awake();

        // 游戏启动时构建一次字典，后续零分配
        if (allItemPools != null)
        {
            for (int i = 0; i < allItemPools.Count; i++)
            {
                var pool = allItemPools[i];
                if (pool.allItems == null) continue;

                for (int j = 0; j < pool.allItems.Count; j++)
                {
                    var item = pool.allItems[j];
                    if (item == null) continue;

                    itemDictByID[item.ItemID] = item;
                    itemDictByName[item.ItemName] = item;
                }
            }
        }
    }

    // 测试
    [ContextMenu("获得测试道具【磨刀石】")]
    public void GetItem_1() { AddItem(1);}
    [ContextMenu("移除测试道具【磨刀石】")]
    public void RemoveItem_1() { RemoveItem(1); }
    [ContextMenu("获得测试道具【护甲】")]
    public void GetItem_2() { AddItem(2); }
    [ContextMenu("移除测试道具【护甲】")]
    public void RemoveItem_2() { RemoveItem(2); }


    /// <summary>
    /// 添加指定道具给玩家
    /// </summary>
    public void AddItem(ItemData item)
    {
        if (item == null) return;

        // 如果拾取时强制穿戴，先检查能否拾取
        if (item.AutoEquipOnPickup)
        {
            // 已穿戴道具达到上限，从0号道具开始查找，脱下第一个能脱下的道具
            if (item.Type == ItemType.TypeA_PassiveLimited && passiveLimitedItems.Count >= maxPassiveLimitedSlots)
            {
                bool allLocked = true;
                for (int i = 0; i < passiveLimitedItems.Count; i++)
                {
                    if (!passiveLimitedItems[i].Data.CannotUnequip)
                    {
                        allLocked = false;
                        break;
                    }
                }
                // 都不能脱下，说明无法拾取
                if (allLocked)
                {
                    Debug.LogWarning("槽位已满且所有道具均不可脱下，无法拾取: " + item.ItemName);
                    return;
                }
            }
            else if (item.Type == ItemType.TypeC_ActiveLimited && currentActiveItem != null)
            {
                if (currentActiveItem.Data.CannotUnequip)
                {
                    Debug.LogWarning("主动道具槽被锁定（不可脱下），无法拾取: " + item.ItemName);
                    return;
                }
            }
        }

        // 1. 实例化并触发首次拾取效果
        ItemInstance instance = new ItemInstance(item);
        //触发拾取效果，只触发一次
        instance.TriggerPickUp();

        // 2. 根据类型分类处理
        switch (item.Type)
        {
            //无限被动道具直接装备，触发装备效果
            case ItemType.TypeB_PassiveInfinite:
                passiveInfiniteItems.Add(instance);
                instance.Equip();
                break;
            //有限被动道具，如果强制穿戴则触发装备效果，否则加入仓库
            case ItemType.TypeA_PassiveLimited:
                if (item.AutoEquipOnPickup) EquipItemFromInventory(instance, true);
                else inventoryPassiveItems.Add(instance);
                break;
            //有限主动道具，如果强制穿戴则触发装备效果，否则加入仓库
            case ItemType.TypeC_ActiveLimited:
                if (item.AutoEquipOnPickup) EquipItemFromInventory(instance, true);
                else inventoryActiveItems.Add(instance);
                break;
        }
    }

    public void AddItem(int index)
    {
        if (itemDictByID.TryGetValue(index, out ItemData item)) AddItem(item);
        else Debug.LogError($"找不到 ID 为 {index} 的道具");
    }

    public void AddItem(string name)
    {
        if (itemDictByName.TryGetValue(name, out ItemData item)) AddItem(item);
        else Debug.LogError($"找不到 Name 为 {name} 的道具");
    }

    // ================= 装备与卸下流转 =================

    public bool EquipItemFromInventory(ItemInstance instance, bool isNewPickup = false)
    {
        if (instance.Data.CannotEquip) return false;
        if (instance.Data.CannotEquipAfterUnequip && instance.HasBeenUnequipped) return false;
        if (instance.IsEquipped) return true;

        // 有限被动道具
        if (instance.Data.Type == ItemType.TypeA_PassiveLimited)
        {
            // 达到上限则需替换，找到第一个可卸下的道具
            if (passiveLimitedItems.Count >= maxPassiveLimitedSlots)
            {
                ItemInstance toReplace = null;
                for (int i = 0; i < passiveLimitedItems.Count; i++)
                {
                    if (!passiveLimitedItems[i].Data.CannotUnequip)
                    {
                        toReplace = passiveLimitedItems[i];
                        break;
                    }
                }
                // 卸下要替换的道具
                if (toReplace != null)
                {
                    UnequipItemToInventory(toReplace);
                }
                else return false; // 全部锁定
            }

            //不是新捡起的道具要从仓库移除
            if (!isNewPickup) inventoryPassiveItems.Remove(instance);
            passiveLimitedItems.Add(instance);
            //触发穿戴效果
            instance.Equip();
            return true;
        }
        else if (instance.Data.Type == ItemType.TypeC_ActiveLimited)
        {
            if (currentActiveItem != null)
            {
                if (currentActiveItem.Data.CannotUnequip) return false;
                UnequipItemToInventory(currentActiveItem);
            }

            if (!isNewPickup) inventoryActiveItems.Remove(instance);
            currentActiveItem = instance;
            instance.Equip();
            return true;
        }

        return false;
    }

    public bool UnequipItemToInventory(ItemInstance instance)
    {
        if (instance.Data.CannotUnequip) return false;
        if (!instance.IsEquipped) return true;

        if (instance.Data.Type == ItemType.TypeA_PassiveLimited)
        {
            passiveLimitedItems.Remove(instance);
            inventoryPassiveItems.Add(instance);
            instance.Unequip();
            return true;
        }
        else if (instance.Data.Type == ItemType.TypeC_ActiveLimited && currentActiveItem == instance)
        {
            currentActiveItem = null;
            inventoryActiveItems.Add(instance);
            instance.Unequip();
            return true;
        }

        return false;
    }

    // ================= 移除/丢弃逻辑 =================

    public void RemoveItem(ItemData item, bool removeAll = false)   //removeAll表示有复数道具时，全部移除还是移除一个
    {
        if (item == null) return;

        if (removeAll)
        {
            // 如果移除全部，采用倒序遍历直接在原列表删除，避免生成临时List去记录要删除的项
            RemoveAllMatchedInList(passiveLimitedItems, item);
            RemoveAllMatchedInList(passiveInfiniteItems, item);
            RemoveAllMatchedInList(inventoryPassiveItems, item);
            RemoveAllMatchedInList(inventoryActiveItems, item);

            if (currentActiveItem != null && currentActiveItem.Data == item)
            {
                currentActiveItem.RemoveItemInstance();
                currentActiveItem = null;
            }
        }
        else
        {
            // 如果只移除一个（优先低等级），我们只记录引用而不分配新List
            ItemInstance targetToRemove = null;
            List<ItemInstance> listContainingTarget = null; // 记录它在哪个列表里，方便直接Remove

            CheckLowestUpgradeInList(passiveLimitedItems, item, ref targetToRemove, ref listContainingTarget);
            CheckLowestUpgradeInList(passiveInfiniteItems, item, ref targetToRemove, ref listContainingTarget);
            CheckLowestUpgradeInList(inventoryPassiveItems, item, ref targetToRemove, ref listContainingTarget);
            CheckLowestUpgradeInList(inventoryActiveItems, item, ref targetToRemove, ref listContainingTarget);

            if (currentActiveItem != null && currentActiveItem.Data == item)
            {
                if (targetToRemove == null || currentActiveItem.TotalUpgradeCount < targetToRemove.TotalUpgradeCount)
                {
                    targetToRemove = currentActiveItem;
                    listContainingTarget = null; // 标记为在独立槽位中
                }
            }

            // 执行真正的销毁逻辑
            if (targetToRemove != null)
            {
                targetToRemove.RemoveItemInstance();

                if (listContainingTarget != null)
                {
                    listContainingTarget.Remove(targetToRemove);
                }
                else if (targetToRemove == currentActiveItem)
                {
                    currentActiveItem = null;
                }
            }
        }
    }

    public void RemoveItem(int index, bool removeAll = false)
    {
        if (itemDictByID.TryGetValue(index, out ItemData item)) RemoveItem(item, removeAll);
    }

    public void RemoveItem(string name, bool removeAll = false)
    {
        if (itemDictByName.TryGetValue(name, out ItemData item)) RemoveItem(item, removeAll);
    }

    // ================= 性能辅助方法 (零分配) =================

    private void RemoveAllMatchedInList(List<ItemInstance> list, ItemData item)
    {
        // 倒序遍历允许我们在迭代时直接调用 RemoveAt，安全且无额外分配
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].Data == item)
            {
                list[i].RemoveItemInstance();
                list.RemoveAt(i);
            }
        }
    }

    private void CheckLowestUpgradeInList(List<ItemInstance> list, ItemData item, ref ItemInstance currentTarget, ref List<ItemInstance> targetList)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var instance = list[i];
            if (instance.Data == item)
            {
                if (currentTarget == null || instance.TotalUpgradeCount < currentTarget.TotalUpgradeCount)
                {
                    currentTarget = instance;
                    targetList = list;
                }
            }
        }
    }
}