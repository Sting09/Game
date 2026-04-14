using UnityEngine;

// ================= 效果静态数据 (配置用) =================
public abstract class EffectData : ScriptableObject
{
    [TextArea] public string Description;

    // 生成对应的运行时效果
    public abstract RuntimeEffect CreateRuntimeEffect();
}

// ================= 效果运行时逻辑 (带状态，如冷却时间) =================
public abstract class RuntimeEffect
{
    protected bool isActive = true;

    // 当效果被拾取时调用 (例如：注册事件)
    public abstract void OnPickUp();
    // 当效果被装备时调用 (例如：增加属性)
    public abstract void OnEquip();
    // 当效果被卸下时调用 (例如：扣除属性)
    public abstract void OnUnequip();

    // 主动技能调用 (只有主动道具才会用到)
    public virtual void OnActivate() { }
    // 当效果被移除时调用 (例如：注销事件)
    public abstract void OnRemove();
}