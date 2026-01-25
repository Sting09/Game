using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;



public class BulletDOTSManager : SingletonMono<BulletDOTSManager>
{
    // --- 配置 (分离外观与行为) ---
    [Header("Bullet Configs (Gameplay)")]
    public List<BulletBasicConfigSO> bulletConfigs;     //所有子弹配置信息
    [Header("Behavior Configs (Logic)")]
    public List<BulletBehaviorProfileSO> behaviorProfiles;  //所有事件组配置信息

    // --- ID 查找表 ---
    private Dictionary<string, int> m_VisualNameToID = new Dictionary<string, int>();
    private Dictionary<string, int> m_BehaviorNameToID = new Dictionary<string, int>();

    // --- 对象池 ---
    public Queue<GameObject>[] m_VisualPools;       //备用子弹队列。已经Instantiate但还未激活的子弹Object
                                                    //激活的子弹在 private List<GameObject> m_ActiveGOs;
    private Transform[] m_VisualRoots;
    public Transform poolRoot;
    public int maxBulletCapacity = 10000;
    [SerializeField] private int m_ActiveCount = 0;

    // --- 遮挡关系控制 ---
    public float deltaZ = -1e-05f;
    [SerializeField] private float currentZ = 0;




    #region Job系统的内部属性
    // --- Native 数据 ---
    private NativeArray<float3> m_Positions;        //所有子弹的位置
    private NativeArray<float> m_Speeds;        //所有子弹的速度
    private NativeArray<float> m_Angles;        //所有子弹的方向
    private NativeArray<float> m_LastAngles;        //所有子弹上一帧的方向，用于节省计算
    private NativeArray<float> m_Lifetimes;        //所有子弹已经生成了多久

    private NativeArray<float> m_Accelerations;        //所有子弹的加速度
    private NativeArray<float> m_AccelAngles;        //所有子弹的加速度方向
    private NativeArray<float> m_AngularVelocities;        //所有子弹的角速度

    private NativeArray<bool> m_IsDead;        //子弹是否应该回收。用于多线程同步的状态信息
    private NativeArray<int> m_ActiveVisualIDs;        //所有子弹对应的子弹配置信息ID
    private NativeArray<int> m_BulletBehaviorIDs;        //所有子弹对应的事件组ID
    private NativeArray<int> m_NextEventIndex;        //子弹即将执行第几个子弹事件
    private NativeArray<Unity.Mathematics.Random> m_Randoms;        //所有子弹涉及随机操作时，使用的随机种子

    private NativeArray<NativeBulletEvent> m_GlobalEventArray;  //事件列表，所有涉及的事件
    private NativeArray<int2> m_BehaviorRanges; //每个事件组包含的事件，在事件列表m_GlobalEventArray中的起始索引和长度

    private TransformAccessArray m_Transforms;        //所有子弹的Transform引用
    private List<GameObject> m_ActiveGOs;       //对所有活跃的子弹GameObject的引用

    private bool m_IsInitialized = false;        //是否已初始化完成
    private JobHandle m_JobHandle;        //Job句柄

    // --- Pending buffer for deferred AddBullet (Method A) ---
    private struct PendingBullet
    {
        public int visualID;
        public int behaviorID;
        public Vector3 startPos;
        public BulletRuntimeInfo info;
    }
    private List<PendingBullet> m_PendingBullets;

    #endregion





    #region BulletManager在Awake和Destroy时涉及的逻辑
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

    /// <summary>
    /// 根据配置文件，初始化维护每种子弹和子弹事件组所必要的数据结构。在Awake时执行。
    /// </summary>
    private void InitializeConfig()
    {
        //清空从子弹名称/子弹事件名称到id的字典
        m_VisualNameToID.Clear();
        m_BehaviorNameToID.Clear();

        //初始化维护每种子弹类型所需的数据结构（字典、每种子弹类型的根节点、对象池）
        if (bulletConfigs != null)
        {
            //初始化对象池，每种子弹类型，建立一个根节点和一个对象队列
            m_VisualPools = new Queue<GameObject>[bulletConfigs.Count];
            m_VisualRoots = new Transform[bulletConfigs.Count];

            //建立子弹名称到索引的映射
            for (int i = 0; i < bulletConfigs.Count; i++)
            {
                if (bulletConfigs[i] != null)
                {
                    if (!m_VisualNameToID.ContainsKey(bulletConfigs[i].name))
                    {
                        m_VisualNameToID.Add(bulletConfigs[i].name, i);
                    }
                }
            }
        }
        else
        {
            bulletConfigs = new List<BulletBasicConfigSO>();
            m_VisualPools = new Queue<GameObject>[0];
        }

        // 初始化维护每种子弹事件类型所需的数据结构（字典、所有事件列表、事件组的其实索引和长度）
        if (behaviorProfiles != null)
        {
            //所有子弹事件的列表
            List<NativeBulletEvent> tempAllEvents = new List<NativeBulletEvent>();

            //每个事件组包含的事件，在tempAllEvents中的起始索引和长度
            m_BehaviorRanges = new NativeArray<int2>(behaviorProfiles.Count, Allocator.Persistent);

            for (int i = 0; i < behaviorProfiles.Count; i++)
            {
                var profile = behaviorProfiles[i];

                //一个SO是事件组，包含若干事件，所以先存起始索引，再存个数
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

                        //SO中配置的事件，转化为Manager所需的NativeBulletEvent事件
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
    /// 初始化子弹更新运算所需的内存。Awake时执行，直接按最大子弹数目申请内存。
    /// </summary>
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

        //Job系统不能使用System.Random
        m_Randoms = new NativeArray<Unity.Mathematics.Random>(maxBulletCapacity, Allocator.Persistent);
        var seedGen = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
        for (int i = 0; i < maxBulletCapacity; i++)
        {
            m_Randoms[i] = new Unity.Mathematics.Random(seedGen.NextUInt(1, uint.MaxValue));
        }

        m_Transforms = new TransformAccessArray(maxBulletCapacity);
        m_ActiveGOs = new List<GameObject>(maxBulletCapacity);
    }

    /// <summary>
    /// Destroy时，回收内存，避免内存泄漏
    /// </summary>
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

        //注意一下，只有这里需要小写is的i
        if (m_Transforms.isCreated) m_Transforms.Dispose();
    }

    #endregion





    #region 外部发射子弹时调用，向Manager中加入子弹
    /// <summary>
    /// 添加子弹。外部发射子弹时调用
    /// </summary>
    public void AddBullet(int visualID, int behaviorID, Vector3 startPos, BulletRuntimeInfo info)
    {
        // 先检查 pending 列表是否为空
        if (m_PendingBullets == null) m_PendingBullets = new List<PendingBullet>();

        // 如果添加子弹会导致超过最大子弹数目，则跳过添加
        int pendingCount = m_PendingBullets.Count;
        if (m_ActiveCount + pendingCount >= maxBulletCapacity)
        {
            Debug.LogWarning("Bullet capacity reached; dropping bullet.");
            return;
        }

        //将子弹加入 pending 列表，由 Update 统一处理（确保安全且减少频繁 Complete）
        m_PendingBullets.Add(new PendingBullet
        {
            visualID = visualID,
            behaviorID = behaviorID,
            startPos = startPos,
            info = info
        });
    }
    #endregion




    #region 内部每帧Update()更新，更新子弹位置、速度等信息
    void Update()
    {
        // 确保上一帧的 Jobs 完成（安全地写入 NativeArray）
        m_JobHandle.Complete();

        // Flush pending bullets（在主线程批量把 pending 请求写入 NativeArray & TransformAccessArray）
        FlushPendingBullets();

        // 如果没有活动子弹，直接返回（节省调度开销）
        if (m_ActiveCount == 0) return;

        float dt = Time.deltaTime;

        //派发三个Job，在Update和LateUpdate之间执行。这期间，其他Update方法、协程也会一起执行，最大化利用时间
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
        //强制阻塞主线程，直到派发的三个Job完成
        m_JobHandle.Complete();
        //将状态为isDead的子弹移除
        if (m_ActiveCount > 0) CheckAndRemoveBullets();
        //如果currentZ过小（小于-1），则恢复到0
        currentZ = currentZ < -1 ? 0 : currentZ;
    }

    /// <summary>
    /// 将敌人本帧发射的子弹，添加到对象池中，Update时调用
    /// </summary>
    private void FlushPendingBullets()
    {
        if (m_PendingBullets == null || m_PendingBullets.Count == 0) return;

        //计算要添加子弹的数目
        int pendingTotal = m_PendingBullets.Count;
        int available = maxBulletCapacity - m_ActiveCount;
        int toProcess = math.min(pendingTotal, available);

        if (toProcess <= 0)
        {
            // 容量已满，丢弃这些要生成的子弹
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

            //添加子弹到对象池
            GameObject obj = GetBulletFromPool(visualID);
            if (obj == null) continue;

            obj.SetActive(true);
            obj.transform.SetPositionAndRotation(startPos, Quaternion.Euler(0, 0, -info.direction));

            //设置遮挡关系
            int index = m_ActiveCount;

            currentZ += deltaZ;
            float zPriority = (visualID >= 0 && visualID < bulletConfigs.Count) ? bulletConfigs[visualID].zPriority : 0f;

            //子弹数据添加到内存
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

        //最后别忘了清空Pending
        m_PendingBullets.Clear();

        #region 可选：如果有剩余未处理的 pending（因为容量），把未处理部分移到头部保留到下一帧

        /*if (toProcess < pendingTotal)
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
        }*/
        #endregion

    }

    /// <summary>
    /// 检查所有子弹，移除状态为isDead的子弹，LateUpdate时调用
    /// </summary>
    private void CheckAndRemoveBullets()
    {
        for (int i = m_ActiveCount - 1; i >= 0; i--)
        {
            if (m_IsDead[i]) RemoveBulletAt(i);
        }
    }

    /// <summary>
    /// 移除索引为index的子弹
    /// </summary>
    /// <param name="index"></param>
    private void RemoveBulletAt(int index)
    {
        int lastIndex = m_ActiveCount - 1;
        GameObject objToRemove = m_ActiveGOs[index];
        int visualID = m_ActiveVisualIDs[index];


        //从对象池中移除
        objToRemove.SetActive(false);
        if (visualID >= 0 && visualID < m_VisualPools.Length) m_VisualPools[visualID].Enqueue(objToRemove);
        else Destroy(objToRemove);


        //从内存中移除
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
    #endregion




    #region 对象池相关方法
    /// <summary>
    /// 从对象池中获取一个子弹Object。
    /// </summary>
    /// <param name="visualID">子弹类型id</param>
    /// <returns></returns>
    private GameObject GetBulletFromPool(int visualID)
    {
        //检测id合法性
        if (visualID < 0 || visualID >= m_VisualPools.Length) return null;

        //没有这个池子，则先建池子
        if (m_VisualPools[visualID] == null) m_VisualPools[visualID] = new Queue<GameObject>();

        Queue<GameObject> pool = m_VisualPools[visualID];

        //有空闲的，直接返回池子里的
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        //没有空闲的，Instantiate一个再返回
        else
        {
            BulletBasicConfigSO cfg = bulletConfigs[visualID];
            //这种类型的子弹，Tranform放在哪个父物体下面
            Transform parent = GetOrCreateVisualRoot(visualID);
            GameObject newObj = Instantiate(cfg.prefab, parent);
            return newObj;
        }
    }


    /// <summary>
    /// 预加载对象池。由BaseShooter在Start()时调用，读取DanmakuSO文件中声明的所需子弹类型，每种预填充50个
    /// </summary>
    public void PreparePoolsForLevel(List<string> bulletNamesToPrewarm, int countPerType = 50)
    {
        if (bulletNamesToPrewarm == null)
        {
            Debug.LogWarning("requiredBulletNames未填写，请检查DanmakuSO！！");
            return;
        }

        //对于每种声明的子弹，先转换为id
        foreach (var name in bulletNamesToPrewarm)
        {
            int id = GetVisualID(name);
            if (id != -1)
            {
                // 从对象池中尽可能取countPerType个对象
                List<GameObject> temp = new List<GameObject>();
                for (int i = 0; i < countPerType; i++)
                {
                    //此时执行GetBulletFromPool，分两种情况
                    //如果这种类型已经填充过，从Queue获取再还回去，不会额外生成
                    //如果没填充过，执行Instantiate
                    GameObject obj = GetBulletFromPool(id);
                    if (obj != null) temp.Add(obj);
                }

                // 将实际取到的对象加入备用子弹队列。
                //备用子弹：public Queue<GameObject>[] m_VisualPools; 已经Instantiate但还未激活的子弹Object
                foreach (var obj in temp)
                {
                    m_VisualPools[id].Enqueue(obj);
                    obj.SetActive(false);
                }
            }
        }
    }


    /// <summary>
    /// 返回指定类型子弹Transform的父物体。每种子弹的Transform分类管理，同种子弹放在同一个父物体下。
    /// </summary>
    /// <param name="visualID">子弹类型id</param>
    /// <returns></returns>
    private Transform GetOrCreateVisualRoot(int visualID)
    {
        //如果没有父物体，先新建一个
        if (m_VisualRoots[visualID] == null)
        {
            //命名格式形如Pool_YellowRing
            string bulletTypeName = bulletConfigs[visualID].bulletName;
            GameObject subRootObj = new GameObject($"Pool_{bulletTypeName}");

            //加到对象池总父物体下
            subRootObj.transform.SetParent(poolRoot);
            subRootObj.transform.localPosition = Vector3.zero;

            //新建完后加入所有父物体数组中
            m_VisualRoots[visualID] = subRootObj.transform;
        }
        return m_VisualRoots[visualID];
    }
    #endregion




    #region 外部接口和工具函数
    /// <summary>
    /// 返回当前子弹数量
    /// </summary>
    public int ActiveCount => m_ActiveCount;

    /// <summary>
    /// 根据名称获取外观ID 
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
    /// 在运行时重新加载 behaviorProfiles 到 m_GlobalEventArray / m_BehaviorRanges 中（安全）
    /// 可在编辑器 Inspector 中点击或通过脚本调用以热重载行为配置（无需重启游戏）
    /// 不会在游戏运行过程中自动调用
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
    #endregion




}

// --- Jobs ---
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
        //没有通过Burst编译时报错
        CheckBurstStatus();

        //确定执行哪些事件
        int evtIdx = nextEventIndex[index];
        if (evtIdx == -1) return;

        int bID = bulletBehaviorIDs[index];
        if (bID < 0 || bID >= behaviorRanges.Length) return;

        int2 range = behaviorRanges[bID];
        int globalEndIndex = range.x + range.y;

        //逐个执行事件
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
                //速度变化事件：速度增加 / 变为valueA
                case BulletEventType.ChangeSpeed:
                    if (evt.useRelative) speeds[index] += val; else speeds[index] = val; break;

                //方向变化事件：角度增加 / 变为valueA
                case BulletEventType.ChangeDirection:
                    if (evt.useRelative) angles[index] += val; else angles[index] = val; break;

                //设置加速度事件：加速度大小变为valueA；
                //valueB > 0.5，加速度方向与速度方向一致
                //valueB <= 0.5，加速度方向变为valueC指定的方向
                case BulletEventType.SetAcceleration:
                    accelerations[index] = val;
                    if (evt.valueB > 0.5f) accelAngles[index] = angles[index]; else accelAngles[index] = evt.valueC; break;

                //设置角速度事件：角速度变为valueA
                case BulletEventType.SetAngularVelocity:
                    angularVelocities[index] = val; break;

                //停止移动事件：速度、加速度、角速度全部变为0
                case BulletEventType.Stop:
                    speeds[index] = 0; accelerations[index] = 0; angularVelocities[index] = 0; break;

                //强制移除事件：移除子弹
                case BulletEventType.Recycle:
                    isDead[index] = true; break;
            }
            evtIdx++;
        }
        nextEventIndex[index] = (evtIdx >= globalEndIndex) ? -1 : evtIdx;
    }

    [BurstDiscard]
    private void CheckBurstStatus()
    {
        // 这里的 index == 0 是为了防止成千上万个子弹同时报错刷屏，只报一次即可
        // 也可以使用静态变量控制只报错一次
        Debug.LogWarning($"[性能警告] BulletMoveJob 正在以 Mono (慢速) 模式运行！Burst 未生效！");
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
        //没有通过Burst编译时报错
        CheckBurstStatus();

        //修改生命周期
        lifetimes[index] += dt;


        float currentSpeed = speeds[index];
        float currentAngle = angles[index];
        float accel = accelerations[index];
        float angVel = angularVelocities[index];

        //应用角速度
        if (math.abs(angVel) > 0.001f) currentAngle += angVel * dt;

        //应用加速度
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

        //写入新的速度和角度
        speeds[index] = currentSpeed;
        angles[index] = currentAngle;

        //计算新的位置
        float3 move = new float3(velVec.x, velVec.y, 0) * dt;
        float3 newPos = positions[index] + move;
        positions[index] = newPos;
        transform.position = new Vector3(newPos.x, newPos.y, newPos.z);

        //判定要不要修改lastAngles
        if (math.abs(currentAngle - lastAngles[index]) > 0.001f)
        {
            quaternion q = quaternion.RotateZ(math.radians(-currentAngle));
            transform.rotation = new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
            lastAngles[index] = currentAngle;
        }
    }

    [BurstDiscard]
    private void CheckBurstStatus()
    {
        // 这里的 index == 0 是为了防止成千上万个子弹同时报错刷屏，只报一次即可
        // 也可以使用静态变量控制只报错一次
        Debug.LogWarning($"[性能警告] BulletMoveJob 正在以 Mono (慢速) 模式运行！Burst 未生效！");
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
    public NativeArray<bool> isDeadResults;

    public void Execute(int index)
    {
        //没有通过Burst编译时报错
        CheckBurstStatus();

        //已经确定移除了（例如碰撞到玩家了），就不用检查了，直接返回
        if (isDeadResults[index])
        {
            return;
        }

        //超过最大生命周期，移除
        bool dead = lifetimes[index] > maxLifetime;
        if (!dead)
        {
            //超过屏幕边界，移除
            float3 pos = positions[index];
            dead = pos.x < -boundsX || pos.x > boundsX || pos.y < -boundsY || pos.y > boundsY;
        }
        isDeadResults[index] = dead;
    }

    [BurstDiscard]
    private void CheckBurstStatus()
    {
        // 这里的 index == 0 是为了防止成千上万个子弹同时报错刷屏，只报一次即可
        // 也可以使用静态变量控制只报错一次
        Debug.LogWarning($"[性能警告] BulletMoveJob 正在以 Mono (慢速) 模式运行！Burst 未生效！");
    }
}
