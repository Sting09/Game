using System.Collections.Generic;
using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    //复用BaseShooter代码，区别是，玩家的射击要遍历所有Danmaku

    public List<DanmakuSO> shooters;            //所有武器的发射器
    public List<PlayerShootType> shooterTypes;  //所有武器的发射方式（高速/低速/技能等）
    private List<PlayerShooterTimer> timers;           //每个Emitter的计时器

    public bool needPrewarm = false;        //是否需要根据弹幕列表，预热对象池

    public float battleTimer;             //战斗已经开始了多久

    private void Awake()
    {
        timers = new List<PlayerShooterTimer>();
    }

    private void Start()
    {
        if (needPrewarm)
        {
            PrewarmEntityPool();
        }
    }

    public void PrewarmEntityPool()
    {
        for (int i = 0; i < shooters.Count; i++)
        {
            DanmakuSO currentDanmaku = shooters[i];
            foreach (var info in currentDanmaku.requiredEntities)
            {
                switch (info.type)
                {
                    case ShootObjType.Bullet:
                        BulletDOTSManager.Instance.PreparePoolsForLevel(info.entityName, info.num);
                        break;
                    case ShootObjType.BulletGroup:
                        break;
                    case ShootObjType.Enemy:
                        EnemyDOTSManager.Instance.PreparePoolsForLevel(info.entityName, info.num);
                        break;
                    case ShootObjType.PlayerBullet:
                        PlayerShootingManager.Instance.PreparePoolsForLevel(info.entityName, info.num);
                        break;
                    default:
                        break;
                }
            }
        }
    }


    private void OnEnable()
    {
        LoadShooters();
    }

    /// <summary>
    /// 加载弹幕发射信息。OnEnable时调用
    /// </summary>
    public void LoadShooters()
    {
        timers.Clear();

        for(int i = 0; i < shooters.Count; i++)
        {
            if (shooters[i] != null)
            {
                DanmakuSO currentDanmaku = shooters[i];
                List<AbstractEmitterConfigSO> currentEmitters = currentDanmaku.emitterList;

                foreach (AbstractEmitterConfigSO emitter in currentEmitters)
                {
                    timers.Add(new PlayerShooterTimer(emitter));
                }
            }
        }
    }


    void Update()
    {
        //==================================================
        //应该是有些武器高速发射、有些武器低速发射
        //当前没考虑高速低速等因素，直接所有武器一起发射
        //==================================================

        //更新运行时间
        battleTimer += Time.deltaTime;

        //每帧每个计时器自己检查要不要执行发射
        foreach (PlayerShooterTimer timer in timers)
        {
            timer.Tick(Time.deltaTime, battleTimer, gameObject.transform);
        }
    }

}
