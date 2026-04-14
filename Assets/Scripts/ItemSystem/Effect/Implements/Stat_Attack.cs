using UnityEngine;

// ==========================================
// 1. 效果静态数据配置类 (ScriptableObject)
// ==========================================
[CreateAssetMenu(fileName = "Stat_AttackEffect", menuName = "Item/Effects/【属性】改变攻击力")]
public class Stat_AttackEffectData : EffectData
{
    [Header("属性修改配置")]
    [Tooltip("增加的攻击力数值，负数表示降低")]
    public float attackAmount = 1;

    // 核心：工厂方法，当道具被实例化时，根据此SO生成对应的运行时效果
    public override RuntimeEffect CreateRuntimeEffect()
    {
        return new Stat_AttackRuntimeEffect(this);
    }
}

// ==========================================
// 2. 效果运行时逻辑类 (无 MonoBehavior，纯 C# 类)
// ==========================================
public class Stat_AttackRuntimeEffect : RuntimeEffect
{
    // 持有静态数据的引用，方便读取配表数值
    private Stat_AttackEffectData data;

    // 构造函数，由 CreateRuntimeEffect() 调用
    public Stat_AttackRuntimeEffect(Stat_AttackEffectData effectData)
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
        // 假设外部有一个单例或事件总线管理玩家属性，调用其方法增加攻击力。
        // 自拟外部类：PlayerStatsManager.Instance.ChangeAttack(int amount)
        StatModifier mod = new StatModifier(data.attackAmount, StatModType.Flat, this);
        GameManager.Instance.player.playerStats.damage.AddModifier(mod);

        Debug.Log($"玩家攻击力 +{data.attackAmount}");
    }

    public override void OnUnequip()
    {
        // 【核心逻辑：卸下时扣属性】
        // 传入负值，将之前加的攻击力扣除

        // PlayerStatsManager.Instance.ChangeAttack(-data.attackAmount);
        GameManager.Instance.player.playerStats.damage.RemoveAllModifiersFromSource(this);
        Debug.Log($"玩家攻击力 -{data.attackAmount}");
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