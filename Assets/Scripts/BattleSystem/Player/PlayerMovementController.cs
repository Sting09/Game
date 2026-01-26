using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;



/*
 * 降低输入延迟，外部进行的操作：
 * 
 * 1 
 * 修改 Edit -> Project Settings -> Script Execution Order，
 * 当前设置：InputSystem -100；PlayerMovementController -99；BulletDOTSManager -98
 * 
 * 2
 * Update Mode: 确认为 Process Events In Dynamic Update。
 * Edit -> Project Settings -> Input System Package -> Update Mode -> Process Events In Dynamic Update
 * 
 * 3
 * Background Behavior: 勾选 Run In Background
 * Edit -> Project Settings -> Player -> Resolution and Presentation -> 勾选 Run In Background
 * 回到左侧的 Input System Package，找到 Background Behavior 选项，将其设置为 Ignore Focus
 */



public class PlayerMovementController : MonoBehaviour
{
    // --- 参数设置 ---
    [Header("移动参数")]
    private float moveSpeed;
    private float focusSpeedMultiplier;
    private float boundsX;
    private float boundsY;

    // --- 内部变量 ---
    private Vector2 moveInput;
    [SerializeField] private bool isFocusing; // 是否处于精确移动模式

    // --- 延迟测试 (保留原有功能) ---
    [Header("调试")]
    public bool enableLatencyTest = true;
    private SpriteRenderer sr;
    private Color originalColor;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr) originalColor = sr.color;

        // 初始化参数 (这里假设你已经有GlobalSetting，如果没有请填默认值)
        if (GlobalSetting.Instance != null && GlobalSetting.Instance.globalVariable != null)
        {
            moveSpeed = GlobalSetting.Instance.globalVariable.playerDefaultSpeed;
            focusSpeedMultiplier = GlobalSetting.Instance.globalVariable.slowModeRate;
            boundsX = GlobalSetting.Instance.globalVariable.playerMoveRangeHalfWidth;
            boundsY = GlobalSetting.Instance.globalVariable.playerMoveRangeHalfHeight;
        }
        else
        {
            // 默认兜底参数
            moveSpeed = 7f;
            focusSpeedMultiplier = 0.45f;
            boundsX = 5.5f;
            boundsY = 5.5f;
        }
    }

    // 为了降低输入延迟，移动逻辑不能在 FixedUpdate ，而要在 Update
    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        // 1. 计算速度
        float currentSpeed = isFocusing ? moveSpeed * focusSpeedMultiplier : moveSpeed;

        // 2. 计算位移量 (注意这里用 Time.deltaTime 而不是 fixedDeltaTime)
        Vector2 displacement = moveInput * currentSpeed * Time.deltaTime;

        // 3. 计算新位置
        Vector3 newPosition = transform.position + (Vector3)displacement;

        // 4. 手动边界限制 (Clamp)
        // 既然不依赖刚体物理墙，我们需要自己限制坐标，防止飞出屏幕
        newPosition.x = Mathf.Clamp(newPosition.x, -boundsX, boundsX);
        newPosition.y = Mathf.Clamp(newPosition.y, -boundsY, boundsY);

        // 5. 应用位置
        transform.position = newPosition;
    }

    #region Input System 回调 (保持不变)
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
        if (moveInput.sqrMagnitude > 1) moveInput.Normalize();
    }

    public void OnFocus(InputValue value)
    {
        bool isPressed = value.isPressed;
        if (isFocusing != isPressed)
        {
            isFocusing = isPressed;

            //如果开启了测试代码，低速移动时玩家变红
            if (enableLatencyTest && sr != null)
            {
                float inputTime = Time.realtimeSinceStartup;
                sr.color = isPressed ? Color.red : originalColor;
                //打印输入延迟
                StartCoroutine(CalculateFrameLatency(inputTime, isPressed ? "按下" : "松开"));
            }
        }
    }

    /// <summary>
    /// 打印输入延迟
    /// </summary>
    /// <param name="inputTime"></param>
    /// <param name="actionName"></param>
    /// <returns></returns>
    private IEnumerator CalculateFrameLatency(float inputTime, string actionName)
    {
        yield return new WaitForEndOfFrame();
        float renderTime = Time.realtimeSinceStartup;
        float latencyMs = (renderTime - inputTime) * 1000f;
        Debug.Log($"<color=cyan>[延迟测试]</color> {actionName} | 逻辑帧: {Time.frameCount} | 耗时: {latencyMs:F2}ms");
    }
    #endregion
}