using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;



#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemyDOTSManager : BaseObjManager<EnemyDOTSManager>
{
    // --- 所有敌人的配置列表 ---
    [Header("Enemy Configs (Gameplay)")]
    public List<EnemyBasicConfigSO> enemyConfigs;

    // --- 敌人名称 到 ID 查找表 ---
    private Dictionary<string, int> m_VisualNameToID = new Dictionary<string, int>();

    public BossController bossController;
    public int bossEnemyID;
    public int currentBossPhase = 0;
    public bool newBoss = true;

    // 当前帧要加入池子的敌人列表
    private struct PendingEnemy
    {
        public int visualID;
        public int behaviorID;
        public Vector3 startPos;
        public BulletRuntimeInfo info;
    }
    private List<PendingEnemy> m_PendingEnemy;

    //通知其他Manager，可以计算玩家子弹和敌人碰撞的时间点
    public event System.Action OnSafeToApplyDamage;


    // 敌人独有的属性
    // 记得修改：FlushPending()、OnSwapData()、OnDispose()、OnInitialize()
    public NativeArray<float> m_HP;

    // --- 全局减伤配置缓存（所有敌人共享） ---
    protected NativeArray<int2> m_DRRanges; // x = startIndex, y = count
    protected NativeArray<DamageReductionStage> m_GlobalDRStages;
    // --- 全局 Boss 减伤配置缓存 ---
    // x = 在 m_GlobalBossDRRanges 的起始索引, y = 该Boss拥有的总阶段数
    protected NativeArray<int2> m_BossPhaseInfo;
    // x = 在 m_GlobalDRStages 的起始索引, y = 该阶段的时间轴节点总数
    protected NativeArray<int2> m_GlobalBossDRRanges;

    // --- 实体独有属性 ---
    public NativeArray<int> m_BossCurrentPhase; // 当前处于第几阶段攻击

    // --- 敌人个体减伤内存 (记得在 Initialize, Dispose, SwapData 中处理) ---
    public NativeArray<int> m_DRCurrentStageIndex; // 当前处于时间轴的第几个阶段
    public NativeArray<float> m_DRTimer;           // 当前阶段的计时器
    public NativeArray<float> m_BaseDR;            // 仅由时间轴计算出的基础减伤率

    // --- 状态覆盖内存 ---
    public NativeArray<bool> m_HasLocalDROverride; // 是否有单体减伤覆盖
    public NativeArray<float> m_LocalDROverride;   // 单体减伤覆盖的数值

    // --- 全局状态覆盖（用于玩家全屏炸弹等场景，极致性能） ---
    public bool hasGlobalDROverride = false;
    public float globalDROverrideValue = 1.0f;     // 比如放炸弹时设为 1.0f (100%)

    // --- Boss特有参数---
    public NativeArray<bool> m_IsBoss;                      //是否是Boss
    public NativeArray<bool> m_IsInvulnerable;              //是否处于无敌状态
    public NativeArray<bool> m_TriggerPhaseTransition;      //是否需要触发转场事件.

    protected override void LateUpdate()
    {
        base.LateUpdate();

        OnJobCompleted();
    }


    /// <summary>
    /// 添加敌人。
    /// </summary>
    /// <param name="visualID">外观ID</param>
    /// <param name="behaviorID">行为ID</param>
    /// <param name="startPos">初始位置</param>
    /// <param name="info">运行信息</param>
    /// <param name="emitter">发射者Transform（如果是相对移动子弹，此参数必须不为空）</param>
    public void AddEnemy(int visualID, int behaviorID, Vector3 startPos, BulletRuntimeInfo info)
    {
        if (isPaused) { return; }
        if (m_PendingEnemy == null) { m_PendingEnemy = new List<PendingEnemy>(); }

        int pendingCount = m_PendingEnemy.Count;
        if (m_ActiveCount + pendingCount >= maxEntityCapacity)
        {
            Debug.LogWarning("Bullet capacity reached; dropping bullet.");
            return;
        }

        m_PendingEnemy.Add(new PendingEnemy
        {
            visualID = visualID,
            behaviorID = behaviorID,
            startPos = startPos,
            info = info,
        });
    }


    public void ResetBossHPAndInvulnerability(float maxHP, bool advancePhase = true)
    {
        m_JobHandle.Complete();
        m_IsInvulnerable[bossEnemyID] = false;
        m_HP[bossEnemyID] = maxHP;
        m_BossCurrentPhase[bossEnemyID] = currentBossPhase;
        m_DRCurrentStageIndex[bossEnemyID] = 0; // 重置减伤阶段的时间轴
        m_DRTimer[bossEnemyID] = 0f;

        if (advancePhase)
        {
            // 立即取新阶段第一秒的减伤率，防止漏算 1 帧
            int vID = m_ActiveVisualIDs[bossEnemyID];
            int phase = m_BossCurrentPhase[bossEnemyID];
            int2 phaseInfo = m_BossPhaseInfo[vID];

            if (phase < phaseInfo.y)
            {
                int2 range = m_GlobalBossDRRanges[phaseInfo.x + phase];
                if (range.y > 0) m_BaseDR[bossEnemyID] = m_GlobalDRStages[range.x].reductionRate;
            }
        }
    }

    public void RemoveBoss()
    {
        m_IsDead[bossEnemyID] = true;
    }


    #region 玩家被命中的逻辑
    /// <summary>
    /// 触发玩家与敌人碰撞的逻辑
    /// </summary>
    private void OnPlayerHit()
    {
        //Debug.Log("<color=red>玩家中弹！</color>");
        if (playerSpriteRenderer == null && BattleManager.Instance != null && BattleManager.Instance.player != null)
        {
            playerSpriteRenderer = BattleManager.Instance.player.GetComponent<SpriteRenderer>();
        }
        if (hitPlayerCoroutine != null) StopCoroutine(hitPlayerCoroutine);
        hitPlayerCoroutine = StartCoroutine(HitFlashRoutine());
    }

    /// <summary>
    /// 玩家贴图闪烁红光
    /// </summary>
    /// <returns></returns>
    private IEnumerator HitFlashRoutine()
    {
        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            playerSpriteRenderer.color = Color.white;
        }
    }
    #endregion



    #region 要派发的Job（事件、移动、碰撞、移除）
    private void ScheduleEventJob()
    {
        ObjectEventJob eventJob = new ObjectEventJob
        {
            lifetimes = m_Lifetimes,
            bulletBehaviorIDs = m_EntityBehaviorIDs,
            behaviorRanges = m_BehaviorRanges,
            globalEvents = m_GlobalEventArray,
            speeds = m_Speeds,
            angles = m_Angles,
            accelerations = m_Accelerations,
            accelAngles = m_AccelAngles,
            angularVelocities = m_AngularVelocities,
            isDead = m_IsDead,
            nextEventIndex = m_NextEventIndex,
            randoms = m_Randoms,

            // 发射信息参数
            shootPointIndices = m_ShootPointIndices,
        };
        m_JobHandle = eventJob.Schedule(m_ActiveCount, 64, m_JobHandle);
    }

    private void ScheduleMoveJob()
    {
        ObjectMoveJob moveJob = new ObjectMoveJob
        {
            dt = dt,
            positions = m_Positions,
            speeds = m_Speeds,
            angles = m_Angles,
            lifetimes = m_Lifetimes,
            lastAngles = m_LastAngles,
            accelerations = m_Accelerations,
            accelAngles = m_AccelAngles,
            angularVelocities = m_AngularVelocities,

            // 相对移动参数
            isRelative = m_IsRelative,
            emitterIDs = m_EmitterIDs,
            emitterDeltas = m_EmitterDeltas
        };
        // 这里的依赖关系：Job 依赖于 m_JobHandle (EventJob)，并且会读取 m_EmitterDeltas
        m_JobHandle = moveJob.Schedule(m_Transforms, m_JobHandle);
    }

    private void ScheduleDamageReductionJob()
    {
        EnemyDamageReductionJob drJob = new EnemyDamageReductionJob
        {
            dt = dt,
            isDead = m_IsDead,
            visualIDs = m_ActiveVisualIDs,
            drRanges = m_DRRanges,
            globalDRStages = m_GlobalDRStages,

            // --- 传入新增的 Boss 数组 ---
            isBoss = m_IsBoss,
            bossCurrentPhase = m_BossCurrentPhase,
            bossPhaseInfo = m_BossPhaseInfo,
            globalBossDRRanges = m_GlobalBossDRRanges,
            // -----------------------------

            drCurrentStageIndex = m_DRCurrentStageIndex,
            drTimer = m_DRTimer,
            baseDR = m_BaseDR
        };

        m_JobHandle = drJob.Schedule(m_ActiveCount, 64, m_JobHandle);
    }

    private void ScheduleCollisionJob()
    {
        var policy = new EnemyCollisionPolicy
        {
            collisionEvents = m_CollisionQueue.AsParallelWriter(),
        };
        var collisionJob = new ObjectCollisionJob<EnemyCollisionPolicy>
        {
            playerPos = playerPos,
            playerRadius = playerHitboxRadius * playerHitboxRate,
            positions = m_Positions,
            angles = m_Angles,
            collisionTypes = m_CollisionTypes,
            bulletRadii = m_CircleRadii,
            boxSizes = m_BoxSizes,
            isDead = m_IsDead,
            policyData = policy
        };
        m_JobHandle = collisionJob.Schedule(m_ActiveCount, 64, m_JobHandle);
    }

    private void ScheduleCullJob()
    {
        EnemyCullJob cullJob = new EnemyCullJob
        {
            lifetimes = m_Lifetimes,
            maxLifetimes = m_MaxLifetimes,
            isDeadResults = m_IsDead,
            hp = m_HP,
            isInvulnerable = m_IsInvulnerable,
            isBoss = m_IsBoss,
            triggerPhaseTransition = m_TriggerPhaseTransition
        };
        m_JobHandle = cullJob.Schedule(m_ActiveCount, 64, m_JobHandle);
    }


    public void OnJobCompleted()
    {
        // 检查是否有Boss触发了转场
        for (int i = 0; i < m_ActiveCount; i++)
        {
            if (m_TriggerPhaseTransition[i])   //如果存在敌人需要转场
            {
                m_TriggerPhaseTransition[i] = false; // 清除标记

                // 通知对应的实体对象
                if(bossController == null) { Debug.Log("bossController is needed!"); return; }
                bossController.OnDanmakuEnd();

                break;      //场上只能有一个Boss
            }
        }
    }

    #endregion



    #region 实现抽象类
    /// <summary>
    /// 添加本帧的obj
    /// </summary>
    protected override void FlushPending()
    {
        if (m_PendingEnemy == null || m_PendingEnemy.Count == 0) return;

        int pendingTotal = m_PendingEnemy.Count;
        int available = maxEntityCapacity - m_ActiveCount;
        int toProcess = math.min(pendingTotal, available);

        if (toProcess <= 0)
        {
            m_PendingEnemy.Clear();
            return;
        }

        for (int i = 0; i < toProcess; i++)
        {
            var pb = m_PendingEnemy[i];
            int visualID = pb.visualID;
            int behaviorID = pb.behaviorID;
            Vector3 startPos = pb.startPos;
            BulletRuntimeInfo info = pb.info;
            Transform emitter = pb.info.parentTransform;

            GameObject obj = GetBulletFromPool(visualID);
            if (obj == null) continue;

            obj.SetActive(true);
            obj.transform.SetPositionAndRotation(startPos, Quaternion.Euler(0, 0, -info.direction));

            int index = m_ActiveCount;
            currentZ += deltaZ;
            float zPriority = (visualID >= 0 && visualID < enemyConfigs.Count) ? enemyConfigs[visualID].zPriority : 0f;

            m_Positions[index] = new float3(startPos.x, startPos.y, currentZ - zPriority);
            m_Speeds[index] = info.speed;
            m_Angles[index] = info.direction;
            m_Lifetimes[index] = 0f;
            m_MaxLifetimes[index] = info.totalLifetime;     //负数表示永不过期，只能通过生命值归零消失
            m_LastAngles[index] = info.direction;
            m_IsDead[index] = false;

            m_Accelerations[index] = 0f;
            m_AccelAngles[index] = 0f;
            m_AngularVelocities[index] = 0f;

            m_ActiveVisualIDs[index] = visualID;
            m_EntityBehaviorIDs[index] = behaviorID;

            m_HP[index] = (visualID >= 0 && visualID < enemyConfigs.Count) ? math.max(enemyConfigs[visualID].maxHP, 1f) : 1f;

            m_TriggerPhaseTransition[index] = false;

            m_IsBoss[index] = enemyConfigs[visualID].isBoss;
            m_IsKeyComponent[index] = m_IsBoss[index];
            m_IsInvulnerable[index] = m_IsBoss[index];

            if (enemyConfigs[visualID].isBoss)
            {
                bossEnemyID = index;
            }

            // --- 相对移动逻辑 ---
            bool isRel = false;
            int eID = 0;

            if (visualID >= 0 && visualID < enemyConfigs.Count)
            {
                EnemyBasicConfigSO cfg = enemyConfigs[visualID];
                m_CollisionTypes[index] = (int)cfg.collisionType;
                m_CircleRadii[index] = cfg.circleRadius;
                m_BoxSizes[index] = cfg.boxSize;

                // 检查配置是否开启相对移动，且发射者是否存在
                if (pb.info.isRelative && emitter != null)
                {
                    isRel = true;
                    eID = emitter.GetInstanceID();

                    // 注册到活跃发射者列表，以便 Update 计算位移
                    if (!m_ActiveEmitters.ContainsKey(eID))
                    {
                        m_ActiveEmitters.Add(eID, emitter);
                        // 初始化上一帧位置为当前位置（防止第一帧跳变）
                        if (!m_LastEmitterPos.ContainsKey(eID))
                        {
                            m_LastEmitterPos.Add(eID, emitter.position);
                        }
                    }
                }
            }
            else
            {
                m_CollisionTypes[index] = 0;
                m_CircleRadii[index] = 0.1f;
                m_BoxSizes[index] = new float2(0.2f, 0.2f);
            }

            m_IsRelative[index] = isRel;
            m_EmitterIDs[index] = eID;

            if (behaviorID >= 0 && behaviorID < m_BehaviorRanges.Length)
            {
                int2 range = m_BehaviorRanges[behaviorID];
                m_NextEventIndex[index] = (range.y > 0) ? range.x : -1;
            }
            else
            {
                m_NextEventIndex[index] = -1;
            }

            // 减伤逻辑
            m_DRCurrentStageIndex[index] = 0;
            m_DRTimer[index] = 0f;
            m_HasLocalDROverride[index] = false;
            m_LocalDROverride[index] = 0f;
            int safePhase = math.min(currentBossPhase, math.max(0, m_BossPhaseInfo[visualID].y - 1));
            m_BossCurrentPhase[index] = safePhase;

            // 初始化第一阶段的减伤率
            if (m_IsBoss[index])
            {
                int2 phaseInfo = m_BossPhaseInfo[visualID];
                if (phaseInfo.y > 0)
                {
                    int2 range = m_GlobalBossDRRanges[phaseInfo.x + safePhase]; // 取出第 i 阶段的时间轴范围
                    m_BaseDR[index] = (range.y > 0) ? m_GlobalDRStages[range.x].reductionRate : 0f;
                }
                else m_BaseDR[index] = 0f;
            }
            else
            {
                if (visualID >= 0 && visualID < m_DRRanges.Length && m_DRRanges[visualID].y > 0)
                {
                    m_BaseDR[index] = m_GlobalDRStages[m_DRRanges[visualID].x].reductionRate;
                }
                else m_BaseDR[index] = 0f;
            }

            m_Transforms.Add(obj.transform);
            m_ActiveGOs.Add(obj);
            m_ActiveCount++;
        }
        m_PendingEnemy.Clear();
    }

    protected override void HandleCollisions()
    {


        // 通知其他类，敌人和玩家的碰撞检测已完毕，可以检测玩家子弹和敌人的碰撞了
        OnSafeToApplyDamage?.Invoke();


        //如果本帧有子弹命中玩家，则触发OnPlayerHit
        bool hasHit = false;
        while (m_CollisionQueue.TryDequeue(out int bulletIndex))
        {
            hasHit = true;
        }

        if (hasHit)
        {
            //Debug.Log("<color=red>玩家被敌人体术！</color>");
            OnPlayerHit();
        }
    }

    protected override void OnDispose()
    {
        if (m_HP.IsCreated) m_HP.Dispose();

        if (m_DRCurrentStageIndex.IsCreated) m_DRCurrentStageIndex.Dispose();
        if (m_DRTimer.IsCreated) m_DRTimer.Dispose();
        if (m_BaseDR.IsCreated) m_BaseDR.Dispose();
        if (m_HasLocalDROverride.IsCreated) m_HasLocalDROverride.Dispose();
        if (m_LocalDROverride.IsCreated) m_LocalDROverride.Dispose();

        if (m_DRRanges.IsCreated) m_DRRanges.Dispose();
        if (m_GlobalDRStages.IsCreated) m_GlobalDRStages.Dispose();

        if(m_IsBoss.IsCreated) m_IsBoss.Dispose();
        if(m_IsInvulnerable.IsCreated) m_IsInvulnerable.Dispose();
        if(m_TriggerPhaseTransition.IsCreated) m_TriggerPhaseTransition.Dispose();
        if (m_BossPhaseInfo.IsCreated) m_BossPhaseInfo.Dispose();
        if (m_GlobalBossDRRanges.IsCreated) m_GlobalBossDRRanges.Dispose();
        if (m_BossCurrentPhase.IsCreated) m_BossCurrentPhase.Dispose();
    }

    protected override void OnInitialize()
    {
        //初始化查找表
        m_VisualNameToID.Clear();

        if (enemyConfigs != null)
        {
            m_VisualPools = new Queue<GameObject>[enemyConfigs.Count];
            m_VisualRoots = new Transform[enemyConfigs.Count];

            for (int i = 0; i < enemyConfigs.Count; i++)
            {
                if (enemyConfigs[i] != null)
                {
                    if (!m_VisualNameToID.ContainsKey(enemyConfigs[i].enemyName))
                    {
                        m_VisualNameToID.Add(enemyConfigs[i].enemyName, i);
                    }
                }
            }
        }
        else
        {
            Debug.Log("在EnemyManager中未配置子弹类型列表!");
        }


        //初始化新属性
        m_HP = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);

        // 1. 初始化个体减伤内存
        m_DRCurrentStageIndex = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent);
        m_DRTimer = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_BaseDR = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_HasLocalDROverride = new NativeArray<bool>(maxEntityCapacity, Allocator.Persistent);
        m_LocalDROverride = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);

        m_IsBoss = new NativeArray<bool>(maxEntityCapacity, Allocator.Persistent);
        m_IsInvulnerable = new NativeArray<bool>(maxEntityCapacity, Allocator.Persistent);
        m_TriggerPhaseTransition = new NativeArray<bool>(maxEntityCapacity, Allocator.Persistent);
        m_BossCurrentPhase = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent); // 新增

        // 2. 展平策划的减伤时间轴配置
        if (enemyConfigs != null)
        {
            List<DamageReductionStage> tempAllStages = new List<DamageReductionStage>();
            List<int2> tempBossDRRanges = new List<int2>();

            m_DRRanges = new NativeArray<int2>(enemyConfigs.Count, Allocator.Persistent);
            m_BossPhaseInfo = new NativeArray<int2>(enemyConfigs.Count, Allocator.Persistent);

            for (int i = 0; i < enemyConfigs.Count; i++)
            {
                var config = enemyConfigs[i];
                if (config == null) continue;

                // --- 处理普通敌人时间轴 ---
                int normalStartIndex = tempAllStages.Count;
                int normalCount = 0;
                if (config.drTimeline != null && config.drTimeline.Count > 0)
                {
                    normalCount = config.drTimeline.Count;
                    tempAllStages.AddRange(config.drTimeline);
                }
                m_DRRanges[i] = new int2(normalStartIndex, normalCount);

                // --- 处理 Boss 多阶段时间轴 ---
                int bossPhaseStartIndex = tempBossDRRanges.Count;
                int bossPhaseCount = 0;
                if (config.isBoss && config.bossDRTimeline != null)
                {
                    bossPhaseCount = config.bossDRTimeline.Count;
                    foreach (var phase in config.bossDRTimeline)
                    {
                        int phaseStartIndex = tempAllStages.Count;
                        int phaseStageCount = 0;
                        if (phase.columns != null && phase.columns.Count > 0)
                        {
                            phaseStageCount = phase.columns.Count;
                            tempAllStages.AddRange(phase.columns);
                        }
                        tempBossDRRanges.Add(new int2(phaseStartIndex, phaseStageCount));
                    }
                }
                m_BossPhaseInfo[i] = new int2(bossPhaseStartIndex, bossPhaseCount);
            }

            m_GlobalDRStages = new NativeArray<DamageReductionStage>(tempAllStages.ToArray(), Allocator.Persistent);
            m_GlobalBossDRRanges = new NativeArray<int2>(tempBossDRRanges.ToArray(), Allocator.Persistent);
        }
    }

    protected override void ScheduleSpecificJobs()
    {
        ScheduleEventJob();
        ScheduleMoveJob();
        ScheduleDamageReductionJob();
        ScheduleCollisionJob();
        ScheduleCullJob();
    }

    protected override void OnSwapData(int index, int lastIndex)
    {
        // 只需要处理子类特有的数组交换
        m_HP[index] = m_HP[lastIndex];

        m_DRCurrentStageIndex[index] = m_DRCurrentStageIndex[lastIndex];
        m_DRTimer[index] = m_DRTimer[lastIndex];
        m_BaseDR[index] = m_BaseDR[lastIndex];
        m_HasLocalDROverride[index] = m_HasLocalDROverride[lastIndex];
        m_LocalDROverride[index] = m_LocalDROverride[lastIndex];
        m_IsBoss[index] = m_IsBoss[lastIndex];
        m_IsInvulnerable[index] = m_IsInvulnerable[lastIndex];
        m_TriggerPhaseTransition[index] = m_TriggerPhaseTransition[lastIndex];
        m_BossCurrentPhase[index] = m_BossCurrentPhase[lastIndex];

        // 【新增】：如果被移动的最后一个元素刚好是Boss，必须更新它的缓存ID！
        if (bossEnemyID == lastIndex)
        {
            bossEnemyID = index;
        }
    }

    #endregion

    

    #region 对象池管理方法  和  辅助方法
    private GameObject GetBulletFromPool(int visualID)
    {
        if (visualID < 0 || visualID >= m_VisualPools.Length) return null;
        if (m_VisualPools[visualID] == null) m_VisualPools[visualID] = new Queue<GameObject>();

        Queue<GameObject> pool = m_VisualPools[visualID];
        if (pool.Count > 0) return pool.Dequeue();
        else
        {
            EnemyBasicConfigSO cfg = enemyConfigs[visualID];
            Transform parent = GetOrCreateVisualRoot(visualID);
            return Instantiate(cfg.prefab, parent);
        }
    }

    public void PreparePoolsForLevel(string name, int countPerType = 50)
    {
        int id = GetVisualID(name);
        if (id != -1)
        {
            List<GameObject> temp = new List<GameObject>();
            for (int i = 0; i < countPerType; i++)
            {
                GameObject obj = GetBulletFromPool(id);
                if (obj != null) temp.Add(obj);
            }
            foreach (var obj in temp)
            {
                m_VisualPools[id].Enqueue(obj);
                obj.SetActive(false);
            }
        }
    }

    private Transform GetOrCreateVisualRoot(int visualID)
    {
        if (m_VisualRoots[visualID] == null)
        {
            string bulletTypeName = enemyConfigs[visualID].enemyName;
            GameObject subRootObj = new GameObject($"Pool_{bulletTypeName}");
            subRootObj.transform.SetParent(poolRoot);
            subRootObj.transform.localPosition = Vector3.zero;
            m_VisualRoots[visualID] = subRootObj.transform;
        }
        return m_VisualRoots[visualID];
    }

    public int GetVisualID(string name)
    {
        if (m_VisualNameToID.TryGetValue(name, out int id)) return id;
        return -1;
    }


    public override void OnClearAllObjects(bool destroy) 
    {
        if (m_PendingEnemy != null)
        {
            m_PendingEnemy.Clear();
        }
    }
    #endregion

    #region 调试方法：画敌人判定
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !m_IsInitialized) return;
        if (!m_Positions.IsCreated || !m_CircleRadii.IsCreated) return;
        if (!m_JobHandle.IsCompleted) return;

#if UNITY_EDITOR
        UnityEditor.Handles.color = debugGizmoColor;

        for (int i = 0; i < m_ActiveCount; i++)
        {
            Vector3 pos = m_Positions[i];
            pos.z = 0;

            int type = m_CollisionTypes[i];

            if (type == 0)
            {
                float radius = m_CircleRadii[i];
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, radius);
            }
            else if (type == 1)
            {
                float2 size = m_BoxSizes[i];
                float angleDeg = m_Angles[i];
                Quaternion rot = Quaternion.Euler(0, 0, -angleDeg);

                Matrix4x4 matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
                using (new UnityEditor.Handles.DrawingScope(matrix))
                {
                    UnityEditor.Handles.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0));
                }
            }
        }
#endif
    }

    #endregion
}