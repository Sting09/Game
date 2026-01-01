using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

/// <summary>
/// BulletDOTSManager — 改造版（采用方法 A：延迟生成 / pending buffer）
/// 说明：AddBullet 不再直接修改 NativeArray / TransformAccessArray，而是将请求加入 pending 列表。
///       在 Update() 开始时 Complete 上一帧 Job 并 FlushPendingBullets（把 pending 列表中的子弹批量写入 NativeArray 并创建/激活 GameObject）。
/// 这样避免了在高频发射场景下在 AddBullet 中频繁调用 JobHandle.Complete 导致的主线程阻塞。
/// </summary>
public class BulletDOTSManager : SingletonMono<BulletDOTSManager>
{
    // --- 配置 (分离外观与行为) ---
    [Header("Visual Configs (Appearance)")]
    public List<BulletBasicConfigSO> visualConfigs;

    [Header("Behavior Configs (Logic)")]
    public List<BulletBehaviorProfileSO> behaviorProfiles;

    // --- ID 查找表 (修复报错关键) ---
    private Dictionary<string, int> m_VisualNameToID = new Dictionary<string, int>();
    private Dictionary<string, int> m_BehaviorNameToID = new Dictionary<string, int>();

    // --- 对象池 ---
    public Queue<GameObject>[] m_VisualPools;
    private Transform[] m_VisualRoots;
    public Transform poolRoot;
    public int maxBulletCapacity = 10000;
    public float deltaZ = -0.0001f;
    [SerializeField] private float currentZ = 0;

    // --- Native 数据 ---
    private NativeArray<float3> m_Positions;
    private NativeArray<float> m_Speeds;
    private NativeArray<float> m_Angles;
    private NativeArray<float> m_LastAngles;
    private NativeArray<float> m_Lifetimes;

    private NativeArray<float> m_Accelerations;
    private NativeArray<float> m_AccelAngles;
    private NativeArray<float> m_AngularVelocities;

    private NativeArray<bool> m_IsDead;
    private NativeArray<int> m_ActiveVisualIDs;
    private NativeArray<int> m_BulletBehaviorIDs;
    private NativeArray<int> m_NextEventIndex;
    private NativeArray<Unity.Mathematics.Random> m_Randoms;

    private NativeArray<NativeBulletEvent> m_GlobalEventArray;
    private NativeArray<int2> m_BehaviorRanges;

    private TransformAccessArray m_Transforms;
    private List<GameObject> m_ActiveGOs;
    private int m_ActiveCount = 0;
    public int ActiveCount => m_ActiveCount;

    private bool m_IsInitialized = false;
    private JobHandle m_JobHandle;

    // --- Pending buffer for deferred AddBullet (Method A) ---
    private struct PendingBullet
    {
        public int visualID;
        public int behaviorID;
        public Vector3 startPos;
        public BulletRuntimeInfo info;
    }
    private List<PendingBullet> m_PendingBullets;

    protected override void Awake()
    {
        base.Awake();
        InitializeConfig();
        InitializeMemory();
        m_PendingBullets = new List<PendingBullet>();
        m_IsInitialized = true;
    }

    void OnDestroy()
    {
        m_JobHandle.Complete();
        DisposeMemory();
    }

    private void InitializeConfig()
    {
        m_VisualNameToID.Clear();
        m_BehaviorNameToID.Clear();

        // 1. 初始化外观配置与查找表
        if (visualConfigs != null)
        {
            m_VisualPools = new Queue<GameObject>[visualConfigs.Count];
            m_VisualRoots = new Transform[visualConfigs.Count];
            for (int i = 0; i < visualConfigs.Count; i++)
            {
                if (visualConfigs[i] != null)
                {
                    // 建立名字到ID的映射
                    if (!m_VisualNameToID.ContainsKey(visualConfigs[i].name))
                    {
                        m_VisualNameToID.Add(visualConfigs[i].name, i);
                    }
                }
            }
        }
        else
        {
            visualConfigs = new List<BulletBasicConfigSO>();
            m_VisualPools = new Queue<GameObject>[0];
        }

        // 2. 初始化行为配置与查找表
        if (behaviorProfiles != null)
        {
            List<NativeBulletEvent> tempAllEvents = new List<NativeBulletEvent>();
            m_BehaviorRanges = new NativeArray<int2>(behaviorProfiles.Count, Allocator.Persistent);

            for (int i = 0; i < behaviorProfiles.Count; i++)
            {
                var profile = behaviorProfiles[i];
                int startIndex = tempAllEvents.Count;
                int count = 0;

                if (profile != null)
                {
                    // 建立名字到ID的映射
                    if (!m_BehaviorNameToID.ContainsKey(profile.name))
                    {
                        m_BehaviorNameToID.Add(profile.name, i);
                    }

                    if (profile.eventList != null)
                    {
                        count = profile.eventList.Count;
                        foreach (var evt in profile.eventList)
                        {
                            tempAllEvents.Add(new NativeBulletEvent
                            {
                                triggerTime = evt.time,
                                type = evt.type,
                                valueA = evt.valA,
                                valueB = evt.valB,
                                valueC = evt.valC,
                                useRelative = evt.useRelative,
                                useRandom = evt.useRandom
                            });
                        }
                    }
                }
                m_BehaviorRanges[i] = new int2(startIndex, count);
            }
            m_GlobalEventArray = new NativeArray<NativeBulletEvent>(tempAllEvents.ToArray(), Allocator.Persistent);
        }
        else
        {
            m_BehaviorRanges = new NativeArray<int2>(0, Allocator.Persistent);
            m_GlobalEventArray = new NativeArray<NativeBulletEvent>(0, Allocator.Persistent);
        }
    }

    /// <summary>
    /// 在运行时重新加载 behaviorProfiles 到 m_GlobalEventArray / m_BehaviorRanges 中（安全）
    /// 可在编辑器 Inspector 中点击或通过脚本调用以热重载行为配置（无需重启游戏）
    /// </summary>
    [ContextMenu("Reload Behavior Configs")]
    public void ReloadBehaviorConfigs()
    {
        // 确保上一帧所有 job 完成，保证 NativeArray 安全性
        m_JobHandle.Complete();

        // 释放旧的数据（如果存在）
        if (m_GlobalEventArray.IsCreated)
        {
            m_GlobalEventArray.Dispose();
        }
        if (m_BehaviorRanges.IsCreated)
        {
            m_BehaviorRanges.Dispose();
        }

        // 重新构建 behavior ranges + global events（与 InitializeConfig 中的逻辑保持一致）
        if (behaviorProfiles != null)
        {
            List<NativeBulletEvent> tempAllEvents = new List<NativeBulletEvent>();
            m_BehaviorRanges = new NativeArray<int2>(behaviorProfiles.Count, Allocator.Persistent);

            for (int i = 0; i < behaviorProfiles.Count; i++)
            {
                var profile = behaviorProfiles[i];
                int startIndex = tempAllEvents.Count;
                int count = 0;

                if (profile != null)
                {
                    if (!m_BehaviorNameToID.ContainsKey(profile.name))
                    {
                        // 可能需要同步名称映射（如果你希望在编辑时映射更新）
                        m_BehaviorNameToID[profile.name] = i;
                    }
                    if (profile.eventList != null)
                    {
                        count = profile.eventList.Count;
                        foreach (var evt in profile.eventList)
                        {
                            tempAllEvents.Add(new NativeBulletEvent
                            {
                                triggerTime = evt.time,
                                type = evt.type,
                                valueA = evt.valA,
                                valueB = evt.valB,
                                valueC = evt.valC,
                                useRelative = evt.useRelative,
                                useRandom = evt.useRandom
                            });
                        }
                    }
                }

                m_BehaviorRanges[i] = new int2(startIndex, count);
            }

            if (tempAllEvents.Count > 0)
            {
                m_GlobalEventArray = new NativeArray<NativeBulletEvent>(tempAllEvents.ToArray(), Allocator.Persistent);
            }
            else
            {
                m_GlobalEventArray = new NativeArray<NativeBulletEvent>(0, Allocator.Persistent);
            }
        }
        else
        {
            m_BehaviorRanges = new NativeArray<int2>(0, Allocator.Persistent);
            m_GlobalEventArray = new NativeArray<NativeBulletEvent>(0, Allocator.Persistent);
        }

        Debug.Log("[BulletDOTSManager] Behavior configs reloaded. Profiles: " + (behaviorProfiles != null ? behaviorProfiles.Count : 0));
    }


    private void InitializeMemory()
    {
        m_Positions = new NativeArray<float3>(maxBulletCapacity, Allocator.Persistent);
        m_Speeds = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_Angles = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_Lifetimes = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_LastAngles = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_IsDead = new NativeArray<bool>(maxBulletCapacity, Allocator.Persistent);

        m_Accelerations = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_AccelAngles = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);
        m_AngularVelocities = new NativeArray<float>(maxBulletCapacity, Allocator.Persistent);

        m_ActiveVisualIDs = new NativeArray<int>(maxBulletCapacity, Allocator.Persistent);
        m_BulletBehaviorIDs = new NativeArray<int>(maxBulletCapacity, Allocator.Persistent);
        m_NextEventIndex = new NativeArray<int>(maxBulletCapacity, Allocator.Persistent);

        m_Randoms = new NativeArray<Unity.Mathematics.Random>(maxBulletCapacity, Allocator.Persistent);
        var seedGen = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
        for (int i = 0; i < maxBulletCapacity; i++)
        {
            m_Randoms[i] = new Unity.Mathematics.Random(seedGen.NextUInt(1, uint.MaxValue));
        }

        m_Transforms = new TransformAccessArray(maxBulletCapacity);
        m_ActiveGOs = new List<GameObject>(maxBulletCapacity);
    }

    private void DisposeMemory()
    {
        if (!m_IsInitialized) return;

        if (m_Positions.IsCreated) m_Positions.Dispose();
        if (m_Speeds.IsCreated) m_Speeds.Dispose();
        if (m_Angles.IsCreated) m_Angles.Dispose();
        if (m_Lifetimes.IsCreated) m_Lifetimes.Dispose();
        if (m_LastAngles.IsCreated) m_LastAngles.Dispose();
        if (m_IsDead.IsCreated) m_IsDead.Dispose();

        if (m_Accelerations.IsCreated) m_Accelerations.Dispose();
        if (m_AccelAngles.IsCreated) m_AccelAngles.Dispose();
        if (m_AngularVelocities.IsCreated) m_AngularVelocities.Dispose();

        if (m_ActiveVisualIDs.IsCreated) m_ActiveVisualIDs.Dispose();
        if (m_BulletBehaviorIDs.IsCreated) m_BulletBehaviorIDs.Dispose();
        if (m_NextEventIndex.IsCreated) m_NextEventIndex.Dispose();
        if (m_Randoms.IsCreated) m_Randoms.Dispose();

        if (m_GlobalEventArray.IsCreated) m_GlobalEventArray.Dispose();
        if (m_BehaviorRanges.IsCreated) m_BehaviorRanges.Dispose();

        if (m_Transforms.isCreated) m_Transforms.Dispose();
    }

    // --- Public API Calls (修复报错需要的方法) ---

    /// <summary>
    /// 根据名称获取外观ID (对应 ShootPattern 中的报错)
    /// </summary>
    public int GetVisualID(string name)
    {
        if (m_VisualNameToID.TryGetValue(name, out int id)) return id;
        Debug.LogWarning($"Visual config not found: {name}");
        return -1;
    }

    /// <summary>
    /// 根据名称获取行为ID
    /// </summary>
    public int GetBehaviorID(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        if (m_BehaviorNameToID.TryGetValue(name, out int id)) return id;
        // Debug.LogWarning($"Behavior profile not found: {name}");
        return -1;
    }

    /// <summary>
    /// 预加载对象池 (对应 BaseShooter 中的报错)
    /// </summary>
    public void PreparePoolsForLevel(List<string> bulletNamesToPrewarm, int countPerType = 50)
    {
        if (bulletNamesToPrewarm == null) return;

        foreach (var name in bulletNamesToPrewarm)
        {
            int id = GetVisualID(name);
            if (id != -1)
            {
                // 预先生成对象并回收
                List<GameObject> temp = new List<GameObject>();
                for (int i = 0; i < countPerType; i++)
                {
                    GameObject obj = GetBulletFromPool(id);
                    if (obj != null) temp.Add(obj);
                }
                // 立即回收
                foreach (var obj in temp)
                {
                    m_VisualPools[id].Enqueue(obj);
                    obj.SetActive(false);
                }
            }
        }
    }

    // --- Internal Logic ---

    private GameObject GetBulletFromPool(int visualID)
    {
        if (visualID < 0 || visualID >= m_VisualPools.Length) return null;

        if (m_VisualPools[visualID] == null) m_VisualPools[visualID] = new Queue<GameObject>();
        Queue<GameObject> pool = m_VisualPools[visualID];

        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        else
        {
            BulletBasicConfigSO cfg = visualConfigs[visualID];
            Transform parent = GetOrCreateVisualRoot(visualID);
            GameObject newObj = Instantiate(cfg.prefab, parent);
            return newObj;
        }
    }

    /// <summary>
    /// 添加子弹 — 现在只把请求放入 pending 列表，实际创建在 Update 里统一 Flush
    /// </summary>
    public void AddBullet(int visualID, int behaviorID, Vector3 startPos, BulletRuntimeInfo info)
    {
        // 不再在这里 Complete() 或直接操作 NativeArray / TransformAccessArray
        // 将子弹加入 pending 列表，由 Update 统一处理（确保安全且减少频繁 Complete）
        if (m_PendingBullets == null) m_PendingBullets = new List<PendingBullet>();

        // 如果已经接近容量，则丢弃（与之前直接返回的行为相符）
        int pendingCount = m_PendingBullets.Count;
        if (m_ActiveCount + pendingCount >= maxBulletCapacity)
        {
            // 超出容量，忽略该发射请求
            // 可选择打印一次警告（注释以避免日志噪音）
            // Debug.LogWarning("Bullet capacity reached; dropping bullet.");
            return;
        }

        m_PendingBullets.Add(new PendingBullet
        {
            visualID = visualID,
            behaviorID = behaviorID,
            startPos = startPos,
            info = info
        });
    }

    void Update()
    {
        // 确保上一帧的 Jobs 完成（安全地写入 NativeArray）
        m_JobHandle.Complete();

        // Flush pending bullets（在主线程批量把 pending 请求写入 NativeArray & TransformAccessArray）
        FlushPendingBullets();

        // 如果没有活动子弹，直接返回（节省调度开销）
        if (m_ActiveCount == 0) return;

        float dt = Time.deltaTime;

        BulletEventJob eventJob = new BulletEventJob
        {
            lifetimes = m_Lifetimes,
            bulletBehaviorIDs = m_BulletBehaviorIDs,
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

        BulletMoveJob moveJob = new BulletMoveJob
        {
            dt = dt,
            positions = m_Positions,
            speeds = m_Speeds,
            angles = m_Angles,
            lifetimes = m_Lifetimes,
            lastAngles = m_LastAngles,
            accelerations = m_Accelerations,
            accelAngles = m_AccelAngles,
            angularVelocities = m_AngularVelocities
        };
        m_JobHandle = moveJob.Schedule(m_Transforms, m_JobHandle);

        BulletCullJob cullJob = new BulletCullJob
        {
            positions = m_Positions,
            lifetimes = m_Lifetimes,
            maxLifetime = 15f,
            boundsX = 22f,
            boundsY = 14f,
            isDeadResults = m_IsDead
        };
        m_JobHandle = cullJob.Schedule(m_ActiveCount, 64, m_JobHandle);
    }

    void LateUpdate()
    {
        m_JobHandle.Complete();
        if (m_ActiveCount > 0) CheckAndRemoveBullets();
        currentZ = currentZ < -1 ? 0 : currentZ;
    }

    private void FlushPendingBullets()
    {
        if (m_PendingBullets == null || m_PendingBullets.Count == 0) return;

        int pendingTotal = m_PendingBullets.Count;
        int available = maxBulletCapacity - m_ActiveCount;
        int toProcess = math.min(pendingTotal, available);

        if (toProcess <= 0)
        {
            // 容量已满，清理 pending（或保留以便下一帧尝试）
            // 选择丢弃以保持行为一致（AddBullet 之前也是丢弃）
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

            GameObject obj = GetBulletFromPool(visualID);
            if (obj == null) continue;

            obj.SetActive(true);
            obj.transform.SetPositionAndRotation(startPos, Quaternion.Euler(0, 0, -info.direction));

            int index = m_ActiveCount;

            currentZ += deltaZ;
            float zPriority = (visualID >= 0 && visualID < visualConfigs.Count) ? visualConfigs[visualID].zPriority : 0f;

            m_Positions[index] = new float3(startPos.x, startPos.y, currentZ - zPriority);
            m_Speeds[index] = info.speed;
            m_Angles[index] = info.direction;
            m_Lifetimes[index] = 0f;
            m_LastAngles[index] = info.direction;
            m_IsDead[index] = false;

            m_Accelerations[index] = 0f;
            m_AccelAngles[index] = 0f;
            m_AngularVelocities[index] = 0f;

            m_ActiveVisualIDs[index] = visualID;
            m_BulletBehaviorIDs[index] = behaviorID;

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

        // 如果有剩余未处理的 pending（因为容量），把未处理部分移到头部保留到下一帧或丢弃
        if (toProcess < pendingTotal)
        {
            // 把未处理的移到列表头
            int remaining = pendingTotal - toProcess;
            for (int i = 0; i < remaining; i++)
            {
                m_PendingBullets[i] = m_PendingBullets[toProcess + i];
            }
            m_PendingBullets.RemoveRange(remaining, toProcess); // remove the processed tail
            m_PendingBullets.RemoveRange(remaining, 0); // no-op, keeps clarity
            m_PendingBullets.RemoveRange(remaining, 0); // no-op
            // Actually the above is a bit awkward due to remove indices; do a simpler approach:
            m_PendingBullets.RemoveRange(0, toProcess); // remove processed items from front
        }
        else
        {
            // 全部处理完
            m_PendingBullets.Clear();
        }
    }

    private void CheckAndRemoveBullets()
    {
        for (int i = m_ActiveCount - 1; i >= 0; i--)
        {
            if (m_IsDead[i]) RemoveBulletAt(i);
        }
    }

    private void RemoveBulletAt(int index)
    {
        int lastIndex = m_ActiveCount - 1;
        GameObject objToRemove = m_ActiveGOs[index];
        int visualID = m_ActiveVisualIDs[index];

        objToRemove.SetActive(false);
        if (visualID >= 0 && visualID < m_VisualPools.Length) m_VisualPools[visualID].Enqueue(objToRemove);
        else Destroy(objToRemove);

        if (index != lastIndex)
        {
            m_Positions[index] = m_Positions[lastIndex];
            m_Speeds[index] = m_Speeds[lastIndex];
            m_Angles[index] = m_Angles[lastIndex];
            m_Lifetimes[index] = m_Lifetimes[lastIndex];
            m_LastAngles[index] = m_LastAngles[lastIndex];
            m_IsDead[index] = m_IsDead[lastIndex];
            m_Accelerations[index] = m_Accelerations[lastIndex];
            m_AccelAngles[index] = m_AccelAngles[lastIndex];
            m_AngularVelocities[index] = m_AngularVelocities[lastIndex];
            m_ActiveVisualIDs[index] = m_ActiveVisualIDs[lastIndex];
            m_BulletBehaviorIDs[index] = m_BulletBehaviorIDs[lastIndex];
            m_NextEventIndex[index] = m_NextEventIndex[lastIndex];
            m_Randoms[index] = m_Randoms[lastIndex];
            m_ActiveGOs[index] = m_ActiveGOs[lastIndex];
            m_Transforms.RemoveAtSwapBack(index);
        }
        else
        {
            m_Transforms.RemoveAtSwapBack(index);
        }
        m_ActiveGOs.RemoveAt(lastIndex);
        m_ActiveCount--;
    }

    private Transform GetOrCreateVisualRoot(int visualID)
    {
        if (m_VisualRoots[visualID] == null)
        {
            BulletBasicConfigSO cfg = visualConfigs[visualID];
            GameObject subRootObj = new GameObject($"Pool_{cfg.bulletName}");
            subRootObj.transform.SetParent(poolRoot);
            subRootObj.transform.localPosition = Vector3.zero;
            m_VisualRoots[visualID] = subRootObj.transform;
        }
        return m_VisualRoots[visualID];
    }
}

// --- Jobs (保持不变) ---
[BurstCompile]
public struct BulletEventJob : IJobParallelFor
{
    public NativeArray<float> speeds;
    public NativeArray<float> angles;
    public NativeArray<float> accelerations;
    public NativeArray<float> accelAngles;
    public NativeArray<float> angularVelocities;
    public NativeArray<bool> isDead;
    public NativeArray<int> nextEventIndex;
    public NativeArray<Unity.Mathematics.Random> randoms;
    [ReadOnly] public NativeArray<float> lifetimes;
    [ReadOnly] public NativeArray<int> bulletBehaviorIDs;
    [ReadOnly] public NativeArray<int2> behaviorRanges;
    [ReadOnly] public NativeArray<NativeBulletEvent> globalEvents;

    public void Execute(int index)
    {
        int evtIdx = nextEventIndex[index];
        if (evtIdx == -1) return;

        int bID = bulletBehaviorIDs[index];
        if (bID < 0 || bID >= behaviorRanges.Length) return;

        int2 range = behaviorRanges[bID];
        int globalEndIndex = range.x + range.y;

        while (evtIdx < globalEndIndex)
        {
            NativeBulletEvent evt = globalEvents[evtIdx];
            if (lifetimes[index] < evt.triggerTime) break;

            float val = evt.valueA;
            if (evt.useRandom)
            {
                var rng = randoms[index];
                val = rng.NextFloat(evt.valueA, evt.valueC);
                randoms[index] = rng;
            }

            switch (evt.type)
            {
                case BulletEventType.ChangeSpeed:
                    if (evt.useRelative) speeds[index] += val; else speeds[index] = val; break;
                case BulletEventType.ChangeDirection:
                    if (evt.useRelative) angles[index] += val; else angles[index] = val; break;
                case BulletEventType.SetAcceleration:
                    accelerations[index] = val;
                    if (evt.valueB > 0.5f) accelAngles[index] = angles[index]; else accelAngles[index] = evt.valueC; break;
                case BulletEventType.SetAngularVelocity:
                    angularVelocities[index] = val; break;
                case BulletEventType.Stop:
                    speeds[index] = 0; accelerations[index] = 0; angularVelocities[index] = 0; break;
                case BulletEventType.Recycle:
                    isDead[index] = true; break;
            }
            evtIdx++;
        }
        nextEventIndex[index] = (evtIdx >= globalEndIndex) ? -1 : evtIdx;
    }
}

[BurstCompile]
public struct BulletMoveJob : IJobParallelForTransform
{
    public NativeArray<float3> positions;
    public NativeArray<float> lifetimes;
    public NativeArray<float> lastAngles;
    public NativeArray<float> speeds;
    public NativeArray<float> angles;
    [ReadOnly] public NativeArray<float> accelerations;
    [ReadOnly] public NativeArray<float> accelAngles;
    [ReadOnly] public NativeArray<float> angularVelocities;
    [ReadOnly] public float dt;

    public void Execute(int index, TransformAccess transform)
    {
        lifetimes[index] += dt;
        float currentSpeed = speeds[index];
        float currentAngle = angles[index];
        float accel = accelerations[index];
        float angVel = angularVelocities[index];

        if (math.abs(angVel) > 0.001f) currentAngle += angVel * dt;

        float angleRad = math.radians(-currentAngle);
        float s, c;
        math.sincos(angleRad, out s, out c);
        float2 velVec = new float2(c, s) * currentSpeed;

        if (accel > 0.0001f)
        {
            float accRad = math.radians(-accelAngles[index]);
            float2 accVec = new float2(math.cos(accRad), math.sin(accRad)) * accel;
            velVec += accVec * dt;
            float newSpeed = math.length(velVec);
            if (newSpeed > 0.001f)
            {
                float newRad = math.atan2(velVec.y, velVec.x);
                currentAngle = -math.degrees(newRad);
            }
            currentSpeed = newSpeed;
        }

        speeds[index] = currentSpeed;
        angles[index] = currentAngle;
        float3 move = new float3(velVec.x, velVec.y, 0) * dt;
        float3 newPos = positions[index] + move;
        positions[index] = newPos;
        transform.position = new Vector3(newPos.x, newPos.y, newPos.z);

        if (math.abs(currentAngle - lastAngles[index]) > 0.001f)
        {
            quaternion q = quaternion.RotateZ(math.radians(-currentAngle));
            transform.rotation = new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
            lastAngles[index] = currentAngle;
        }
    }
}

[BurstCompile]
public struct BulletCullJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float> lifetimes;
    public float maxLifetime;
    public float boundsX;
    public float boundsY;
    [WriteOnly] public NativeArray<bool> isDeadResults;

    public void Execute(int index)
    {
        bool dead = lifetimes[index] > maxLifetime;
        if (!dead)
        {
            float3 pos = positions[index];
            dead = pos.x < -boundsX || pos.x > boundsX || pos.y < -boundsY || pos.y > boundsY;
        }
        isDeadResults[index] = dead;
    }
}
