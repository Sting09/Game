#if False
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class BaseEmitter : MonoBehaviour
{
    public List<DanmakuSO> danmakuToShoot;     //该敌人要发射的弹幕
    private int currentDanmakuIndex = 0;       //当前正在发射第几个弹幕

    public float danmakuTimer;                  //当前符卡的计时器
    public float currentDanmakuDuration;        //当前符卡的持续时间

    public List<ShooterTimer> timers;           //每个计时器

    public Transform playerTransform;           //玩家Transform的引用

    void Update()
    {
        danmakuTimer += Time.deltaTime;
        if(danmakuTimer > currentDanmakuDuration )
        {
            //已发射时间比符卡持续时间长，当前符卡停止
            OnDanmakuFinish();
            return;
        }

        //检查每个发射器要不要执行发射
        foreach(ShooterTimer timer in timers )
        {
            if(danmakuTimer > timer.delay && danmakuTimer <= timer.duration)
            {
                CheckAction(timer);
            }
        }
    }

    private void Awake()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
    }

    private void Start()
    {
        currentDanmakuIndex = 0;    //初始化，从第一个弹幕开始发射
        
        LoadDanmaku();
    }

    //加载弹幕发射信息。敌人生成和切换符卡时调用。
    public void LoadDanmaku()
    {
        //清空上一个弹幕的信息
        danmakuTimer = 0;
        timers = new List<ShooterTimer>();

        if (currentDanmakuIndex >= danmakuToShoot.Count)
        {
            Debug.Log("符卡索引超出配置数量。请检查敌人弹幕配置");
            return;
        }
        DanmakuSO currentDanmaku = danmakuToShoot[currentDanmakuIndex]; //当前正在发射的弹幕

        List<BaseShooterParametersSO> currentShooters = currentDanmaku.shooterList;
        //为每一个发射器添加一个计时器
        foreach(BaseShooterParametersSO shooter in currentShooters)
        {
            ShooterTimer timer = new();
            timer.duration = shooter.shootDuration;
            timer.delay = shooter.shootDealy;
            timer.interval = shooter.shootInterval;
            timer.waveInterval = shooter.waveInterval;
            timer.timesThisWave = shooter.shootTimesPerWave;

            timer.intervalTimer = 0f;
            timer.waveTimer = 0f;
            timer.times = 0;
            timer.duringWave = false;

            timer.shooterSO = shooter;

            timers.Add(timer);
        }


        currentDanmakuDuration = currentDanmaku.duration;   //读取符卡持续时间
        danmakuTimer = 0f;      //重置计时器
    }

    void CallShoot(BaseShooterParametersSO shooter, bool newWave = false)
    {
        float x = 0f, y = 0f;
        Vector3 anglePosition = Vector3.zero;

        switch (shooter.baseShootPointPosX)
        {
            case (PositionType.Self):
                x = transform.position.x;
                break;
            case (PositionType.Player):
                x = playerTransform.position.x;
                break;
            case (PositionType.Object):
                //暂不实现。有需要时可以使用Tag实现
                break;
            case (PositionType.CertainValue):
                x = shooter.valueShootPointPosX;
                break;
        }

        switch (shooter.baseShootPointPosY)
        {
            case (PositionType.Self):
                y = transform.position.y;
                break;
            case (PositionType.Player):
                y = playerTransform.position.y;
                break;
            case (PositionType.Object):
                //暂不实现。有需要时可以使用Tag实现
                break;
            case (PositionType.CertainValue):
                y = shooter.valueShootPointPosY;
                break;
        }

        switch (shooter.shootDirectionType)
        {
            case (DirectionType.Player):
                //朝玩家发射，传递玩家坐标
                anglePosition = playerTransform.position;
                break;
            case (DirectionType.CertainValue):
                //固定值，在SO里计算
                break;
            case (DirectionType.Object):
                //暂未实现
                break;
        }

        shooter.Shoot(new Vector3(x, y, 0), anglePosition, newWave);
    }

    //符卡结束
    private void OnDanmakuFinish()
    {
        currentDanmakuIndex ++;
        if (currentDanmakuIndex >= danmakuToShoot.Count)    //配置的弹幕都发射完了
        {
            gameObject.SetActive(false);    //敌人失活，或者以后改成敌人死亡动画等
        }
        else
        {
            LoadDanmaku();      //还有符卡要发射，就加载配置
        }
    }

    public void CheckAction(ShooterTimer timer)
    {
        /*timer += Time.deltaTime;
        if (timer >= fireRate)
        {
            Shoot();
            timer = 0;
        }*/
        if (timer.duringWave)     //处于波次之间的间隔
        {
            timer.waveTimer += Time.deltaTime;
            if (timer.waveTimer >= timer.waveInterval)
            {
                timer.duringWave = false;     //波次间的暂停结束
                timer.times = 0;
                timer.intervalTimer = timer.waveTimer - timer.waveInterval;
                timer.waveTimer = 0f;
            }
        }
        else     //处于一波之中
        {
            timer.intervalTimer += Time.deltaTime;
            if (timer.intervalTimer >= timer.interval)
            {
                timer.intervalTimer -= timer.interval;
                if (timer.times == 0)
                {
                    CallShoot(timer.shooterSO, true);
                }
                else
                {
                    CallShoot(timer.shooterSO);
                }

                timer.times++;
                if (timer.times >= timer.timesThisWave)  //发射够次数，进入波间暂停
                {
                    timer.waveTimer = timer.intervalTimer;
                    timer.intervalTimer = 0f;
                    timer.duringWave = true;
                }
            }
        }
    }
}

public class ShooterTimer
{
    //弹幕配置信息，保存以备比较，不会修改
    public float duration;
    public float delay;
    public float interval;
    public float waveInterval;
    public int timesThisWave;

    //计时与计数。修改这里
    public float intervalTimer;
    public float waveTimer;
    public int times;   //当前波次已发射几次
    public bool duringWave;

    public BaseShooterParametersSO shooterSO;
}


public class RuntimeInfo
{

}
#endif