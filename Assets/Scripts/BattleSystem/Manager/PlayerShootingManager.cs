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
            randoms = m_Randoms
        };
        m_JobHandle = eventJob.Schedule(m_ActiveCount, 64);
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

    private void ScheduleCollisionJob()
    {
        // 玩家子弹不与玩家发生碰撞，这里不调度 ObjectCollisionJob
        // 与敌人的碰撞在 HandleCollisions 中在主线程完成
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
        // 1. 清理基类用于玩家圆形判定的队列 (PlayerShootingManager 不需要这个)
        while (m_CollisionQueue.TryDequeue(out _)) { }

        // 2. 获取敌人管理器，如果没有敌人或没有子弹，直接返回
        EnemyDOTSManager enemyMgr = EnemyDOTSManager.Instance;
        if (enemyMgr == null || enemyMgr.ActiveCount == 0 || m_ActiveCount == 0) return;


        enemyMgr.CompleteAllJobs();

        // 3. 准备 Job 数据
        // Allocator.TempJob 表示分配的内存仅在 Job 完成前有效，速度很快
        NativeQueue<int2> hitResults = new NativeQueue<int2>(Allocator.TempJob);

        PlayerBulletEnemyCollisionJob collisionJob = new PlayerBulletEnemyCollisionJob
        {
            // 玩家子弹数据 (从 BaseObjManager 继承的 NativeArray)
            bulletPositions = m_Positions,
            bulletTypes = m_CollisionTypes,
            bulletRadii = m_CircleRadii,
            bulletBoxSizes = m_BoxSizes,
            bulletAngles = m_Angles,
            bulletIsDead = m_IsDead,

            // 敌人数据 (直接读取 EnemyDOTSManager 的 public NativeArray)
            enemyPositions = enemyMgr.m_Positions,
            enemyTypes = enemyMgr.m_CollisionTypes,
            enemyRadii = enemyMgr.m_CircleRadii,
            enemyBoxSizes = enemyMgr.m_BoxSizes,
            enemyAngles = enemyMgr.m_Angles,
            enemyHP = enemyMgr.m_HP,

            // 输出
            hitResults = hitResults.AsParallelWriter()
        };

        // 4. 调度 Job
        // BatchCount 设为 32 或 64，表示每核处理 64 个子弹
        JobHandle handle = collisionJob.Schedule(m_ActiveCount, 64);

        // 5. 立即等待完成 (Complete 会阻塞主线程直到 Job 结束)
        // 注意：如果你希望更高性能，可以将 Complete 延迟到 LateUpdate，
        // 但那样需要将处理逻辑分开。为了逻辑简单，这里直接 Complete。
        handle.Complete();

        // 6. 处理结果
        const float damagePerHit = 5f;

        while (hitResults.TryDequeue(out int2 hit))
        {
            int bulletIdx = hit.x;
            int enemyIdx = hit.y;

            // 双重检查：
            // 1. 子弹可能在这一帧已经判定过（比如之前的 Job 设计防止了一弹多中，但这里再保险一下）
            if (m_IsDead[bulletIdx]) continue;
            // 2. 敌人可能在这一帧已经被前面的子弹打死了
            if (enemyMgr.m_HP[enemyIdx] <= 0f) continue;

            // 敌人扣血
            float newHp = enemyMgr.m_HP[enemyIdx] - damagePerHit;
            enemyMgr.m_HP[enemyIdx] = newHp;

            // 玩家子弹 Cull (移除)
            m_IsDead[bulletIdx] = true;

            // 如果敌人死了，EnemyCullJob 会在 LateUpdate 处理它，这里不需要额外操作
        }

        // 7. 释放 NativeQueue
        hitResults.Dispose();
    }

    protected override void OnDispose()
    {
        return;
    }

    protected override void OnInitialize()
    {
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
            Debug.Log("��BulletManager��δ�����ӵ������б�!");
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
        // ֻ��Ҫ�����������е����齻��
    }

    #endregion

    #region ��ұ����е��߼�
    private void OnPlayerHit()
    {

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