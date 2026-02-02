using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BulletDOTSManager : BaseObjManager<BulletDOTSManager>
{
    // --- 配置 ---
    [Header("Bullet Configs (Gameplay)")]
    public List<BulletBasicConfigSO> bulletConfigs;

    // --- 子弹样式 ID 查找表 ---
    private Dictionary<string, int> m_VisualNameToID = new Dictionary<string, int>();

    // 当前帧要加入池子的子弹列表
    private struct PendingBullet
    {
        public int visualID;
        public int behaviorID;
        public Vector3 startPos;
        public BulletRuntimeInfo info;
    }
    private List<PendingBullet> m_PendingBullets;



    /// <summary>
    /// 添加子弹。
    /// </summary>
    /// <param name="visualID">外观ID</param>
    /// <param name="behaviorID">行为ID</param>
    /// <param name="startPos">初始位置</param>
    /// <param name="info">运行参数</param>
    /// <param name="emitter">发射者Transform（如果是相对移动子弹，此参数必须不为空）</param>
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

    #region 玩家被命中的逻辑
    private void OnPlayerHit()
    {
        Debug.Log("<color=red>玩家中弹！</color>");
        if (playerSpriteRenderer == null && BattleManager.Instance != null && BattleManager.Instance.player != null)
        {
            playerSpriteRenderer = BattleManager.Instance.player.GetComponent<SpriteRenderer>();
        }
        if (hitPlayerCoroutine != null) StopCoroutine(hitPlayerCoroutine);
        hitPlayerCoroutine = StartCoroutine(HitFlashRoutine());
    }

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


    #region 要派发的Job
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
        m_JobHandle = moveJob.Schedule(m_Transforms, m_JobHandle);
    }

    private void ScheduleCollisionJob()
    {
        var policy = new BulletCollisionPolicy
        {
            collisionEvents = m_CollisionQueue.AsParallelWriter(),
        };
        var collisionJob = new ObjectCollisionJob<BulletCollisionPolicy>
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
            // 设置最大存活时间，如果未设置(<=0)则默认为15秒
            m_MaxLifetimes[index] = info.totalLifetime > 0 ? info.totalLifetime : 15f;
            m_LastAngles[index] = info.direction;
            m_IsDead[index] = false;

            m_ShootPointIndices[index] = info.shootPointIndex;

            m_Accelerations[index] = 0f;
            m_AccelAngles[index] = 0f;
            m_AngularVelocities[index] = 0f;

            m_ActiveVisualIDs[index] = visualID;
            m_EntityBehaviorIDs[index] = behaviorID;

            // --- 相对移动逻辑 ---
            bool isRel = false;
            int eID = 0;

            if (visualID >= 0 && visualID < bulletConfigs.Count)
            {
                BulletBasicConfigSO cfg = bulletConfigs[visualID];
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

            m_Transforms.Add(obj.transform);
            m_ActiveGOs.Add(obj);
            m_ActiveCount++;
        }
        m_PendingBullets.Clear();
    }

    protected override void HandleCollisions()
    {
        //如果本帧有子弹命中玩家，则触发OnPlayerHit
        bool hasHit = false;
        while (m_CollisionQueue.TryDequeue(out int bulletIndex))
        {
            hasHit = true;
        }

        if (hasHit)
        {
            OnPlayerHit();
        }
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
            Debug.Log("在BulletManager中未配置子弹类型列表!");
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
        // 只需要处理子类特有的数组交换
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

        if (BattleManager.Instance != null)
        {
            Vector3 pPos = BattleManager.Instance.GetPlayerPos();
            pPos.z = 0;
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(pPos, Vector3.forward, playerHitboxRadius * playerHitboxRate);
        }
#endif
    }

    #endregion
}