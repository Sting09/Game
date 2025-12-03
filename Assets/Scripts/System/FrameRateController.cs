using UnityEngine;

public class FrameRateController : MonoBehaviour
{
    // 目标帧率 (STG标准通常是60)
    public int targetFrameRate = 60;

    void Awake()
    {
        // 1. 锁死渲染帧率，防止在高配机上跑太快导致游戏加速
        Application.targetFrameRate = targetFrameRate;

        // 2. 启用“STG慢放模式”
        // 只要这行代码生效，你整个项目里所有的 Time.deltaTime 都会变成这个固定值！
        // 无论你的 Update 里的代码怎么写，它们都会认为“这一帧只过了 0.016秒”
        Time.captureDeltaTime = 1f / targetFrameRate;

        // 3. 必须把 VSync 关掉，否则 targetFrameRate 可能失效
        QualitySettings.vSyncCount = 0;
    }

    // 如果你需要在某些时刻（比如暂停菜单、过场动画）恢复正常时间
    public void DisableSlowdownMode()
    {
        Time.captureDeltaTime = 0f; // 设为0表示恢复 Unity 默认的动态时间计算
    }
}