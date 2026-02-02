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

public class PlayerShootingManager : BaseObjManager<PlayerShootingManager>
{
    // --- 所有玩家子弹 ---
    [Header("Bullet Configs (Gameplay)")]
    public List<BulletBasicConfigSO> bulletConfigs;

    // --- 玩家子弹ID查找表 ---
    private Dictionary<string, int> m_VisualNameToID = new Dictionary<string, int>();

    // 缓存的子弹信息
    private struct PendingBullet
    {
        public int visualID;
        public int behaviorID;
        public Vector3 startPos;
        public BulletRuntimeInfo info;
    }
    private List<PendingBullet> m_PendingBullets;


    // --- 新增成员 ---
    // 记得修改：FlushPending()、OnSwapData()、OnDispose()、OnInitialize()
    // 用于跨帧存储碰撞结果，必须用 Persistent
    private NativeQueue<int2> m_HitResults;
    // 专门记录碰撞 Job 的句柄
    private JobHandle m_CollisionJobHandle;



    /// <summary>
    /// 添加子弹
    /// </summary>
    /// <param name="visualID">子弹配置ID</param>
    /// <param name="behaviorID">子弹事件ID</param>
    /// <param name="startPos">发弹位置</param>
    /// <param name="info">子弹信息</param>
    public void AddBullet(int visualID, int behaviorID, Vector3 startPos, BulletRuntimeInfo info)
    {
        if (m_PendingBullets == null) m_PendingBullets = new List<PendingBullet>();

        int pendingCount = m_PendingBullets.Count;
        if (m_ActiveCount + pendingCount >= maxEntityCapacity)
        {
            Debug.LogWarning("Bullet capacity reached; dropping bullet.");
            return;
        }

        m_PendingBullets.Add(new PendingBullet
        {
            visualID = visualID,
            behaviorID = behaviorID,
            startPos = startPos,
            info = info,
        });
    }


    #region Job
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

            // 相对移动属性
            isRelative = m_IsRelative,
            emitterIDs = m_EmitterIDs,
            emitterDeltas = m_EmitterDeltas
        };
        m_JobHandle = moveJob.Schedule(m_Transforms, m_JobHandle);
    }

    // 检查玩家发射的子弹和所有敌人的碰撞
    private void ScheduleCollisionJob()
    {
        // 1. 获取敌人管理器
        EnemyDOTSManager enemyMgr = EnemyDOTSManager.Instance;
        if (enemyMgr == null || enemyMgr.ActiveCount == 0 || m_ActiveCount == 0)
        {
            // 如果没有敌人或子弹，不调度 Job，直接返回
            return;
        }

        // 2. 准备 Job 数据
        PlayerBulletEnemyCollisionJob collisionJob = new PlayerBulletEnemyCollisionJob
        {
            bulletPositions = m_Positions,
            bulletTypes = m_CollisionTypes,
            bulletRadii = m_CircleRadii,
            bulletBoxSizes = m_BoxSizes,
            bulletAngles = m_Angles,
            bulletIsDead = m_IsDead,

            enemyPositions = enemyMgr.m_Positions,
            enemyTypes = enemyMgr.m_CollisionTypes,
            enemyRadii = enemyMgr.m_CircleRadii,
            enemyBoxSizes = enemyMgr.m_BoxSizes,
            enemyAngles = enemyMgr.m_Angles,
            enemyHP = enemyMgr.m_HP,

            hitResults = m_HitResults.AsParallelWriter()
        };

        // 3. 构建依赖：(等待子弹移动) + (等待敌人移动)
        // 获取敌人管理器的Job句柄
        JobHandle enemyMoveHandle = enemyMgr.GetJobHandle();
        //声明：敌人的Job、玩家子弹移动的Job，两个Job都执行完后，再执行下一个Job
        JobHandle combinedDependencies = JobHandle.CombineDependencies(m_JobHandle, enemyMoveHandle);

        // 4. 调度 Job
        //前述两个Job都执行完后，再执行敌人和玩家子弹碰撞检测的Job
        m_CollisionJobHandle = collisionJob.Schedule(m_ActiveCount, 64, combinedDependencies);

        // 5. 更新主依赖链
        // 让后续的 Job (如 CullJob) 等待碰撞完成
        m_JobHandle = m_CollisionJobHandle;

        // 6. 注册外部依赖（让下一帧敌人移动等待此碰撞完成）
        //下一帧开始时，敌人管理器调用Complete前，要等待敌人和玩家子弹碰撞检测的Job完成
        enemyMgr.RegisterExternalDependency(m_CollisionJobHandle);
    }

    private void ScheduleCullJob()
    {
        BulletCullJob cullJob = new BulletCullJob
        {
            positions = m_Positions,
            lifetimes = m_Lifetimes,
            maxLifetimes = m_MaxLifetimes, 
            cullBoundsX = boundsX,
            cullBoundsY = boundsY,
            isDeadResults = m_IsDead
        };
        m_JobHandle = cullJob.Schedule(m_ActiveCount, 64, m_JobHandle);
    }

    #endregion



    #region 实现抽象类
    protected override void FlushPending()
    {
        if (m_PendingBullets == null || m_PendingBullets.Count == 0) return;

        int pendingTotal = m_PendingBullets.Count;
        int available = maxEntityCapacity - m_ActiveCount;
        int toProcess = math.min(pendingTotal, available);

        if (toProcess <= 0)
        {
            m_PendingBullets.Clear();
            return;
        }

        for (int i = 0; i < toProcess; i++)
        {
            var pb = m_PendingBullets[i];
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
            float zPriority = (visualID >= 0 && visualID < bulletConfigs.Count) ? bulletConfigs[visualID].zPriority : 0f;

            m_Positions[index] = new float3(startPos.x, startPos.y, currentZ - zPriority);
            m_Speeds[index] = info.speed;
            m_Angles[index] = info.direction;
            m_Lifetimes[index] = 0f;
            // 如果没有配置子弹最大存活时间，设置为15秒
            m_MaxLifetimes[index] = info.totalLifetime > 0 ? info.totalLifetime : 15f;
            m_LastAngles[index] = info.direction;
            m_IsDead[index] = false;

            m_Accelerations[index] = 0f;
            m_AccelAngles[index] = 0f;
            m_AngularVelocities[index] = 0f;

            m_ActiveVisualIDs[index] = visualID;
            m_EntityBehaviorIDs[index] = behaviorID;

            
            bool isRel = false;
            int eID = 0;

            if (visualID >= 0 && visualID < bulletConfigs.Count)
            {
                BulletBasicConfigSO cfg = bulletConfigs[visualID];
                m_CollisionTypes[index] = (int)cfg.collisionType;
                m_CircleRadii[index] = cfg.circleRadius;
                m_BoxSizes[index] = cfg.boxSize;

                if (pb.info.isRelative && emitter != null)
                {
                    isRel = true;
                    eID = emitter.GetInstanceID();

                    if (!m_ActiveEmitters.ContainsKey(eID))
                    {
                        m_ActiveEmitters.Add(eID, emitter);
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

            m_Transforms.Add(obj.transform);
            m_ActiveGOs.Add(obj);
            m_ActiveCount++;
        }
        m_PendingBullets.Clear();
    }



    protected override void HandleCollisions()
    {
        //可以在这里播放射击命中敌人的音效
        while (m_CollisionQueue.TryDequeue(out _)) { }
    }

    protected override void OnDispose()
    {
        // 取消订阅，防止内存泄漏或空引用
        if (EnemyDOTSManager.Instance != null)
        {
            EnemyDOTSManager.Instance.OnSafeToApplyDamage -= ApplyDamageSafely;
        }

        // 销毁前必须确保 Job 完成
        m_CollisionJobHandle.Complete();
        // 释放队列
        if (m_HitResults.IsCreated) m_HitResults.Dispose();
        return;
    }

    protected override void OnInitialize()
    {
        //初始化查找表
        m_VisualNameToID.Clear();

        if (bulletConfigs != null)
        {
            m_VisualPools = new Queue<GameObject>[bulletConfigs.Count];
            m_VisualRoots = new Transform[bulletConfigs.Count];

            for (int i = 0; i < bulletConfigs.Count; i++)
            {
                if (bulletConfigs[i] != null)
                {
                    if (!m_VisualNameToID.ContainsKey(bulletConfigs[i].bulletName))
                    {
                        m_VisualNameToID.Add(bulletConfigs[i].bulletName, i);
                    }
                }
            }
        }
        else
        {
            Debug.Log("PlayerShootingManager中，未配置发射的子弹的数据!");
        }



        // 初始化持久化队列
        m_HitResults = new NativeQueue<int2>(Allocator.Persistent);

        // 订阅敌人的安全事件
        if (EnemyDOTSManager.Instance != null)
        {
            EnemyDOTSManager.Instance.OnSafeToApplyDamage += ApplyDamageSafely;
        }
    }

    protected override void ScheduleSpecificJobs()
    {
        ScheduleEventJob();
        ScheduleMoveJob();
        ScheduleCollisionJob();
        ScheduleCullJob();
    }

    protected override void OnSwapData(int index, int lastIndex)
    {
        
    }

    // 这个方法会在 EnemyDOTSManager 的“安全窗口期”被调用
    private void ApplyDamageSafely()
    {
        EnemyDOTSManager enemyMgr = EnemyDOTSManager.Instance;
        if (enemyMgr == null) return;

        // 1. 确保上一帧的碰撞计算已完成
        // (由于 EnemyDOTSManager 已经在 Update 开头 Complete 了依赖链，这里其实已经完成了，但为了保险再调一次)
        m_CollisionJobHandle.Complete();

        // 2. 处理命中队列（结算伤害）
        const float damagePerHit = 5f;
        while (m_HitResults.TryDequeue(out int2 hit))
        {
            int bulletIdx = hit.x;
            int enemyIdx = hit.y;

            // 安全检查
            if (bulletIdx < m_IsDead.Length && !m_IsDead[bulletIdx] &&
                enemyIdx < enemyMgr.m_HP.Length && enemyMgr.m_HP[enemyIdx] > 0f)
            {
                enemyMgr.m_HP[enemyIdx] -= damagePerHit; // 现在这里是安全的！
                m_IsDead[bulletIdx] = true;
            }
        }
    }

    #endregion

    #region Pool & Helper Methods
    private GameObject GetBulletFromPool(int visualID)
    {
        if (visualID < 0 || visualID >= m_VisualPools.Length) return null;
        if (m_VisualPools[visualID] == null) m_VisualPools[visualID] = new Queue<GameObject>();

        Queue<GameObject> pool = m_VisualPools[visualID];
        if (pool.Count > 0) return pool.Dequeue();
        else
        {
            BulletBasicConfigSO cfg = bulletConfigs[visualID];
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
        else
        {
            Debug.Log($"PlayerShootingManagerԤ���{name}ʧ�ܣ����������Ƿ���ȷ");
        }
    }

    private Transform GetOrCreateVisualRoot(int visualID)
    {
        if (m_VisualRoots[visualID] == null)
        {
            string bulletTypeName = bulletConfigs[visualID].bulletName;
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
    #endregion

    #region Debug Gizmos
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