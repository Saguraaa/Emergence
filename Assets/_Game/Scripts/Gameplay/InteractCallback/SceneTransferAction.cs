using System;
using Emergence.Bootstrap;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Emergence.Gameplay.InteractCallback
{
    // 交互条件由 InteractionSystem 判断；这里只负责 Unity 场景加载。
    public sealed class SceneTransferAction : MonoBehaviour
    {
        [SerializeField] private GameBootstrap gameBootstrap;
#if UNITY_EDITOR
        [SerializeField] private SceneAsset destinationScene;
#endif
        [SerializeField, HideInInspector] private string destinationScenePath;
        [SerializeField] private string destinationPointId;

        private void Awake()
        {
            if (gameBootstrap == null)
                throw new MissingReferenceException(name + ": gameBootstrap is required.");
            if (gameBootstrap.gameObject.scene != gameObject.scene)
                throw new InvalidOperationException(name + ": gameBootstrap must belong to the same scene.");
            if (string.IsNullOrWhiteSpace(destinationScenePath))
                throw new MissingReferenceException(name + ": destinationScene is required.");
            if (string.IsNullOrWhiteSpace(destinationPointId))
                throw new MissingReferenceException(name + ": destinationPointId is required.");
            _ = new FixedString64Bytes(destinationPointId);
            if (!Application.CanStreamedLevelBeLoaded(destinationScenePath))
                throw new InvalidOperationException(name + ": destinationScene must be enabled in the build scene list: " + destinationScenePath);
        }

        public void Transfer()
        {
            if (!gameBootstrap.enabled) return;
            if (!SceneTransferSystem.Begin(World.DefaultGameObjectInjectionWorld, destinationScenePath, destinationPointId)) return;
            // 在卸载场景前按门控顺序关闭，避免 OnDestroy 时依赖已先被销毁。
            gameBootstrap.enabled = false;
            SceneManager.LoadSceneAsync(destinationScenePath, LoadSceneMode.Single);
        }

#if UNITY_EDITOR
        private void OnValidate() => destinationScenePath = destinationScene == null
            ? string.Empty : AssetDatabase.GetAssetPath(destinationScene);
#endif
    }
}
