using System.Collections.Generic;
using UnityEngine;

public class BaseShooter : MonoBehaviour
{
    public List<DanmakuSO> danmakuToShoot;     //该敌人要发射的弹幕
    public int currentDanmakuIndex = 0;       //当前正在发射第几个弹幕

    [SerializeField]private float danmakuTimer;                  //当前符卡的计时器
    [SerializeField]private float currentDanmakuDuration;        //当前符卡的持续时间
    private List<ShooterTimer> timers;           //每个Emitter的计时器

    private void Awake()
    {
        timers = new List<ShooterTimer>();
    }


    private void Start()
    {
        currentDanmakuIndex = 0;    //初始化，从第一个弹幕开始发射

        LoadDanmaku();
    }

    void Update()
    {

        danmakuTimer += Time.deltaTime;
        //danmakuDuration为负数时发射永不停止 
        if (currentDanmakuDuration > 0 && danmakuTimer > currentDanmakuDuration)
        {
            //已发射时间比符卡持续时间长，当前符卡停止
            OnDanmakuFinish();
            return;
        }

        //每帧每个计时器自己检查要不要执行发射
        foreach (ShooterTimer timer in timers)
        {
            timer.Tick(Time.deltaTime, danmakuTimer, gameObject.transform);
        }
    }

    /// <summary>
    /// 加载弹幕发射信息。敌人生成和切换符卡时调用。
    /// </summary>
    public void LoadDanmaku()
    {
        //清空上一个弹幕的信息
        danmakuTimer = 0;
        timers.Clear();

        if (currentDanmakuIndex >= danmakuToShoot.Count)
        {
            Debug.Log("符卡索引超出配置数量。请检查敌人弹幕配置");
            return;
        }
        if (danmakuToShoot[currentDanmakuIndex] != null)
        {
            DanmakuSO currentDanmaku = danmakuToShoot[currentDanmakuIndex];
            List<AbstractEmitterConfigSO> currentEmitters = currentDanmaku.emitterList;

            foreach(AbstractEmitterConfigSO emitter in currentEmitters)
            {
                timers.Add(new ShooterTimer(emitter));
            }

            BulletDOTSManager.Instance.PreparePoolsForLevel(currentDanmaku.requiredBulletNames);

            currentDanmakuDuration = currentDanmaku.duration;   //读取符卡持续时间
            danmakuTimer = 0f;      //重置计时器
        }
    }


    /// <summary>
    /// 符卡结束的处理
    /// </summary>
    private void OnDanmakuFinish()
    {
        currentDanmakuIndex++;
        if (currentDanmakuIndex >= danmakuToShoot.Count)    //配置的弹幕都发射完了
        {
            gameObject.SetActive(false);    //敌人失活，或者以后改成敌人死亡动画等
        }
        else
        {
            LoadDanmaku();      //还有符卡要发射，就加载配置
        }
    }
}
