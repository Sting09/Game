using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System;


#if UNITY_EDITOR
using UnityEditor;
#endif

//子类执行顺序： Enemy > Bullet > PlayerShooting
/*
 * 主线程：…… -> EnemyManager
 * 
 */

public abstract class BaseObjManager<T> : SingletonMono<T> where T : BaseObjManager<T>
{
    // --- 玩家信息 ---
    protected float playerHitboxRadius;
    protected float playerHitboxRate = 1f;
    protected SpriteRenderer playerSpriteRenderer;
    protected Coroutine hitPlayerCoroutine;         //obj击中玩家时启动的协程

    // --- 调试信息 ---
    [Header("Debug Settings")]
    [Tooltip("是否使用Gizmo画出判定大小")]
    public bool showDebugGizmos = true;
    [Tooltip("Gizmo颜色")]
    public Color debugGizmoColor = Color.green;

    // --- Obj事件列表 ---
    [Header("Behavior Configs (Logic)")]
    public List<EntityEventGroup> behaviorProfiles;

    // --- Obj事件列表ID查找表 ---
    protected Dictionary<string, int> m_BehaviorNameToID = new Dictionary<string, int>();

    // --- 对象池相关属性 ---
    [NonSerialized] public Queue<GameObject>[] m_VisualPools;
    [NonSerialized] public Transform[] m_VisualRoots;
    public Transform poolRoot;
    public int maxEntityCapacity = 10000;
    [SerializeField] protected int m_ActiveCount = 0;

    // --- 遮挡层级信息 ---
    public float deltaZ = -1e-05f;
    [SerializeField] protected float currentZ = 0;

    // --- 缓存的其他信息 ---
    protected float boundsX;        //活动范围为(-boundsX, boundsX)
    protected float boundsY;
    protected float dt;
    protected float3 playerPos;

    #region Job系统

    //====================================================================================================
    // 添加新变量时，一定要修改：
    // 基类——下方变量、InitializeBaseMemory()、DisposeMemory()、BaseRemoveObjectAt()
    // 子类——变量、FlushPending()、OnSwapData()、OnDispose()、OnInitialize()
    // 确保不重不漏
    //====================================================================================================

    // 基本运动信息
    public NativeArray<float3> m_Positions;
    protected NativeArray<float> m_Speeds;
    public NativeArray<float> m_Angles;
    protected NativeArray<float> m_LastAngles;

    // 发射信息，用来控制子弹事件参数
    protected NativeArray<int> m_ShootPointIndices;

    // 生命周期信息
    protected NativeArray<float> m_Lifetimes;
    protected NativeArray<float> m_MaxLifetimes;
    protected NativeArray<bool> m_IsDead;

    // 高级运动信息
    protected NativeArray<float> m_Accelerations;
    protected NativeArray<float> m_AccelAngles;
    protected NativeArray<float> m_AngularVelocities;

    // 随机数生成器
    protected NativeArray<Unity.Mathematics.Random> m_Randoms;

    // 配置文件索引
    protected NativeArray<int> m_ActiveVisualIDs;

    // Obj事件
    protected NativeArray<int> m_EntityBehaviorIDs;
    protected NativeArray<int> m_NextEventIndex;
    protected NativeArray<NativeEntityEvent> m_GlobalEventArray;
    protected NativeArray<int2> m_BehaviorRanges;

    // 碰撞信息
    public NativeArray<int> m_CollisionTypes;
    public NativeArray<float> m_CircleRadii;
    public NativeArray<float2> m_BoxSizes;

    // 每一帧中，哪些Obj发生了碰撞，要处理碰撞事件
    protected NativeQueue<int> m_CollisionQueue;

    // 相对某物体移动时的辅助变量
    protected NativeArray<bool> m_IsRelative;
    protected NativeArray<int> m_EmitterIDs;
    protected Dictionary<int, Transform> m_ActiveEmitters = new Dictionary<int, Transform>();
    protected Dictionary<int, float3> m_LastEmitterPos = new Dictionary<int, float3>();
    protected List<int> m_EmittersToRemoveCache = new List<int>(128);
    protected List<int> m_DeadEmittersThisFrame = new List<int>(128);
    protected NativeHashMap<int, float3> m_EmitterDeltas;

    // Obj的Transoform和GameObject
    protected TransformAccessArray m_Transforms;
    protected List<GameObject> m_ActiveGOs;

    // 是否初始化完成标志位
    protected bool m_IsInitialized = false;

    //Job句柄
    protected JobHandle m_JobHandle;
    #endregion

    #region Awake初始化 / Start / Destroy销毁
    protected override void Awake()
    {
        // 1. 单例模式基类Awake
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        // 2. 初始化通用数据结构和内存
        InitializeBaseConfig();
        InitializeBaseMemory();

        // 3. 子类各自的初始化方法
        OnInitialize();

        // 4. 获取基本信息
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

        // 5. 初始化完成
        m_IsInitialized = true;
    }

    private void Start()
    {
        //获取playerSpriteRenderer
        if (BattleManager.Instance != null && BattleManager.Instance.player != null)
        {
            playerSpriteRenderer = BattleManager.Instance.player.GetComponent<SpriteRenderer>();
        }
    }

    void OnDestroy()
    {
        // 销毁时，确保Job已经完成
        m_JobHandle.Complete();

        // 归还内存
        DisposeMemory();

        // 子类自己的Dispose方法
        OnDispose();
    }

    /// <summary>
    /// Obj管理器的通用方法，初始化必要的数据结构
    /// </summary>
    private void InitializeBaseConfig()
    {

        // 1. 初始化obj事件
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
                    if (!m_BehaviorNameToID.ContainsKey(profile.profileName))
                        m_BehaviorNameToID.Add(profile.profileName, i);

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

    /// <summary>
    /// 初始化分配的内存
    /// </summary>
    private void InitializeBaseMemory()
    {
        m_Positions = new NativeArray<float3>(maxEntityCapacity, Allocator.Persistent);
        m_Speeds = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_Angles = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_Lifetimes = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_MaxLifetimes = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_LastAngles = new NativeArray<float>(maxEntityCapacity, Allocator.Persistent);
        m_IsDead = new NativeArray<bool>(maxEntityCapacity, Allocator.Persistent);

        m_ShootPointIndices = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent);

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

        m_IsRelative = new NativeArray<bool>(maxEntityCapacity, Allocator.Persistent);
        m_EmitterIDs = new NativeArray<int>(maxEntityCapacity, Allocator.Persistent);
        m_EmitterDeltas = new NativeHashMap<int, float3>(128, Allocator.Persistent);

        m_Randoms = new NativeArray<Unity.Mathematics.Random>(maxEntityCapacity, Allocator.Persistent);
        var seedGen = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
        for (int i = 0; i < maxEntityCapacity; i++)
        {
            m_Randoms[i] = new Unity.Mathematics.Random(seedGen.NextUInt(1, uint.MaxValue));
        }

        m_Transforms = new TransformAccessArray(maxEntityCapacity);
        m_ActiveGOs = new List<GameObject>(maxEntityCapacity);
    }


    /// <summary>
    /// 销毁分配的内存
    /// </summary>
    private void DisposeMemory()
    {
        if (!m_IsInitialized) return;

        if (m_Positions.IsCreated) m_Positions.Dispose();
        if (m_Speeds.IsCreated) m_Speeds.Dispose();
        if (m_Angles.IsCreated) m_Angles.Dispose();
        if (m_Lifetimes.IsCreated) m_Lifetimes.Dispose();
        if (m_MaxLifetimes.IsCreated) m_MaxLifetimes.Dispose();
        if (m_LastAngles.IsCreated) m_LastAngles.Dispose();
        if (m_IsDead.IsCreated) m_IsDead.Dispose();
        if (m_ShootPointIndices.IsCreated) m_ShootPointIndices.Dispose();
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

        if (m_IsRelative.IsCreated) m_IsRelative.Dispose();
        if (m_EmitterIDs.IsCreated) m_EmitterIDs.Dispose();
        if (m_EmitterDeltas.IsCreated) m_EmitterDeltas.Dispose();

        // 注意一下，只有这里的is是小写
        if (m_Transforms.isCreated) m_Transforms.Dispose();
    }


    // --- 抽象方法 ---

    /// <summary>
    /// 初始化方法
    /// </summary>
    protected abstract void OnInitialize();

    /// <summary>
    /// 销毁方法
    /// </summary>
    protected abstract void OnDispose();
    #endregion

    #region Update：处理碰撞-新增-派发Job（事件-移动-检测碰撞-检测移除）-处理移除
    protected virtual void Update()
    {
        // 1. 确保上一帧的Job已经完成
        m_JobHandle.Complete();

        // 2. 重置用于缓存的数据结构
        m_EmitterDeltas.Clear();

        // 3. 处理发生碰撞事件的Obj
        HandleCollisions();

        // 4. 添加本帧新生成的Obj
        FlushPending();

        if (m_ActiveCount == 0) return;

        // 5. 刷新dt、玩家位置等缓存
        dt = Time.deltaTime;
        playerPos = float3.zero;
        if (BattleManager.Instance != null)
        {
            playerPos = BattleManager.Instance.GetPlayerPos();
        }

        // ---- 相对父物体移动的逻辑  ----
        // 6. 更新相对父物体的位置、移除父物体已死亡的子物体
        m_EmittersToRemoveCache.Clear();
        m_DeadEmittersThisFrame.Clear();

        foreach (var kvp in m_ActiveEmitters)
        {
            int id = kvp.Key;
            Transform t = kvp.Value;
            if (t == null || !t.gameObject.activeInHierarchy)
            {
                m_EmittersToRemoveCache.Add(id);
                m_DeadEmittersThisFrame.Add(id);
                continue;
            }

            float3 currentPos = t.position;
            float3 lastPos = currentPos;

            if (m_LastEmitterPos.TryGetValue(id, out float3 prev))
            {
                lastPos = prev;
            }

            float3 delta = currentPos - lastPos;
            m_EmitterDeltas.TryAdd(id, delta);
            m_LastEmitterPos[id] = currentPos;
        }

        foreach (var id in m_EmittersToRemoveCache)
        {
            m_ActiveEmitters.Remove(id);
            m_LastEmitterPos.Remove(id);
        }
        if (m_DeadEmittersThisFrame.Count > 0)
        {
            // 创建临时Job，遍历所有obj，如果父物体已销毁，本obj也销毁
            OnEmittersDeadThisFrame(m_DeadEmittersThisFrame);
        }

        // 7. 派发本帧要执行的Job
        ScheduleSpecificJobs();
    }

    void LateUpdate()
    {
        // 等待本帧的Job执行完
        m_JobHandle.Complete();

        // 遍历所有obj，移除已经失活的obj
        if (m_ActiveCount > 0) CheckAndRemoveObjects();

        //必要时重置z轴坐标
        currentZ = currentZ < -1 ? 0 : currentZ;
    }

    /// <summary>
    /// 派发要执行的Job
    /// </summary>
    protected abstract void ScheduleSpecificJobs();

    /// <summary>
    /// 将本帧新增的obj加入内存
    /// </summary>
    protected abstract void FlushPending();

    /// <summary>
    /// 处理发生碰撞事件的obj
    /// </summary>
    protected abstract void HandleCollisions();

    /// <summary>
    /// 遍历所有obj，移除已经失活的obj
    /// </summary>
    private void CheckAndRemoveObjects()
    {
        for (int i = m_ActiveCount - 1; i >= 0; i--)
        {
            if (m_IsDead[i]) {
                BaseRemoveObjectAt(i);
            }
        }
    }

    /// <summary>
    /// 具体的移除单个obj的方法
    /// </summary>
    /// <param name="index"></param>
    protected void BaseRemoveObjectAt(int index)
    {
        int lastIndex = m_ActiveCount - 1;
        GameObject objToRemove = m_ActiveGOs[index];
        int visualID = m_ActiveVisualIDs[index];

        objToRemove.SetActive(false);
        if (visualID >= 0 && visualID < m_VisualPools.Length) m_VisualPools[visualID].Enqueue(objToRemove);
        else Destroy(objToRemove);

        // swapData 更新内存
        if (index != lastIndex)
        {
            // 处理子类特有的属性
            OnSwapData(index, lastIndex);

            m_Positions[index] = m_Positions[lastIndex];
            m_Speeds[index] = m_Speeds[lastIndex];
            m_Angles[index] = m_Angles[lastIndex];
            m_Lifetimes[index] = m_Lifetimes[lastIndex];
            m_MaxLifetimes[index] = m_MaxLifetimes[lastIndex];
            m_LastAngles[index] = m_LastAngles[lastIndex];
            m_IsDead[index] = m_IsDead[lastIndex];
            m_ShootPointIndices[index] = m_ShootPointIndices[lastIndex];
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

        m_ActiveGOs.RemoveAt(lastIndex);
        m_ActiveCount--;
    }

    /// <summary>
    /// swapData步骤中，处理子类特有的变量
    /// </summary>
    /// <param name="index">要移除的id</param>
    /// <param name="lastIndex">最后一个物体的id</param>
    protected abstract void OnSwapData(int index, int lastIndex);

    /// <summary>
    /// 创建临时Job，遍历所有obj，如果父物体已销毁，本obj也销毁
    /// </summary>
    /// <param name="deadEmitterIds"></param>
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

    #region 控制Job调度的方法

    /// <summary>
    /// 【延迟结算核心】 允许外部注入依赖。
    /// 一个类使用此方法后，下一帧在 Update 开头调用 m_JobHandle.Complete() 时，要先等待dependency完成
    /// </summary>
    public void RegisterExternalDependency(JobHandle dependency)
    {
        // 将外部的 handle 合并到自己的 handle 中
        m_JobHandle = JobHandle.CombineDependencies(m_JobHandle, dependency);
    }



    /// <summary>
    /// 强制主线程等待到所有Job完成
    /// </summary>
    public void CompleteAllJobs()
    {
        m_JobHandle.Complete();
    }

    #endregion

    #region 对象池方法 和 辅助方法

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