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

    [SerializeField] protected int timesInWave;        //当前波次中的第几次发射
    [SerializeField] protected int waveTimes;            //已经发射了几波弹幕

    public EmitterRuntime(AbstractEmitterConfigSO emitter)
    {
        config = emitter;
        pattern = config.bulletPattern.CreateRuntimePattern();
        posBuffer = new List<Vector3>(10); // 给个初始容量，减少扩容开销
        dirBuffer = new List<float>(10);
        timesInWave = 0;
        waveTimes = -1;
    }

    /// <summary>
    /// 发射器发射一次弹幕
    /// </summary>
    /// <param name="start">发弹敌人的Transform</param>
    public abstract void Shoot(Transform start, bool newWave = false);
}
