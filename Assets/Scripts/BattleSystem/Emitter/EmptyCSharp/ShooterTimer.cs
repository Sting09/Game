using UnityEngine;

/// <summary>
/// Shooter判断到没到时机，发射Emitter编写的弹幕
/// </summary>
public class ShooterTimer
{
    //计时与计数
    public float intervalTimer;
    public float waveTimer;
    public int timesShotThisWave;   //当前波次已发射几次
    public bool duringWave;

    public AbstractEmitterConfigSO config;
    public EmitterRuntime runtime;

    /// <summary>
    /// 使用emitterSO构造计时器
    /// </summary>
    /// <param name="emitter">emitterSO</param>
    public ShooterTimer(AbstractEmitterConfigSO emitter)
    {
        intervalTimer = emitter.shootInterval;     //初始等于shootInterval，使发射器一开始就发射一次
        waveTimer = 0f;
        timesShotThisWave = 0;
        duringWave = false;

        config = emitter;
        runtime = emitter.CreateRuntime();
    }

    /// <summary>
    /// 每帧调用，检查当前帧是否需要发射弹幕
    /// </summary>
    /// <param name="deltaTime">当前帧的deltaTime</param>
    /// <param name="danmakuTimer">该发射器所属弹幕的计时器</param>
    public void Tick(float deltaTime, float danmakuTimer, Transform start)
    {
        bool isStart = danmakuTimer > config.shootDelay;
        //duration为负数时永不结束
        bool notEnd = config.emitterDuration < 0 || danmakuTimer <= config.emitterDuration;

        if (isStart && notEnd)
        {
            if (duringWave)     //处于波次之间的间隔
            {
                waveTimer += deltaTime;
                if (waveTimer >= config.waveInterval)
                {
                    duringWave = false;     //波次间的暂停结束
                    timesShotThisWave = 0;
                    //+ config.shootInterval是为了让下一波子弹的第一次发射能立即执行
                    intervalTimer = waveTimer - config.waveInterval + config.shootInterval;
                    waveTimer = 0f;
                    deltaTime = 0f;     //为了处理一帧中duringWave从True到False的情况
                }
            }

            // 这里不用 else。这样如果上面的 if 结束了，立刻就能进入发射逻辑
            if (!duringWave)
            {
                intervalTimer += deltaTime;
                // 使用while处理卡顿导致一帧要发射两次的情况
                while (intervalTimer >= config.shootInterval)
                {
                    intervalTimer -= config.shootInterval;
                    timesShotThisWave++;
                    runtime.Shoot(start, timesShotThisWave == 1);
                    if (timesShotThisWave >= config.shootTimesPerWave)  //发射够次数，进入波间暂停
                    {
                        waveTimer = intervalTimer;  //剩余时间加到waveTimer上
                        intervalTimer = 0f;
                        duringWave = true;
                        break;                      //跳出while循环
                    }
                }
            }
        }
    }
}
