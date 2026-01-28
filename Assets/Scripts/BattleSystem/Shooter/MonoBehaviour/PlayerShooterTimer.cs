using UnityEngine;

/// <summary>
/// Shooter判断到没到时机，发射Emitter编写的弹幕
/// </summary>
public class PlayerShooterTimer
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
    public PlayerShooterTimer(AbstractEmitterConfigSO emitter)
    {
        intervalTimer = emitter.shootInterval;     //初始等于shootInterval，使发射器一开始就发射一次
        waveTimer = 0f;
        timesShotThisWave = 0;
        duringWave = false;
        config = emitter;
        runtime = emitter.CreateRuntime();
    }

    /// <summary>
    /// 每帧调用，更新发射器事件、检查当前帧是否需要发射弹幕
    /// </summary>
    /// <param name="deltaTime">当前帧的deltaTime</param>
    /// <param name="danmakuTimer">该发射器所属弹幕的计时器</param>
    public void Tick(float deltaTime, float danmakuTimer, Transform start)
    {
        if (config.shootInterval <= 0) return;

        // 修复1：使用 >= 防止丢失第一帧
        bool isStart = danmakuTimer >= config.shootDelay;
        bool notEnd = config.emitterDuration < 0 || danmakuTimer <= config.emitterDuration;

        if (isStart && notEnd)
        {
            // 核心修复：使用循环处理同一帧内可能发生的多次状态切换
            // (例如：发射结束 -> 瞬间完成等待 -> 再次发射)
            // 只有当时间被消耗完，或者进入了真正的等待状态时才停止
            bool stateRunning = true;

            // 只有在第一次进入循环时才消耗 deltaTime，后续的循环使用的是余量
            float currentDelta = deltaTime;

            while (stateRunning)
            {
                stateRunning = false; // 默认跑完这一次逻辑就退出，除非发生状态切换

                if (duringWave) // 处于波间等待
                {
                    waveTimer += currentDelta;
                    currentDelta = 0f; // 时间已累加，后续循环不再重复累加 deltaTime

                    if (waveTimer >= config.waveInterval)
                    {
                        duringWave = false;
                        timesShotThisWave = 0;
                        // 将等待多出的时间补偿给发射计时器
                        intervalTimer = waveTimer - config.waveInterval + config.shootInterval;
                        waveTimer = 0f;

                        stateRunning = true; // 状态发生改变，继续在当前帧处理发射逻辑
                    }
                }
                else // 处于发射状态
                {
                    intervalTimer += currentDelta;
                    currentDelta = 0f; // 时间已累加

                    while (intervalTimer >= config.shootInterval && config.shootInterval > 0)
                    {
                        intervalTimer -= config.shootInterval;
                        timesShotThisWave++;
                        runtime.Shoot(start, timesShotThisWave == 1);

                        if (timesShotThisWave >= config.shootTimesPerWave)
                        {
                            // 发射完毕，进入等待
                            waveTimer = intervalTimer; // 将发射多出的时间补偿给等待计时器
                            intervalTimer = 0f;
                            duringWave = true;

                            stateRunning = true; // 状态改变，继续在当前帧检查波间等待逻辑(万一等待时间是0)
                            break; // 跳出发射的 while，回到外层的 state while
                        }
                    }
                }
            }


            //先发射子弹，再更新发射器事件，保证第一发子弹满足编辑的样式
            runtime.UpdateEventRunners(deltaTime);
        }
    }
}
