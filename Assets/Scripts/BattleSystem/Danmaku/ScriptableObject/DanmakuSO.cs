using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DanmakuSO", menuName = "Battle System/Danmaku/DanmakuSO")]
public class DanmakuSO : ScriptableObject
{
    [Tooltip("持续多少秒。<0表示始终发射")]
    public float duration = -1f;
    [Tooltip("发射器参数列表")]
    public List<AbstractEmitterConfigSO> emitterList;
    [Tooltip("发射器间需要共享的参数")]
    public List<float> emitterParameterList;
    [Tooltip("发射器参数列表")]
    public List<RequiredEntityInfo> requiredEntities;
    //未来可能需要子弹数量、敌人名称、敌人数量
}
