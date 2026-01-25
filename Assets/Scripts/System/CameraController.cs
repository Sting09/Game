using UnityEngine;

public class CameraController : SingletonMono<CameraController>
{
    [Header("Control Settings")]
    [Tooltip("是否允许玩家控制摄像机")]
    public bool canControl = true;

    [Header("Zoom Settings")]
    public float minZoom = 5f;   // 最小视野（放大）
    public float maxZoom = 20f;  // 最大视野（缩小）
    public float zoomSpeed = 2f; // 滚轮灵敏度

    [Header("Pan Settings")]
    public MouseButton dragButton = MouseButton.Right; // 定义用哪个键拖拽（通常是右键或中键）

    public enum MouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    // 内部变量
    private Camera cam;
    private Vector3 dragOrigin;

    // 原始状态记录
    private Vector3 initialPosition;
    private float initialZoom;

    protected override void Awake()
    {
        base.Awake();
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        // 记录初始状态，用于重置功能
        initialPosition = transform.position;
        initialZoom = cam.orthographicSize;
    }

    private void LateUpdate()
    {
        // 4. 检查控制变量
        if (!canControl) return;

        HandleZoom();
        HandlePan();
    }

    // 1. 鼠标滚轮缩放
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            float targetZoom = cam.orthographicSize - scroll * zoomSpeed;
            // 限制缩放范围
            cam.orthographicSize = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }

    // 2. 鼠标拖拽移动 (抓取式)
    private void HandlePan()
    {
        // 按下瞬间：记录鼠标在世界空间的位置
        if (Input.GetMouseButtonDown((int)dragButton))
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
        }

        // 按住期间：计算偏移量并移动摄像机
        if (Input.GetMouseButton((int)dragButton))
        {
            Vector3 currentPos = cam.ScreenToWorldPoint(Input.mousePosition);

            // 计算鼠标移动了多少世界距离
            // 注意：我们要移动摄像机，所以方向是反的，或者是计算差值
            Vector3 difference = dragOrigin - currentPos;

            // 保持Z轴不变（2D游戏摄像机通常在Z=-10）
            transform.position += new Vector3(difference.x, difference.y, 0);
        }
    }

    // 3. 恢复原始设置的函数
    public void ResetCamera()
    {
        transform.position = initialPosition;
        cam.orthographicSize = initialZoom;
    }

    // 4. 外部控制开关的封装方法（可选）
    public void SetControlState(bool state)
    {
        canControl = state;
    }
}