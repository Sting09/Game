using UnityEngine;

[CreateAssetMenu(fileName = "RewardSO", menuName = "Game Data/RewardSO")]
public class RewardSO : ScriptableObject
{
    public string rewardName;
    public string rewardNum;
    public int powerValue;

    public void Test()
    {
        Debug.Log($"ƒ„ªÒµ√¡À£∫{rewardName} °¡ {rewardNum}");
    }
}
