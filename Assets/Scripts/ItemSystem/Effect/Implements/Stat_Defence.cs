using UnityEngine;

// ==========================================
// 1. 效果静态数据配置类 (ScriptableObject)
// ==========================================
[CreateAssetMenu(fileName = "Stat_DefenceEffect", menuName = "Item/Effects/【属性】改变固定伤害减免")]
public class Stat_DefenceEffectData : EffectData
{
    [Header("属性修改配置")]
    [Tooltip("增加的攻击力数值，负数表示降低")]
    public float defenceAmount = 1;

    // 核心：工厂方法，当道具被实例化时，根据此SO生成对应的运行时效果
    public override RuntimeEffect CreateRuntimeEffect()
    {
        return new Stat_DefenceRuntimeEffect(this);
    }
}

// ==========================================
// 2. 效果运行时逻辑类 (无 MonoBehavior，纯 C# 类)
// ==========================================
public class Stat_DefenceRuntimeEffect : RuntimeEffect
{
    // 持有静态数据的引用，方便读取配表数值
    private Stat_DefenceEffectData data;

    // 构造函数，由 CreateRuntimeEffect() 调用
    public Stat_DefenceRuntimeEffect(Stat_DefenceEffectData effectData)
    {
        this.data = effectData;
    }

    public override void OnPickUp()
    {
        // 首次拾取时的逻辑。纯属性修饰道具这里通常不需要操作。
        // （如果是“拾取时获得100金币”的道具，逻辑写在这里）
    }

    public override void OnEquip()
    {
        // 【核心逻辑：装备时加属性】
        StatModifier mod = new StatModifier(data.defenceAmount, StatModType.Flat, this);
        GameManager.Instance.player.playerStats.defence.AddModifier(mod);

        Debug.Log($"玩家伤害减免 +{data.defenceAmount}");
    }

    public override void OnUnequip()
    {
        // 【核心逻辑：卸下时扣属性】
        GameManager.Instance.player.playerStats.defence.RemoveAllModifiersFromSource(this);
        Debug.Log($"玩家伤害减免 -{data.defenceAmount}");
    }

    public override void OnActivate()
    {
        // 这是被动道具，主动释放技能对它无效，留空
    }

    public override void OnRemove()
    {
        // 彻底销毁时的清理逻辑。
        // 注意：InventoryManager 在调用 RemoveItemInstance 时，会先调用 Unequip()。
        // 所以这里不需要再次扣除攻击力，通常用于注销某些委托或事件监听。
    }
}