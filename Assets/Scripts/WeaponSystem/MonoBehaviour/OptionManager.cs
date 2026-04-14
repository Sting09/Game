using UnityEngine;

namespace WeaponSystem
{
    public class OptionManager : MonoBehaviour
    {
        public OptionController OptionPrefab;
        public int MaxOptions = 32;
        public Transform optionParent;

        [Header("运动模式配置")]
        public OptionMovementStrategy HighSpeedStrategy; // 高速时的策略
        public OptionMovementStrategy LowSpeedStrategy;  // 低速时的策略

        [Tooltip("切换阵型时的平滑时间，值越大越平滑(迟缓)")]
        public float MovementSmoothTime = 0.025f;

        private OptionController[] _optionsPool;
        private int _activeOptionCount = 4;
        private bool _isPlayerHighSpeed;

        private void Start()
        {
            // 初始化对象池（代码与之前一致）
            _optionsPool = new OptionController[MaxOptions];
            for (int i = 0; i < MaxOptions; i++)
            {
                var optionInstance = Instantiate(OptionPrefab, transform.position, Quaternion.identity, optionParent);
                optionInstance.Initialize(i);
                optionInstance.gameObject.SetActive(false);
                _optionsPool[i] = optionInstance;
            }
            UpdateActiveOptions();
        }

        public void SetOptionCount(int count)
        {
            _activeOptionCount = Mathf.Clamp(count, 0, MaxOptions);
            UpdateActiveOptions();
        }

        public void SetHighSpeedState(bool isHighSpeed)
        {
            _isPlayerHighSpeed = isHighSpeed;
        }

        private void UpdateActiveOptions()
        {
            for (int i = 0; i < MaxOptions; i++)
            {
                _optionsPool[i].gameObject.SetActive(i < _activeOptionCount);
            }
        }

        public void AllOptionsFire(ShootContext context)
        {
            for (int i = 0; i < _activeOptionCount; i++)
            {
                _optionsPool[i].Fire(context, _isPlayerHighSpeed);
            }
        }


        private void Update()
        {
            SetHighSpeedState(!Input.GetKey(KeyCode.LeftShift));

            // 1. 确定当前应该使用哪个策略进行计算
            OptionMovementStrategy activeStrategy = _isPlayerHighSpeed ? HighSpeedStrategy : LowSpeedStrategy;

            // 防呆设计，如果没有配置策略则直接返回
            if (activeStrategy == null) return;

            // 2. 遍历所有激活的子机
            for (int i = 0; i < _activeOptionCount; i++)
            {
                OptionController option = _optionsPool[i];

                // 重点 1：向当前策略询问，这台子机“现在应该在哪”
                Vector3 targetPosition = activeStrategy.GetTargetPosition(option, transform.position, _activeOptionCount);

                // 重点 2：由 Manager 统一驱动 SmoothDamp，牵引子机过去
                // 这样无论 targetPosition 怎么突变，子机的物理运动轨迹永远是平滑且连续的
                option.transform.position = Vector3.SmoothDamp(
                    option.transform.position,
                    targetPosition,
                    ref option.CurrentVelocity, // 注意：CurrentVelocity 依然保存在 OptionController 实例身上
                    MovementSmoothTime
                );
            }

            // 可以在此处将 _optionsPool 的坐标传递给你的 DOTSManager
        }
    }
}