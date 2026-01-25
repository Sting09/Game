using UnityEngine;

public class GlobalSetting : SingletonMono<GlobalSetting>
{
    public ConfigSO userConfig;
    public GameDataConfigSO gameDataConfig;
    public GlobalVariableSO globalVariable;
}
