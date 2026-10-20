using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Unity.Entities;
using UnityEngine.SceneManagement;
using Emergence.Gameplay.Save;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Emergence.UI.MainMenu
{
    public sealed class MainMenuPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button aboutButton;
#if UNITY_EDITOR
        [SerializeField] private SceneAsset firstLevelScene;
#endif
        [SerializeField, HideInInspector] private string firstLevelScenePath;
        private World world;

        [Header("菜单操作事件")]
        [SerializeField] private UnityEvent newGameRequested = new UnityEvent();
        [SerializeField] private UnityEvent continueGameRequested = new UnityEvent();
        [SerializeField] private UnityEvent settingsRequested = new UnityEvent();
        [SerializeField] private UnityEvent aboutRequested = new UnityEvent();

        public UnityEvent NewGameRequested => newGameRequested;
        public UnityEvent ContinueGameRequested => continueGameRequested;
        public UnityEvent SettingsRequested => settingsRequested;
        public UnityEvent AboutRequested => aboutRequested;

        public void Initialize()
        {
            if (canvasGroup == null) throw new MissingReferenceException(name + ": canvasGroup is required.");
            if (newGameButton == null) throw new MissingReferenceException(name + ": newGameButton is required.");
            if (continueGameButton == null) throw new MissingReferenceException(name + ": continueGameButton is required.");
            if (settingsButton == null) throw new MissingReferenceException(name + ": settingsButton is required.");
            if (aboutButton == null) throw new MissingReferenceException(name + ": aboutButton is required.");
            if (newGameRequested == null) throw new MissingReferenceException(name + ": newGameRequested is required.");
            if (continueGameRequested == null) throw new MissingReferenceException(name + ": continueGameRequested is required.");
            if (settingsRequested == null) throw new MissingReferenceException(name + ": settingsRequested is required.");
            if (aboutRequested == null) throw new MissingReferenceException(name + ": aboutRequested is required.");
            if (!LevelProgressSystem.IsLevelAvailable(firstLevelScenePath))
                throw new System.InvalidOperationException(name + ": firstLevelScene must be an enabled level scene.");
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                throw new System.InvalidOperationException(name + ": Default ECS World is unavailable.");

            newGameButton.onClick.AddListener(RequestNewGame);
            continueGameButton.onClick.AddListener(RequestContinueGame);
            settingsButton.onClick.AddListener(RequestSettings);
            aboutButton.onClick.AddListener(RequestAbout);
            continueGameButton.interactable = LevelProgressSystem.TryReadSave(out _);
            canvasGroup.interactable = true;
            enabled = true;
        }

        public void Shutdown()
        {
            enabled = false;
            canvasGroup.interactable = false;
            newGameButton.onClick.RemoveListener(RequestNewGame);
            continueGameButton.onClick.RemoveListener(RequestContinueGame);
            settingsButton.onClick.RemoveListener(RequestSettings);
            aboutButton.onClick.RemoveListener(RequestAbout);
            world = null;
        }

        private void RequestNewGame()
        {
            if (!LevelProgressSystem.NewGame(world.EntityManager, firstLevelScenePath)) return;
            canvasGroup.interactable = false;
            newGameRequested.Invoke();
            SceneManager.LoadSceneAsync(firstLevelScenePath, LoadSceneMode.Single);
        }

        private void RequestContinueGame()
        {
            if (!LevelProgressSystem.ContinueGame(world.EntityManager, out string level))
            {
                continueGameButton.interactable = LevelProgressSystem.TryReadSave(out _);
                return;
            }
            canvasGroup.interactable = false;
            continueGameRequested.Invoke();
            SceneManager.LoadSceneAsync(level, LoadSceneMode.Single);
        }

        private void RequestSettings() => settingsRequested.Invoke();
        private void RequestAbout() => aboutRequested.Invoke();

#if UNITY_EDITOR
        private void OnValidate() => firstLevelScenePath = firstLevelScene == null
            ? string.Empty : AssetDatabase.GetAssetPath(firstLevelScene);
#endif
    }
}
