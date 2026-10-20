using Emergence.UI.MainMenu;
using Emergence.UI.Input;
using Emergence.UI.Settings;
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace Emergence.Bootstrap
{
    // 开始场景总入口，仅协调界面的开启和关闭。
    public sealed class MainMenuSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private MainMenuPanel mainMenuPanel;
        [SerializeField] private InputBootstrap inputBootstrap;
        [SerializeField] private InputSystemUIInputModule uiInputModule;
        [SerializeField] private SettingsPanel settingsPanel;
        private InputSettingsController inputs;

        private void Start() => Initialize();

        public void Initialize()
        {
            if (mainMenuPanel == null) throw new MissingReferenceException(name + ": mainMenuPanel is required.");
            if (inputBootstrap == null) throw new MissingReferenceException(name + ": inputBootstrap is required.");
            if (uiInputModule == null) throw new MissingReferenceException(name + ": uiInputModule is required.");
            if (settingsPanel == null) throw new MissingReferenceException(name + ": settingsPanel is required.");
            inputs = inputBootstrap.Initialize();
            uiInputModule.actionsAsset = inputs.RuntimeActions;
            uiInputModule.enabled = true;
            mainMenuPanel.Initialize();
            settingsPanel.Initialize(inputs);
            mainMenuPanel.SettingsRequested.AddListener(settingsPanel.Show);
            mainMenuPanel.NewGameRequested.AddListener(ShutdownBeforeLevelLoad);
            mainMenuPanel.ContinueGameRequested.AddListener(ShutdownBeforeLevelLoad);
        }

        public void Shutdown()
        {
            mainMenuPanel.NewGameRequested.RemoveListener(ShutdownBeforeLevelLoad);
            mainMenuPanel.ContinueGameRequested.RemoveListener(ShutdownBeforeLevelLoad);
            mainMenuPanel.SettingsRequested.RemoveListener(settingsPanel.Show);
            if (settingsPanel.enabled) settingsPanel.Shutdown();
            if (mainMenuPanel.enabled) mainMenuPanel.Shutdown();
            uiInputModule.enabled = false;
            inputBootstrap.Shutdown();
            inputs = null;
        }

        private void ShutdownBeforeLevelLoad() => enabled = false;

        private void OnDisable()
        {
            if (inputs != null) Shutdown();
        }

        private void OnApplicationQuit() => enabled = false;
    }
}
