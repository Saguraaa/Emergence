using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private InputBootstrap inputBootstrap;
        [SerializeField] private EnvironmentBootstrap environmentBootstrap;
        [SerializeField] private PlayerBootstrap playerBootstrap;
        [SerializeField] private ObstacleBootstrap[] obstacleBootstraps = new ObstacleBootstrap[0];

        // 总入口仅启动一次，各门控不再维护防重复初始化状态。
        private void Start() => Initialize();

        private void Initialize()
        {
            // 校验
            if (inputBootstrap == null) throw new MissingReferenceException($"{name}: {nameof(inputBootstrap)} is required.");
            if (environmentBootstrap == null) throw new MissingReferenceException($"{name}: {nameof(environmentBootstrap)} is required.");
            if (playerBootstrap == null) throw new MissingReferenceException($"{name}: {nameof(playerBootstrap)} is required.");
            if (obstacleBootstraps == null) throw new MissingReferenceException(name + ": obstacleBootstraps is required.");
            for (int i = 0; i < obstacleBootstraps.Length; i++)
                if (obstacleBootstraps[i] == null)
                    throw new MissingReferenceException($"{name}: obstacleBootstraps[{i}] is required.");
            // 初始化
            inputBootstrap.Initialize();
            environmentBootstrap.Initialize();
            foreach (var obstacleBootstrap in obstacleBootstraps) obstacleBootstrap.Initialize();
            playerBootstrap.Initialize();
        }

        private void Shutdown()
        {
            // 先停止玩家访问，再释放环境和输入；错误直接交给 Unity 报告。
            playerBootstrap.Shutdown();
            for (int i = obstacleBootstraps.Length - 1; i >= 0; i--) obstacleBootstraps[i].Shutdown();
            environmentBootstrap.Shutdown();
            inputBootstrap.Shutdown();
        }

        private void OnDestroy() => Shutdown();
    }
}
