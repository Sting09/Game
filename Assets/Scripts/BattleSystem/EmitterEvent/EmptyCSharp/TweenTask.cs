using System.Collections.Generic;
using UnityEngine;

public class TweenTask
{
    public EmitterRuntime runtime;
    public EmitterPropertyType property;    //要改哪个属性
    public EventModificationType type;      //修改类型：变化至 / 增加 / 减少
    public float startValue;                //创建任务时，属性offset的起始值
    public float endValue;                  //创建任务时，属性offset的目标结束值
    public float currentOffset;             //这个任务当前造成了多少offset
    public float duration;                  //多长时间变化完
    public AnimationCurve curve;            //变化方式曲线
    public float saveTime;
    public int saveIndex;
    public float currentTime;

    public bool IsActive;                   //是否正在处理任务

    public void Init(EmitterRuntime runtime, EmitterPropertyType property, EventModificationType type, float start, float end, float duration, AnimationCurve curve, float saveTime, int saveIndex)
    {
        this.runtime = runtime;
        this.property = property;
        this.type = type;
        this.startValue = start;
        this.endValue = end;
        currentOffset = 0;
        this.duration = duration;
        this.curve = curve;
        this.currentTime = 0;
        this.IsActive = true;
        this.saveTime = saveTime;
        this.saveIndex = saveIndex;
    }

    public void Reset()
    {
        runtime = null;
        curve = null;
        IsActive = false;
    }

    // 返回 true 表示结束
    public bool Tick(float dt)
    {
        if(!IsActive) { return true;  }

        if (duration <= 0)      //小于等于0，直接变
        {
            currentOffset = endValue - startValue;
            switch (type)
            {
                case (EventModificationType.ChangeTo):
                    runtime.SetPropertyOffset(property, endValue);
                    break;
                case EventModificationType.Add:
                    runtime.UpdatePropertyOffset(property, currentOffset);
                    break;
                case EventModificationType.Reduce:
                    runtime.UpdatePropertyOffset(property, currentOffset); //减就是加的差值取反
                    break;
                default:
                    break;
            }
            IsActive = false;
            return true;
        }

        currentTime += dt;
        float t = Mathf.Clamp01(currentTime / duration);
        float curveValue = curve.Evaluate(t);
        float delta = endValue - startValue;
        float currentDelta;

        //根据曲线计算当前值
        switch (type)
        {
            case (EventModificationType.ChangeTo):
                float currentVal = Mathf.LerpUnclamped(startValue, endValue, curveValue);
                currentOffset = currentVal - startValue;
                runtime.SetPropertyOffset(property, currentVal);
                break;
            case EventModificationType.Add:
                currentDelta = Mathf.LerpUnclamped(0, delta, curveValue);
                runtime.UpdatePropertyOffset(property, currentDelta - currentOffset);
                currentOffset = currentDelta;
                break;
            case EventModificationType.Reduce:
                currentDelta = Mathf.LerpUnclamped(0, delta, curveValue);
                runtime.UpdatePropertyOffset(property, currentDelta - currentOffset);
                currentOffset = currentDelta;
                break;
            default:
                break;
        }

        bool needSave = currentTime >= saveTime && saveTime >= 0;
        if (needSave)
        {
            if(saveIndex < BattleManager.Instance.globalParameter.Count)
                BattleManager.Instance.globalParameter[saveIndex] = runtime.GetPropertyOffset(property);
        }

        bool finished = currentTime >= duration;
        if (finished)
        {
            IsActive = false;
            return true;
        }
        return false;
    }
}


// 简单的静态对象池
public static class TweenTaskPool
{
    private static Stack<TweenTask> pool = new Stack<TweenTask>(64);

    public static TweenTask Get()
    {
        if (pool.Count > 0) return pool.Pop();
        return new TweenTask(); // 只有池空了才新建，预热后基本不会发生
    }

    public static void Return(TweenTask task)
    {
        task.Reset(); // 归还前或取出后重置
        pool.Push(task);
    }
}
