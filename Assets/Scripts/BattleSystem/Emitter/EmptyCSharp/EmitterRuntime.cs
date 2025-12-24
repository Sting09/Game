using System.Collections.Generic;
using UnityEngine;

public abstract class EmitterRuntime
{
    //持有配置文件的引用（只读）
    protected AbstractEmitterConfigSO config;

    //每个发弹点发射的子弹样式
    protected ShootPattern pattern;

    //缓存对象，传给 config 去填充数据
    protected List<Vector3> posBuffer;      //每个发弹点的位置
    protected List<float> dirBuffer;        //每个发弹点的角度

    //发射器事件相关参数
    //static缓存枚举的最大值，避免每次获取
    private static readonly int PropertyCount = System.Enum.GetValues(typeof(EmitterPropertyType)).Length;
    protected float[] propertyOffsets;   //发射器事件修改的参数和修改值，索引为EmitterPropertyType
    protected List<EmitterEventRunner> eventRunners;        //发射器事件运行时列表

    [SerializeField] protected int timesInWave;        //当前波次中的第几次发射
    [SerializeField] protected int waveTimes;            //已经发射了几波弹幕

    public EmitterRuntime(AbstractEmitterConfigSO emitter)
    {
        config = emitter;
        pattern = config.bulletPattern.CreateRuntimePattern(this);
        posBuffer = new List<Vector3>(10); // 给个初始容量，减少扩容开销
        dirBuffer = new List<float>(10);
        propertyOffsets = new float[PropertyCount];

        int eventCount = emitter.emitterEvents?.Count?? 0;
        eventRunners = new List<EmitterEventRunner>(eventCount);    //容量就等于事件个数
        if (emitter.emitterEvents != null)
        {
            foreach (var evt in emitter.emitterEvents)  //为每个事件创建eventRunner
            {
                eventRunners.Add(new EmitterEventRunner(evt, this));
            }
        }
        timesInWave = 0;
        waveTimes = -1;
    }

    /// <summary>
    /// 发射器发射一次弹幕
    /// </summary>
    /// <param name="start">发弹敌人的Transform</param>
    public abstract void Shoot(Transform start, bool newWave = false);


    // 发射器事件相关函数
    public float GetPropertyValue(EmitterPropertyType type, float baseValue)
    {
        return baseValue + propertyOffsets[(int)type];
    }

    public void SetPropertyOffset(EmitterPropertyType type, float value)
    {
        propertyOffsets[(int)type] = value;
    }

    public void UpdatePropertyOffset(EmitterPropertyType type, float value)
    {
        propertyOffsets[(int)type] += value;
    }

    public float GetPropertyOffset(EmitterPropertyType type)
    {
        return propertyOffsets[(int)type];
    }


    /// <summary>
    /// 更新发射器事件，由ShooterTimer每帧调用
    /// </summary>
    /// <param name="dt">deltaTime</param>
    public void UpdateEventRunners(float dt)
    {
        for (int i = eventRunners.Count - 1; i >= 0; i--)
        {
            eventRunners[i].Tick(dt);   // 调用每个事件，让它们自己更新
            if (eventRunners[i].IsFinished())
            {
                //运行完移除，Swap Removal ( O(1)删除)
                int lastIndex = eventRunners.Count - 1;
                if (i < lastIndex)
                {
                    eventRunners[i] = eventRunners[lastIndex];
                }
                eventRunners.RemoveAt(lastIndex);
            }
        }
    }
}
