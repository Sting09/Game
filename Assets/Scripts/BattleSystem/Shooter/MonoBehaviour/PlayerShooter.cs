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

/*
 *  敌人发射子弹的逻辑
        Shooter持有一个DanmakuSO，Start时每有一个发射器，就添加一个ShooterTimer
        Shooter每帧让每个ShooterTimer检查，本帧要不要执行操作
        
        ShooterTimer内部持有计时器，会更新弹幕样式、发射弹幕
        ShooterTimer构造时根据EmitterConfigSO，构造一个EmitterRuntime
        调用runtime.shoot发射弹幕、runtime.UpdateEventRunners(deltaTime);更新样式

        EmitterRuntime每次发射一个Patter的弹幕
        构造时根据PatternSO构造一个PatternRuntime
        通过pattern.ShootBullet(info, posBuffer[i], dirBuffer[i]);发射
        分别是：子弹信息、发射位置、发射角度
        Emitter每有一个发弹点，就调用pattern.ShootBullet一次

        pattern内部循环，这个样式每有一颗子弹，就执行下面语句一次：
        BulletDOTSManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos + offset, info);
        真正把子弹添加到场景中，并交给Manager管理


    玩家发射子弹的逻辑
        玩家持有WeaponController，职责：
            管理所有Weapon
            读取玩家输入，通知给各个Weapon，检查要不要发射

        WeaponController持有AbstractWeapon，Weapon的职责：
            持有若干子物体，控制各个子物体的位置
            持有计时器，判断要不要发射或操作
            如果需要发射，调用各个子物体的发射方法
            玩家有属性变化，修改各个子物体（如果需要的话）
        每种具体武器继承AbstratWeapon

        每个子物体Shooter持有若干EmitterSO，构造EmitterRuntime
        不再进行内部计时，只对外开放shoot方法，由Weapon判断要不要发射
        每次发射前，需要更新PatterRuntime，修改弹速等属性
 */
