using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerShootingManager : BaseObjManager<PlayerShootingManager>
{
    // --- ���� ---
    [Header("Bullet Configs (Gameplay)")]
    public List<BulletBasicConfigSO> bulletConfigs;

    // --- �ӵ���ʽ ID ���ұ� ---
    private Dictionary<string, int> m_VisualNameToID = new Dictionary<string, int>();

    // ��ǰ֡Ҫ������ӵ��ӵ��б�
    private struct PendingBullet
    {
        public int visualID;
        public int behaviorID;
        public Vector3 startPos;
        public BulletRuntimeInfo info;
    }
    private List<PendingBullet> m_PendingBullets;



    /// <summary>
    /// �����ӵ���
    /// </summary>
    /// <param name="visualID">���ID</param>
    /// <param name="behaviorID">��ΪID</param>
    /// <param name="startPos">��ʼλ��</param>
    /// <param name="info">���в���</param>
    /// <param name="emitter">������Transform�����������ƶ��ӵ����˲������벻Ϊ�գ�</param>
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


    #region Ҫ�ɷ���Job
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

            // ����ƶ�����
            isRelative = m_IsRelative,
            emitterIDs = m_EmitterIDs,
            emitterDeltas = m_EmitterDeltas
        };
        // �����������ϵ��Job ������ m_JobHandle (EventJob)�����һ��ȡ m_EmitterDeltas
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
            maxLifetimes = m_MaxLifetimes, // �޸ģ�����ÿ���ӵ��������������
            cullBoundsX = boundsX,
            cullBoundsY = boundsY,
            isDeadResults = m_IsDead
        };
        m_JobHandle = cullJob.Schedule(m_ActiveCount, 64, m_JobHandle);
    }

    #endregion



    #region ʵ�ֳ�����
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
            // ���������ʱ�䣬���δ����(<=0)��Ĭ��Ϊ15��
            m_MaxLifetimes[index] = info.totalLifetime > 0 ? info.totalLifetime : 15f;
            m_LastAngles[index] = info.direction;
            m_IsDead[index] = false;

            m_Accelerations[index] = 0f;
            m_AccelAngles[index] = 0f;
            m_AngularVelocities[index] = 0f;

            m_ActiveVisualIDs[index] = visualID;
            m_EntityBehaviorIDs[index] = behaviorID;

            // --- ����ƶ��߼� ---
            bool isRel = false;
            int eID = 0;

            if (visualID >= 0 && visualID < bulletConfigs.Count)
            {
                BulletBasicConfigSO cfg = bulletConfigs[visualID];
                m_CollisionTypes[index] = (int)cfg.collisionType;
                m_CircleRadii[index] = cfg.circleRadius;
                m_BoxSizes[index] = cfg.boxSize;

                // ��������Ƿ�������ƶ����ҷ������Ƿ����
                if (pb.info.isRelative && emitter != null)
                {
                    isRel = true;
                    eID = emitter.GetInstanceID();

                    // ע�ᵽ��Ծ�������б����Ա� Update ����λ��
                    if (!m_ActiveEmitters.ContainsKey(eID))
                    {
                        m_ActiveEmitters.Add(eID, emitter);
                        // ��ʼ����һ֡λ��Ϊ��ǰλ�ã���ֹ��һ֡���䣩
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
        // 玩家子弹不与玩家发生碰撞，因此这里不处理 m_CollisionQueue（该队列只用于玩家圆形碰撞）
        while (m_CollisionQueue.TryDequeue(out _)) { }

        // 与敌人进行逐帧碰撞检测（玩家子弹 VS 敌人）
        EnemyDOTSManager enemyMgr = EnemyDOTSManager.Instance;
        if (enemyMgr == null) return;

        int bulletCount = m_ActiveCount;
        int enemyCount = enemyMgr.ActiveCount;
        if (bulletCount == 0 || enemyCount == 0) return;

        var bulletPositions = m_Positions;
        var bulletAngles = m_Angles;
        var bulletTypes = m_CollisionTypes;
        var bulletRadii = m_CircleRadii;
        var bulletBoxes = m_BoxSizes;
        var bulletIsDead = m_IsDead;

        var enemyPositions = enemyMgr.m_Positions;
        var enemyAngles = enemyMgr.m_Angles;
        var enemyTypes = enemyMgr.m_CollisionTypes;
        var enemyRadii = enemyMgr.m_CircleRadii;
        var enemyBoxes = enemyMgr.m_BoxSizes;
        var enemyHP = enemyMgr.m_HP;

        const float damagePerHit = 5f;

        for (int i = 0; i < bulletCount; i++)
        {
            if (bulletIsDead[i]) continue;

            float3 bPos = bulletPositions[i];
            int bType = bulletTypes[i];
            float bRadius = bulletRadii[i];
            float2 bSize = bulletBoxes[i];
            float bAngle = bulletAngles[i];

            bool bulletHit = false;

            for (int j = 0; j < enemyCount; j++)
            {
                // 已死亡或 HP 已空的敌人无需再参与碰撞
                if (enemyHP[j] <= 0f) continue;

                float3 ePos = enemyPositions[j];
                int eType = enemyTypes[j];
                float eRadius = enemyRadii[j];
                float2 eSize = enemyBoxes[j];
                float eAngle = enemyAngles[j];

                bool hit = CheckBulletEnemyCollision(
                    bPos.xy, bType, bRadius, bSize, bAngle,
                    ePos.xy, eType, eRadius, eSize, eAngle
                );

                if (!hit) continue;

                // 命中：扣除敌人 HP，并标记子弹为死亡（Cull）
                float newHp = enemyHP[j] - damagePerHit;
                enemyHP[j] = newHp;

                if (newHp <= 0f)
                {
                    // HP <= 0 时，敌人会在 EnemyCullJob 中被标记 isDead 并在 LateUpdate 中移除
                    enemyHP[j] = 0f;
                }

                bulletIsDead[i] = true;
                bulletHit = true;
                break; // 一个子弹命中一个敌人后即被移除
            }

            // 子弹已经被标记死亡，不再参与后续敌人的检测
            if (bulletHit) continue;
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

    #region Collision Helpers
    /// <summary>
    /// 子弹与敌人的通用碰撞入口（圆形 / 方形）
    /// </summary>
    private static bool CheckBulletEnemyCollision(
        float2 bulletPos, int bulletType, float bulletRadius, float2 bulletSize, float bulletAngle,
        float2 enemyPos, int enemyType, float enemyRadius, float2 enemySize, float enemyAngle)
    {
        // 0: 圆形, 1: 方形（与配置 ScriptableObject 保持一致）
        if (bulletType == 0)
        {
            if (enemyType == 0)
            {
                return CircleVsCircle(bulletPos, bulletRadius, enemyPos, enemyRadius);
            }
            else
            {
                return CircleVsBox(bulletPos, bulletRadius, enemyPos, enemySize, enemyAngle);
            }
        }
        else
        {
            if (enemyType == 0)
            {
                // 方形子弹 VS 圆形敌人
                return CircleVsBox(enemyPos, enemyRadius, bulletPos, bulletSize, bulletAngle);
            }
            else
            {
                return BoxVsBox(bulletPos, bulletSize, bulletAngle, enemyPos, enemySize, enemyAngle);
            }
        }
    }

    private static bool CircleVsCircle(float2 c1, float r1, float2 c2, float r2)
    {
        float2 diff = c1 - c2;
        float distSq = math.lengthsq(diff);
        float r = r1 + r2;
        return distSq < r * r;
    }

    private static bool CircleVsBox(float2 circleCenter, float radius, float2 boxCenter, float2 boxSize, float boxAngleDeg)
    {
        // 与 ObjectCollisionJob 中「玩家圆形 VS 子弹方形」一致的做法
        float2 diff = circleCenter - boxCenter;
        float angRad = math.radians(boxAngleDeg);
        float s, c;
        math.sincos(angRad, out s, out c);

        float2 localCirclePos = new float2(
            diff.x * c - diff.y * s,
            diff.x * s + diff.y * c
        );

        float2 halfExtents = boxSize / 2f;
        float2 closestPoint = math.clamp(localCirclePos, -halfExtents, halfExtents);
        float distSq = math.distancesq(localCirclePos, closestPoint);

        return distSq < radius * radius;
    }

    private static bool BoxVsBox(float2 centerA, float2 sizeA, float angleADeg,
                                 float2 centerB, float2 sizeB, float angleBDeg)
    {
        float2 halfA = sizeA / 2f;
        float2 halfB = sizeB / 2f;

        float angARad = math.radians(angleADeg);
        float sA, cA;
        math.sincos(angARad, out sA, out cA);

        float angBRad = math.radians(angleBDeg);
        float sB, cB;
        math.sincos(angBRad, out sB, out cB);

        float2[] axes = new float2[4];
        axes[0] = new float2(cA, sA);
        axes[1] = new float2(-sA, cA);
        axes[2] = new float2(cB, sB);
        axes[3] = new float2(-sB, cB);

        float2[] cornersA = new float2[4];
        cornersA[0] = centerA + new float2(halfA.x * cA - halfA.y * sA, halfA.x * sA + halfA.y * cA);
        cornersA[1] = centerA + new float2(-halfA.x * cA - halfA.y * sA, -halfA.x * sA + halfA.y * cA);
        cornersA[2] = centerA + new float2(halfA.x * cA + halfA.y * sA, halfA.x * sA - halfA.y * cA);
        cornersA[3] = centerA + new float2(-halfA.x * cA + halfA.y * sA, -halfA.x * sA - halfA.y * cA);

        float2[] cornersB = new float2[4];
        cornersB[0] = centerB + new float2(halfB.x * cB - halfB.y * sB, halfB.x * sB + halfB.y * cB);
        cornersB[1] = centerB + new float2(-halfB.x * cB - halfB.y * sB, -halfB.x * sB + halfB.y * cB);
        cornersB[2] = centerB + new float2(halfB.x * cB + halfB.y * sB, halfB.x * sB - halfB.y * cB);
        cornersB[3] = centerB + new float2(-halfB.x * cB + halfB.y * sB, -halfB.x * sB - halfB.y * cB);

        for (int k = 0; k < 4; k++)
        {
            float2 axis = axes[k];
            float minA = float.MaxValue;
            float maxA = float.MinValue;
            float minB = float.MaxValue;
            float maxB = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                float projA = math.dot(cornersA[i], axis);
                minA = math.min(minA, projA);
                maxA = math.max(maxA, projA);

                float projB = math.dot(cornersB[i], axis);
                minB = math.min(minB, projB);
                maxB = math.max(maxB, projB);
            }

            if (maxA < minB || maxB < minA)
            {
                // 在该分离轴上无重叠，两个盒子不相交
                return false;
            }
        }

        return true;
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

/*这是一个使用Unity开发的STG游戏。PlayerShootingManager.cs 管理玩家发射的子弹。请实现玩家发射的子弹的逻辑，满足以下功能：不检查玩家子弹和玩家的碰撞；每帧检查玩家子弹和所有敌人的碰撞，敌人由EnemyDOTSManager.cs 管理；碰撞分圆形和方形两种；如果玩家子弹与敌人有碰撞，敌人hp减少5，玩家子弹移除（Cull）；敌人hp为0时，敌人移除。*/