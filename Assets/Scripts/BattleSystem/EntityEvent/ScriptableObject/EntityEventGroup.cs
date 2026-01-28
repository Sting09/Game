using System.Collections.Generic;
using UnityEngine;

// 这是一个纯逻辑的配置，不包含任何Prefab引用
[CreateAssetMenu(fileName = "NewBehaviorProfile", menuName = "Battle System/Entity Event Group")]
public class EntityEventGroup : ScriptableObject
{
    public string profileName;
    // 所有的事件列表
    public List<EntityEventData> eventList;
}

[System.Serializable]
public struct EntityEventData
{
    public float time;
    public EntityEventType type;
    public float valA;
    public float valB;
    public float valC;
    public bool useRelative;
    public bool useRandom;
}