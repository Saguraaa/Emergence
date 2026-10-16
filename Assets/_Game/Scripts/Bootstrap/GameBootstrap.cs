using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private InputBootstrap inputBootstrap;
        [SerializeField] private EnvironmentBootstrap environmentBootstrap;
        [SerializeField] private PlayerBootstrap playerBootstrap;
        [SerializeField] private CameraBootstrap[] cameraBootstraps = new CameraBootstrap[0];
        [SerializeField] private ObstacleBootstrap[] obstacleBootstraps = new ObstacleBootstrap[0];
        [SerializeField] private InteractionBootstrap[] interactionBootstraps = new InteractionBootstrap[0];
        [SerializeField] private RespawnBootstrap[] respawnBootstraps = new RespawnBootstrap[0];
        [SerializeField] private NPCBootstrap[] npcBootstraps = new NPCBootstrap[0];

        // 总入口仅启动一次，各门控不再维护防重复初始化状态。
        private void Start() => Initialize();

        private void Initialize()
        {
            // 校验
            if (inputBootstrap == null) throw new MissingReferenceException($"{name}: {nameof(inputBootstrap)} is required.");
            if (environmentBootstrap == null) throw new MissingReferenceException($"{name}: {nameof(environmentBootstrap)} is required.");
            if (playerBootstrap == null) throw new MissingReferenceException($"{name}: {nameof(playerBootstrap)} is required.");
            if (cameraBootstraps == null) throw new MissingReferenceException(name + ": cameraBootstraps is required.");
            for (int i = 0; i < cameraBootstraps.Length; i++)
                if (cameraBootstraps[i] == null)
                    throw new MissingReferenceException($"{name}: cameraBootstraps[{i}] is required.");
            if (obstacleBootstraps == null) throw new MissingReferenceException(name + ": obstacleBootstraps is required.");
            for (int i = 0; i < obstacleBootstraps.Length; i++)
                if (obstacleBootstraps[i] == null)
                    throw new MissingReferenceException($"{name}: obstacleBootstraps[{i}] is required.");
            if (interactionBootstraps == null) throw new MissingReferenceException(name + ": interactionBootstraps is required.");
            for (int i = 0; i < interactionBootstraps.Length; i++)
                if (interactionBootstraps[i] == null)
                    throw new MissingReferenceException($"{name}: interactionBootstraps[{i}] is required.");
            if (respawnBootstraps == null) throw new MissingReferenceException(name + ": respawnBootstraps is required.");
            for (int i = 0; i < respawnBootstraps.Length; i++)
                if (respawnBootstraps[i] == null)
                    throw new MissingReferenceException($"{name}: respawnBootstraps[{i}] is required.");
            if (npcBootstraps == null) throw new MissingReferenceException(name + ": npcBootstraps is required.");
            for (int i = 0; i < npcBootstraps.Length; i++)
                if (npcBootstraps[i] == null)
                    throw new MissingReferenceException($"{name}: npcBootstraps[{i}] is required.");

            // 初始化
            var inputSettings = inputBootstrap.Initialize();
            environmentBootstrap.Initialize();
            foreach (var obstacleBootstrap in obstacleBootstraps) obstacleBootstrap.Initialize();
            foreach (var interactionBootstrap in interactionBootstraps) interactionBootstrap.Initialize();
            foreach (var respawnBootstrap in respawnBootstraps) respawnBootstrap.Initialize();
            playerBootstrap.Initialize(inputSettings);
            foreach (var npcBootstrap in npcBootstraps) npcBootstrap.Initialize();
            foreach (var cameraBootstrap in cameraBootstraps) cameraBootstrap.Initialize();
        }

        private void Shutdown()
        {
            // 先停止玩家访问，再释放环境和输入；错误直接交给 Unity 报告。
            for (int i = cameraBootstraps.Length - 1; i >= 0; i--) cameraBootstraps[i].Shutdown();
            for (int i = npcBootstraps.Length - 1; i >= 0; i--) npcBootstraps[i].Shutdown();
            playerBootstrap.Shutdown();
            for (int i = respawnBootstraps.Length - 1; i >= 0; i--) respawnBootstraps[i].Shutdown();
            for (int i = interactionBootstraps.Length - 1; i >= 0; i--) interactionBootstraps[i].Shutdown();
            for (int i = obstacleBootstraps.Length - 1; i >= 0; i--) obstacleBootstraps[i].Shutdown();
            environmentBootstrap.Shutdown();
            inputBootstrap.Shutdown();
        }

        private void OnDisable() => Shutdown();
    }
}
