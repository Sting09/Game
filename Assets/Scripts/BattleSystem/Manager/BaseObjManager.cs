using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class BaseObjManager<T> : SingletonMono<T> where T : BaseObjManager<T>
{
    // --- ?????? ---
    [Header("Player Settings")]
    [Tooltip("????????")]
    public float playerHitboxRadius;
    public float playerHitboxRate = 1f;
    protected SpriteRenderer playerSpriteRenderer;
    protected Coroutine hitEffectCoroutine;

    // --- ???????? ---
    [Header("Debug Settings")]
    [Tooltip("?????Scene????????????????")]
    public bool showDebugGizmos = true;
    [Tooltip("????????????")]
    public Color debugGizmoColor = Color.green;

    // --- ???? ---
    [Header("Behavior Configs (Logic)")]
    public List<EntityEventGroup> behaviorProfiles;

    // --- ID ????? ---
    protected Dictionary<string, int> m_BehaviorNameToID = new Dictionary<string, int>();

    // --- ????? ---
    public Queue<GameObject>[] m_VisualPools;
    public Transform[] m_VisualRoots;
    public Transform poolRoot;
    public int maxEntityCapacity = 10000;
    [SerializeField] protected int m_ActiveCount = 0;

    // --- ?????????? ---
    public float deltaZ = -1e-05f;
    [SerializeField] protected float currentZ = 0;

    // --- ?????? ---
    protected float boundsX;
    protected float boundsY;

    // --- ??????????? ---
    protected float dt;
    protected float3 playerPos;

    #region Job???????????
    // --- Native ???? ---

    //====================================================================================================
    // ???????????????? BaseRemoveObjectAt?? ????OnSwapData ?? DisposeMemory ????????????????????
    //====================================================================================================


    public NativeArray<float3> m_Positions;
    protected NativeArray<float> m_Speeds;
    public NativeArray<float> m_Angles;
    protected NativeArray<float> m_LastAngles;

    protected NativeArray<float> m_Lifetimes;
    protected NativeArray<float> m_MaxLifetimes;

    protected NativeArray<float> m_Accelerations;
    protected NativeArray<float> m_AccelAngles;
    protected NativeArray<float> m_AngularVelocities;

    protected NativeArray<bool> m_IsDead;
    protected NativeArray<int> m_ActiveVisualIDs;
    protected NativeArray<int> m_EntityBehaviorIDs;
    protected NativeArray<int> m_NextEventIndex;
    protected NativeArray<Unity.Mathematics.Random> m_Randoms;

    protected NativeArray<NativeEntityEvent> m_GlobalEventArray;
    protected NativeArray<int2> m_BehaviorRanges;

    // --- ?????? Native ???? ---
    public NativeArray<int> m_CollisionTypes;
    public NativeArray<float> m_CircleRadii;
    public NativeArray<float2> m_BoxSizes;

    protected NativeQueue<int> m_CollisionQueue;

    // --- ????????? Native ???? (????) ---
    protected NativeArray<bool> m_IsRelative; // ??????????
    protected NativeArray<int> m_EmitterIDs;  // ??????? InstanceID

    // --- ?????????????????? (????) ---
    // ??????????????????? Transform (Key: InstanceID)
    protected Dictionary<int, Transform> m_ActiveEmitters = new Dictionary<int, Transform>();
    // ??????????????????????????? (Key: InstanceID)
    protected Dictionary<int, float3> m_LastEmitterPos = new Dictionary<int, float3>();
    protected List<int> m_EmittersToRemoveCache = new List<int>(128); // ?????????????????
    // ?????????????/????????? ID ?????????????????????????
    protected List<int> m_DeadEmittersThisFrame = new List<int>(128);
    // ???? Job ???????????
    protected NativeHashMap<int, float3> m_EmitterDeltas;

    protected TransformAccessArray m_Transforms;
    protected List<GameObject> m_ActiveGOs;

    protected bool m_IsInitialized = false;
    protected JobHandle m_JobHandle;
    #endregion

    #region Awake / Start / Destroy
    protected override void Awake()
    {
        // 1. ???????????? Awake???????????????????
        base.Awake();

        // ????????????????
        if (Instance != this)
        {
            return;
        }

        // 3. ???????????????????????????????
        InitializeBaseConfig();
        InitializeBaseMemory();
        //?????????????????
        OnInitialize();

        if (GlobalSetting.Instance != null && GlobalSetting.Instance.globalVariable != null)
        {
            boundsX = GlobalSetting.Instance.globalVariable.halfWidth;
            boundsY = GlobalSetting.Instance.globalVariable.halfHeight;
            playerHitboxRadius = GlobalSetting.Instance.globalVariable.playerHitboxRadius;
        }
        else
        {
            boundsX = 10f;
            boundsY = 10f;
            playerHitboxRadius = 0.05f;
        }
        m_IsInitialized = true;
    }

    private void Start()
    {
        if (BattleManager.Instance != null && BattleManager.Instance.player != null)
        {
            playerSpriteRenderer = BattleManager.Instance.player.GetComponent<SpriteRenderer>();
        }
    }

    void OnDestroy()
    {
        m_JobHandle.Complete();
        DisposeMemory();
        OnDispose();
    }
    #endregion

    #region ?????????????
    private void InitializeBaseConfig()
    {
        m_BehaviorNameToID.Clear();

        if (behaviorProfiles != null)
        {
            List<NativeEntityEvent> tempAllEvents = new List<NativeEntityEvent>();
            m_BehaviorRanges = new NativeArray<int2>(behaviorProfiles.Count, Allocator.Persistent);

            for (int i = 0; i < behaviorProfiles.Count; i++)
            {
                var profile = behaviorProfiles[i];
                int startIndex = tempAllEvents.Count;
                int count = 0;

                if (profile != null)
                {
                    if (!m_BehaviorNameToID.ContainsKey(profile.name))
                        m_BehaviorNameToID.Add(profile.name, i);

                    if (profile.eventList != null)
                    {
                        count = profile.eventList.Count;
                        foreach (var evt in profile.eventList)
                        {
                            tempAllEvents.Add(new NativeEntityEvent
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
            m_GlobalEventArray = new NativeArray<NativeEntityEvent>(tempAllEvents.ToArray(), Allocator.Persistent);
        }
        else
        {
            m_BehaviorRanges = new NativeArray<int2>(0, Allocator.Persistent);
            m_GlobalEventArray = new NativeArray<NativeEntityEvent>(0, Allocator.Persistent);
        }
    }

    private void InitializeBaseMemory()
    {
        m_Positions = new NativeArray<float3>(maxEntityCapacity, Allocator.Persistent);
        m_Speeds = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_Angles = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_Lifetimes = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_MaxLifetimes = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent); // ?????????
        m_LastAngles = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_IsDead = new NativeArray<bool>(maxEntityCapacity, Allocator.Persistent);

        m_Accelerations = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_AccelAngles = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_AngularVelocities = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);

        m_ActiveVisualIDs = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent);
        m_EntityBehaviorIDs = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent);
        m_NextEventIndex = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent);

        m_CollisionTypes = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent);
        m_CircleRadii = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_BoxSizes = new NativeArray<float2>(maxEntityCapacity, Allocator.Persistent);
        m_CollisionQueue = new NativeQueue<int>(Allocator.Persistent);

        // --- ??????????????? ---
        m_IsRelative = new NativeArray<bool>(maxEntityCapacity, Allocator.Persistent);
        m_EmitterIDs = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent);
        m_EmitterDeltas = new NativeHashMap<int, float3>(128, Allocator.Persistent);
        // m_EmitterDeltas ?? Update ????????/??? (Allocator.TempJob)

        m_Randoms = new NativeArray<Unity.Mathematics.Random>(maxEntityCapacity, Allocator.Persistent);
        var seedGen = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
        for (int i = 0; i < maxEntityCapacity; i++)
        {
            m_Randoms[i] = new Unity.Mathematics.Random(seedGen.NextUInt(1, uint.MaxValue));
        }

        m_Transforms = new TransformAccessArray(maxEntityCapacity);
        m_ActiveGOs = new List<GameObject>(maxEntityCapacity);
    }



    private void DisposeMemory()
    {
        if (!m_IsInitialized) return;

        if (m_Positions.IsCreated) m_Positions.Dispose();
        if (m_Speeds.IsCreated) m_Speeds.Dispose();
        if (m_Angles.IsCreated) m_Angles.Dispose();
        if (m_Lifetimes.IsCreated) m_Lifetimes.Dispose();
        if (m_MaxLifetimes.IsCreated) m_MaxLifetimes.Dispose(); // ????????
        if (m_LastAngles.IsCreated) m_LastAngles.Dispose();
        if (m_IsDead.IsCreated) m_IsDead.Dispose();
        if (m_Accelerations.IsCreated) m_Accelerations.Dispose();
        if (m_AccelAngles.IsCreated) m_AccelAngles.Dispose();
        if (m_AngularVelocities.IsCreated) m_AngularVelocities.Dispose();
        if (m_ActiveVisualIDs.IsCreated) m_ActiveVisualIDs.Dispose();
        if (m_EntityBehaviorIDs.IsCreated) m_EntityBehaviorIDs.Dispose();
        if (m_NextEventIndex.IsCreated) m_NextEventIndex.Dispose();
        if (m_Randoms.IsCreated) m_Randoms.Dispose();
        if (m_GlobalEventArray.IsCreated) m_GlobalEventArray.Dispose();
        if (m_BehaviorRanges.IsCreated) m_BehaviorRanges.Dispose();

        if (m_CollisionTypes.IsCreated) m_CollisionTypes.Dispose();
        if (m_CircleRadii.IsCreated) m_CircleRadii.Dispose();
        if (m_BoxSizes.IsCreated) m_BoxSizes.Dispose();
        if (m_CollisionQueue.IsCreated) m_CollisionQueue.Dispose();

        // --- ????????????? ---
        if (m_IsRelative.IsCreated) m_IsRelative.Dispose();
        if (m_EmitterIDs.IsCreated) m_EmitterIDs.Dispose();
        // m_EmitterDeltas ?? JobHandle ???????????????? Destroy ????? Clean ??
        if (m_EmitterDeltas.IsCreated) m_EmitterDeltas.Dispose();

        if (m_Transforms.isCreated) m_Transforms.Dispose();
    }


    // --- ????????????????? ---
    /// <summary>
    /// ????????????????? Awake????
    /// ??????????????????? NativeArray??
    /// </summary>
    protected abstract void OnInitialize();

    /// <summary>
    /// ????????????????? OnDestroy????
    /// ?????? Dispose ????????? NativeArray??
    /// </summary>
    protected abstract void OnDispose();
    #endregion

    #region Update ???
    protected virtual void Update()
    {
        // 1. ??????? Job ????
        m_JobHandle.Complete();

        // 2. ??????????? HashMap
        m_EmitterDeltas.Clear();

        // 3. ???????????????????
        HandleCollisions();

        // 4. ??????????????
        FlushPending();

        if (m_ActiveCount == 0) return;

        // 5. ?? dt ?????
        dt = Time.deltaTime;
        playerPos = float3.zero;
        if (BattleManager.Instance != null)
        {
            playerPos = BattleManager.Instance.GetPlayerPos();
        }

        // --- 6. ???????????????????? (Delta) ---
        m_EmittersToRemoveCache.Clear();
        m_DeadEmittersThisFrame.Clear();

        foreach (var kvp in m_ActiveEmitters)
        {
            int id = kvp.Key;
            Transform t = kvp.Value;

            // ????????????? SetActive(false)???????????
            if (t == null || !t.gameObject.activeInHierarchy)
            {
                m_EmittersToRemoveCache.Add(id);
                m_DeadEmittersThisFrame.Add(id);
                continue;
            }

            float3 currentPos = t.position;
            float3 lastPos = currentPos;

            // ?????????? delta
            if (m_LastEmitterPos.TryGetValue(id, out float3 prev))
            {
                lastPos = prev;
            }

            float3 delta = currentPos - lastPos;

            // ?? HashMap?? ObjectMoveJob ??
            m_EmitterDeltas.TryAdd(id, delta);

            // ???????
            m_LastEmitterPos[id] = currentPos;
        }

        // 7. ????????????
        foreach (var id in m_EmittersToRemoveCache)
        {
            m_ActiveEmitters.Remove(id);
            m_LastEmitterPos.Remove(id);
        }

        // 8. ????????????????????????????????????????
        if (m_DeadEmittersThisFrame.Count > 0)
        {
            OnEmittersDeadThisFrame(m_DeadEmittersThisFrame);
        }

        // 9. ??????? / ?? / ?? / Cull ? Job
        ScheduleSpecificJobs();
    }

    void LateUpdate()
    {
        m_JobHandle.Complete();

        if (m_ActiveCount > 0) CheckAndRemoveObjects();
        currentZ = currentZ < -1 ? 0 : currentZ;
    }


    protected abstract void ScheduleSpecificJobs(); // ????????? Job

    protected abstract void FlushPending();


    protected abstract void HandleCollisions();


    private void CheckAndRemoveObjects()
    {
        for (int i = m_ActiveCount - 1; i >= 0; i--)
        {
            if (m_IsDead[i]) {
                BaseRemoveObjectAt(i);
            }
        }
    }

    protected void BaseRemoveObjectAt(int index)
    {
        int lastIndex = m_ActiveCount - 1;
        GameObject objToRemove = m_ActiveGOs[index];
        int visualID = m_ActiveVisualIDs[index];

        objToRemove.SetActive(false);
        if (visualID >= 0 && visualID < m_VisualPools.Length) m_VisualPools[visualID].Enqueue(objToRemove);
        else Destroy(objToRemove);

        // ?????????????????????????????
        if (index != lastIndex)
        {
            // ????????????????????????????????????????????
            // ??? lastIndex ????????
            OnSwapData(index, lastIndex);

            // --- ????????????? ---
            m_Positions[index] = m_Positions[lastIndex];
            m_Speeds[index] = m_Speeds[lastIndex];
            m_Angles[index] = m_Angles[lastIndex];
            m_Lifetimes[index] = m_Lifetimes[lastIndex];
            m_MaxLifetimes[index] = m_MaxLifetimes[lastIndex];
            m_LastAngles[index] = m_LastAngles[lastIndex];
            m_IsDead[index] = m_IsDead[lastIndex]; // ???????
            m_Accelerations[index] = m_Accelerations[lastIndex];
            m_AccelAngles[index] = m_AccelAngles[lastIndex];
            m_AngularVelocities[index] = m_AngularVelocities[lastIndex];
            m_ActiveVisualIDs[index] = m_ActiveVisualIDs[lastIndex];
            m_EntityBehaviorIDs[index] = m_EntityBehaviorIDs[lastIndex];
            m_NextEventIndex[index] = m_NextEventIndex[lastIndex];
            m_Randoms[index] = m_Randoms[lastIndex];

            m_CollisionTypes[index] = m_CollisionTypes[lastIndex];
            m_CircleRadii[index] = m_CircleRadii[lastIndex];
            m_BoxSizes[index] = m_BoxSizes[lastIndex];

            m_IsRelative[index] = m_IsRelative[lastIndex];
            m_EmitterIDs[index] = m_EmitterIDs[lastIndex];

            m_ActiveGOs[index] = m_ActiveGOs[lastIndex];
            m_Transforms.RemoveAtSwapBack(index);
        }
        else
        {
            m_Transforms.RemoveAtSwapBack(index);
        }

        // ??????????
        m_ActiveGOs.RemoveAt(lastIndex);
        m_ActiveCount--;
    }

    // ??????????? m_ActiveCount ?????? lastIndex???????? m_ActiveCount ?????
    // ???? lastIndex: ????????????????
    protected abstract void OnSwapData(int index, int lastIndex);

    protected virtual void OnEmittersDeadThisFrame(List<int> deadEmitterIds)
    {
        if (deadEmitterIds == null || deadEmitterIds.Count == 0) return;

        // 1. 创建 HashSet，使用 Allocator.TempJob
        NativeHashSet<int> deadSet = new NativeHashSet<int>(deadEmitterIds.Count, Allocator.TempJob);
        for (int i = 0; i < deadEmitterIds.Count; i++)
        {
            deadSet.Add(deadEmitterIds[i]);
        }

        // 2. 创建 Job
        EmitterDeathHandlingJob deathJob = new EmitterDeathHandlingJob
        {
            isRelative = m_IsRelative,
            emitterIDs = m_EmitterIDs,
            deadEmitterSet = deadSet,
            isDead = m_IsDead
        };

        // 3. 调度 Job (依赖之前的 m_JobHandle)
        m_JobHandle = deathJob.Schedule(m_ActiveCount, 64, m_JobHandle);

        // =========================================================
        // 【核心修复点】：
        // 不要在 Job 结构体里自动释放，而是这里告诉容器：“等 m_JobHandle 做完了，你就自己销毁”
        // 这样既不会报错，也不会阻塞主线程。
        // =========================================================
        deadSet.Dispose(m_JobHandle);
    }

    #endregion

    #region Pool & Helper Methods

    public int ActiveCount => m_ActiveCount;

    public int GetBehaviorID(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        if (m_BehaviorNameToID.TryGetValue(name, out int id)) return id;
        return -1;
    }

    public float UpdatePlayerColiision(float delta, float rate)
    {
        playerHitboxRadius += delta;
        playerHitboxRadius = Mathf.Max(playerHitboxRadius, 0f);
        playerHitboxRate += rate;
        playerHitboxRate = Mathf.Max(playerHitboxRate, 0f);
        return playerHitboxRadius * playerHitboxRate;
    }

    [ContextMenu("Reload Behavior Configs")]
    public void ReloadBehaviorConfigs()
    {
        InitializeBaseConfig();
    }

    // 在 BaseObjManager<T> 类中添加这个公共方法
    public JobHandle GetJobHandle()
    {
        return m_JobHandle;
    }

    #endregion
}