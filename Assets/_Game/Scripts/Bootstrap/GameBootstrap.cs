using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private InputBootstrap inputBootstrap;
        [SerializeField] private EnvironmentBootstrap environmentBootstrap;
        [SerializeField] private PlayerBootstrap playerBootstrap;

        // 总入口仅启动一次，各门控不再维护防重复初始化状态。
        private void Start() => Initialize();

        private void Initialize()
        {
            if (inputBootstrap == null) throw new MissingReferenceException($"{name}: {nameof(inputBootstrap)} is required.");
            if (environmentBootstrap == null) throw new MissingReferenceException($"{name}: {nameof(environmentBootstrap)} is required.");
            if (playerBootstrap == null) throw new MissingReferenceException($"{name}: {nameof(playerBootstrap)} is required.");
            inputBootstrap.Initialize();
            environmentBootstrap.Initialize();
            playerBootstrap.Initialize();
        }

        private void Shutdown()
        {
            // 先停止玩家访问，再释放环境和输入；错误直接交给 Unity 报告。
            playerBootstrap.Shutdown();
            environmentBootstrap.Shutdown();
            inputBootstrap.Shutdown();
        }

        private void OnDestroy() => Shutdown();
    }
}
