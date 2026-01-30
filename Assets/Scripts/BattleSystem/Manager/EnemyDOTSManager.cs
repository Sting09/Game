using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;



#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemyDOTSManager : BaseObjManager<EnemyDOTSManager>
{
    // --- 配置 ---
    [Header("Enemy Configs (Gameplay)")]
    public List<EnemyBasicConfigSO> enemyConfigs;

    // --- 敌人名称 到 ID 查找表 ---
    private Dictionary<string, int> m_VisualNameToID = new Dictionary<string, int>();

    // 当前帧要加入池子的子弹列表
    private struct PendingEnemy
    {
        public int visualID;
        public int behaviorID;
        public Vector3 startPos;
        public BulletRuntimeInfo info;
    }
    private List<PendingEnemy> m_PendingEnemy;


    // 敌人独有的属性
    // 记得修改：FlushPending()、OnSwapData()、OnDispose()、OnInitialize()
    public NativeArray<float> m_HP;


    /// <summary>
    /// 添加子弹。
    /// </summary>
    /// <param name="visualID">外观ID</param>
    /// <param name="behaviorID">行为ID</param>
    /// <param name="startPos">初始位置</param>
    /// <param name="info">运行参数</param>
    /// <param name="emitter">发射者Transform（如果是相对移动子弹，此参数必须不为空）</param>
    public void AddEnemy(int visualID, int behaviorID, Vector3 startPos, BulletRuntimeInfo info)
    {
        if (m_PendingEnemy == null) m_PendingEnemy = new List<PendingEnemy>();

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

            // 相对移动参数
            isRelative = m_IsRelative,
            emitterIDs = m_EmitterIDs,
            emitterDeltas = m_EmitterDeltas
        };
        // 这里的依赖关系：Job 依赖于 m_JobHandle (EventJob)，并且会读取 m_EmitterDeltas
        m_JobHandle = moveJob.Schedule(m_Transforms, m_JobHandle);
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
            maxLifetimes = m_MaxLifetimes, // 修改：传入每颗子弹的最大寿命数组
            isDeadResults = m_IsDead,
            hp = m_HP
        };
        m_JobHandle = cullJob.Schedule(m_ActiveCount, 64, m_JobHandle);
    }


    public void CompleteAllJobs()
    {
        // m_JobHandle 是你在 BaseObjManager 或本类中定义的控制所有 Job 的句柄
        // 如果你的变量名是 m_MoveJobHandle 或其他名字，请替换成对应的变量名
        m_JobHandle.Complete();
    }

    #endregion



    #region 实现抽象类
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
            // 设置最大存活时间，如果未设置(<=0)则默认为15秒
            m_MaxLifetimes[index] = info.totalLifetime > 0 ? info.totalLifetime : 15f;
            m_LastAngles[index] = info.direction;
            m_IsDead[index] = false;

            m_Accelerations[index] = 0f;
            m_AccelAngles[index] = 0f;
            m_AngularVelocities[index] = 0f;

            m_ActiveVisualIDs[index] = visualID;
            m_EntityBehaviorIDs[index] = behaviorID;

            m_HP[index] = 30f;

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

            m_Transforms.Add(obj.transform);
            m_ActiveGOs.Add(obj);
            m_ActiveCount++;
        }
        m_PendingEnemy.Clear();
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
        if (m_HP.IsCreated) m_HP.Dispose();
    }

    protected override void OnInitialize()
    {
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
        m_HP[index] = m_HP[lastIndex];
    }

    #endregion

    #region 玩家被命中的逻辑
    private void OnPlayerHit()
    {
        Debug.Log("<color=red>玩家中弹！</color>");
        if (playerSpriteRenderer == null && BattleManager.Instance != null && BattleManager.Instance.player != null)
        {
            playerSpriteRenderer = BattleManager.Instance.player.GetComponent<SpriteRenderer>();
        }
        if (hitEffectCoroutine != null) StopCoroutine(hitEffectCoroutine);
        hitEffectCoroutine = StartCoroutine(HitFlashRoutine());
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

    #region Pool & Helper Methods
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