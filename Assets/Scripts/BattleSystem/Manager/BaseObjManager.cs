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
    // --- 玩家碰撞 ---
    [Header("Player Settings")]
    [Tooltip("玩家判定半径")]
    public float playerHitboxRadius;
    public float playerHitboxRate = 1f;
    protected SpriteRenderer playerSpriteRenderer;
    protected Coroutine hitEffectCoroutine;

    // --- 调试设置 ---
    [Header("Debug Settings")]
    [Tooltip("是否在Scene窗口显示子弹判定范围")]
    public bool showDebugGizmos = true;
    [Tooltip("子弹判定圆的颜色")]
    public Color debugGizmoColor = Color.green;

    // --- 配置 ---
    [Header("Behavior Configs (Logic)")]
    public List<EntityEventGroup> behaviorProfiles;

    // --- ID 查找表 ---
    protected Dictionary<string, int> m_BehaviorNameToID = new Dictionary<string, int>();

    // --- 对象池 ---
    public Queue<GameObject>[] m_VisualPools;
    public Transform[] m_VisualRoots;
    public Transform poolRoot;
    public int maxEntityCapacity = 10000;
    [SerializeField] protected int m_ActiveCount = 0;

    // --- 遮挡关系控制 ---
    public float deltaZ = -1e-05f;
    [SerializeField] protected float currentZ = 0;

    // --- 屏幕边界 ---
    protected float boundsX;
    protected float boundsY;

    // --- 每帧的数据缓存 ---
    protected float dt;
    protected float3 playerPos;

    #region Job系统的内部属性
    // --- Native 数据 ---

    //====================================================================================================
    // 添加数组时，务必在 BaseRemoveObjectAt、 子类OnSwapData 和 DisposeMemory 中逐一核对，确保不重不漏。
    //====================================================================================================


    protected NativeArray<float3> m_Positions;
    protected NativeArray<float> m_Speeds;
    protected NativeArray<float> m_Angles;
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

    // --- 碰撞相关 Native 数据 ---
    protected NativeArray<int> m_CollisionTypes;
    protected NativeArray<float> m_CircleRadii;
    protected NativeArray<float2> m_BoxSizes;

    protected NativeQueue<int> m_CollisionQueue;

    // --- 相对移动相关 Native 数据 (新增) ---
    protected NativeArray<bool> m_IsRelative; // 是否跟随发射者
    protected NativeArray<int> m_EmitterIDs;  // 发射者的 InstanceID

    // --- 相对移动主线程辅助数据 (新增) ---
    // 记录所有需要追踪的发射者 Transform (Key: InstanceID)
    protected Dictionary<int, Transform> m_ActiveEmitters = new Dictionary<int, Transform>();
    // 记录上一帧发射者的位置，用于计算差值 (Key: InstanceID)
    protected Dictionary<int, float3> m_LastEmitterPos = new Dictionary<int, float3>();
    protected List<int> m_EmittersToRemoveCache = new List<int>(128); // 预设容量，减少扩容
    // 传给 Job 的每帧位移数据
    protected NativeHashMap<int, float3> m_EmitterDeltas;

    protected TransformAccessArray m_Transforms;
    protected List<GameObject> m_ActiveGOs;

    protected bool m_IsInitialized = false;
    protected JobHandle m_JobHandle;
    #endregion

    #region Awake / Start / Destroy
    protected override void Awake()
    {
        // 1. 必须先调用父类 Awake，执行单例赋值和去重逻辑
        base.Awake();

        // 【关键步骤】检查单例状态
        if (Instance != this)
        {
            return;
        }

        // 3. 只有确认自己是真正的单例后，才初始化内存
        InitializeBaseConfig();
        InitializeBaseMemory();
        //子类自己的初始化方法
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

    #region 初始化与内存管理
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
        m_MaxLifetimes = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent); // 新增初始化
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

        // --- 初始化相对移动数组 ---
        m_IsRelative = new NativeArray<bool>(maxEntityCapacity, Allocator.Persistent);
        m_EmitterIDs = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent);
        m_EmitterDeltas = new NativeHashMap<int, float3>(128, Allocator.Persistent);
        // m_EmitterDeltas 在 Update 中每帧创建/释放 (Allocator.TempJob)

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
        if (m_MaxLifetimes.IsCreated) m_MaxLifetimes.Dispose(); // 新增回收
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

        // --- 释放相对移动数组 ---
        if (m_IsRelative.IsCreated) m_IsRelative.Dispose();
        if (m_EmitterIDs.IsCreated) m_EmitterIDs.Dispose();
        // m_EmitterDeltas 由 JobHandle 管理或手动释放，确保 Destroy 时它是 Clean 的
        if (m_EmitterDeltas.IsCreated) m_EmitterDeltas.Dispose();

        if (m_Transforms.isCreated) m_Transforms.Dispose();
    }


    // --- 抽象接口：强制子类实现 ---
    /// <summary>
    /// 子类初始化专用接口（替代 Awake）。
    /// 在这里申请子类特有的 NativeArray。
    /// </summary>
    protected abstract void OnInitialize();

    /// <summary>
    /// 子类销毁专用接口（替代 OnDestroy）。
    /// 在这里 Dispose 子类特有的 NativeArray。
    /// </summary>
    protected abstract void OnDispose();
    #endregion

    #region Update 循环
    protected virtual void Update()
    {
        // 确保上一帧 Job 完成
        m_JobHandle.Complete();

        // 清空上一帧的 HashMap
        m_EmitterDeltas.Clear();

        HandleCollisions();

        FlushPending();

        if (m_ActiveCount == 0) return;

        dt = Time.deltaTime;
        playerPos = float3.zero;
        if (BattleManager.Instance != null)
        {
            playerPos = BattleManager.Instance.GetPlayerPos();
        }

        // --- 计算所有活跃发射者的位移 (Delta) ---
        m_EmittersToRemoveCache.Clear();

        foreach (var kvp in m_ActiveEmitters)
        {
            int id = kvp.Key;
            Transform t = kvp.Value;

            // 如果敌人死了/销毁了
            if (t == null)
            {
                m_EmittersToRemoveCache.Add(id);
                continue;
            }

            float3 currentPos = t.position;
            float3 lastPos = currentPos;

            // 获取上一帧位置，计算差值
            if (m_LastEmitterPos.TryGetValue(id, out float3 prev))
            {
                lastPos = prev;
            }

            float3 delta = currentPos - lastPos;

            // 存入 HashMap 供 Job 使用
            m_EmitterDeltas.TryAdd(id, delta);

            // 更新记录
            m_LastEmitterPos[id] = currentPos;
        }

        // 清理已销毁的发射者
        foreach (var id in m_EmittersToRemoveCache)
        {
            m_ActiveEmitters.Remove(id);
            m_LastEmitterPos.Remove(id);
        }

        ScheduleSpecificJobs();
    }

    void LateUpdate()
    {
        m_JobHandle.Complete();

        if (m_ActiveCount > 0) CheckAndRemoveObjects();
        currentZ = currentZ < -1 ? 0 : currentZ;
    }


    protected abstract void ScheduleSpecificJobs(); // 调度碰撞等 Job

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

        // 如果不是移除最后一个，需要交换数据
        if (index != lastIndex)
        {
            // 【关键修改】在父类交换数据前，先让子类交换它的私有数据
            // 此时 lastIndex 还是准确的
            OnSwapData(index, lastIndex);

            // --- 父类交换通用数据 ---
            m_Positions[index] = m_Positions[lastIndex];
            m_Speeds[index] = m_Speeds[lastIndex];
            m_Angles[index] = m_Angles[lastIndex];
            m_Lifetimes[index] = m_Lifetimes[lastIndex];
            m_MaxLifetimes[index] = m_MaxLifetimes[lastIndex];
            m_LastAngles[index] = m_LastAngles[lastIndex];
            m_IsDead[index] = m_IsDead[lastIndex]; // 注意这里
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

        // 最后才减少计数
        m_ActiveGOs.RemoveAt(lastIndex);
        m_ActiveCount--;
    }

    // 绝对不能依赖 m_ActiveCount 来计算 lastIndex，因为此时 m_ActiveCount 还没减
    // 要使用 lastIndex: 最后一个有效元素的位置
    protected abstract void OnSwapData(int index, int lastIndex);

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
    #endregion
}