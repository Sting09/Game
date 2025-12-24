using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class EmitterEventRunner
{
    private EmitterEventSO config;          //用于配置的发射器事件SO文件
    private EmitterRuntime targetRuntime;   //所属的发射器运行时

    private float timer;        //检查事件是否执行的计时器
    private int executedCount;  //已检查的次数
    private bool isRunning;     //是否正在运行。暂时用不上，先保留着，时停一类功能用得上

    // 正在进行的缓动任务列表
    private List<TweenTask> activeTweens;

    /// <summary>
    /// 返回事件是否已经执行完毕
    /// </summary>
    public bool IsFinished()
    {
        if (config.interval < 0)
        {
            return executedCount > 0 && activeTweens.Count == 0;
        }
        else
        {
            if (config.loopCount == 0)
            {
                return false;
            }
            else
            {
                return executedCount >= config.loopCount && activeTweens.Count == 0;
            }
        }
    }

    public EmitterEventRunner(EmitterEventSO config, EmitterRuntime runtime)
    {
        this.config = config;
        this.targetRuntime = runtime;
        this.timer = 0f;
        this.executedCount = 0;
        this.activeTweens = new List<TweenTask>(16);  //预分配 List 容量，减少扩容 GC
        this.isRunning = true;
    }

    /// <summary>
    /// 发射器事件运行时进行一次检查，检查是否需要执行事件，执行已经启动的事件（EmitterRunner的UpdateEventRunners里调用）
    /// </summary>
    /// <param name="dt">deltaTime</param>
    public void Tick(float dt)
    {
        if (!isRunning) return;

        timer += dt;

        // timer累积到多少才执行事件
        float triggerTime = executedCount == 0 ? config.startDelay : config.interval;

        // 如果 interval < 0，说明只执行一次，不需要再检测时间
        bool canTrigger = (config.interval >= 0 || executedCount == 0);


        // 检查是否需要执行事件
        if (canTrigger && timer >= triggerTime)
        {
            // 检查次数限制
            if (config.loopCount <= 0 || executedCount < config.loopCount)
            { 
                TryExecuteEvent();  //检查事件条件，执行事件
                timer -= triggerTime; // 重置计时器
            }
        }

        // 执行已经启动的事件
        for (int i = activeTweens.Count - 1; i >= 0; i--)   //倒序，方便Remove
        {
            bool finished = activeTweens[i].Tick(dt);
            if (finished)
            {
                // 归还对象池
                TweenTaskPool.Return(activeTweens[i]);

                // Swap Removal ( O(1)删除 )
                int lastIndex = activeTweens.Count - 1;
                if (i < lastIndex)
                {
                    activeTweens[i] = activeTweens[lastIndex];
                }
                activeTweens.RemoveAt(lastIndex);
            }
        }
    }

    private void TryExecuteEvent()
    {
        // 这里可以接入 Condition 检查
        // if (!CheckConditions()) return;

        executedCount++;

        foreach (var action in config.actions)
        {
            // 从池中取一个缓动任务
            TweenTask task = TweenTaskPool.Get();

            float startVal = targetRuntime.GetPropertyOffset(action.targetProperty);
            float endVal = action.modificationType switch
            { 
                EventModificationType.ChangeTo => action.value,
                EventModificationType.Add => startVal + action.value,
                EventModificationType.Reduce => startVal - action.value,
                _ => startVal
            };

            task.Init(
                targetRuntime,
                action.targetProperty,
                action.modificationType,
                startVal,
                endVal,
                action.duration,
                action.curve
            );

            activeTweens.Add(task);
        }
    }
}