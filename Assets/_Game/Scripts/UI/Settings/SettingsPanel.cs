using System;
using System.Linq;
using Emergence.UI.Input;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Emergence.UI.Settings
{
    public sealed class SettingsPanel : MonoBehaviour
    {
        [Serializable]
        private sealed class BindingControl
        {
            public InputActionReference action;
            public int bindingIndex;
            public Button button;
            public Text label;
            [NonSerialized] public UnityAction clicked;
        }

        [SerializeField] private GameObject overlay;
        [SerializeField] private CanvasGroup menuCanvasGroup;
        [SerializeField] private Button returnSelection;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private TabContainer tabContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button quitButton;
        [Tooltip("仅关卡内设置绑定 UI/Cancel；开始界面留空，不响应 Esc。")]
        [SerializeField] private InputActionReference settingsAction;
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Dropdown windowModeDropdown;
        [SerializeField] private Dropdown frameRateDropdown;
        [SerializeField] private Dropdown vSyncDropdown;
        [SerializeField] private Text frameRateHint;
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider soundEffectsSlider;
        [SerializeField] private Text musicValue;
        [SerializeField] private Text soundEffectsValue;
        [SerializeField] private BindingControl[] bindings;
        [SerializeField] private Text rebindStatus;
        [SerializeField] private Button cancelRebindButton;
        [SerializeField] private Button resetBindingsButton;
        private InputSettingsController inputs;
        private InputAction settings;
        private bool gameplayMapWasEnabled;
        private Vector2Int[] resolutions;
        private static readonly int[] FrameRates = { 30, 60, 120, 144, -1 };
        private static readonly FullScreenMode[] WindowModes =
            { FullScreenMode.FullScreenWindow, FullScreenMode.Windowed, FullScreenMode.ExclusiveFullScreen };
        private const string PreferencePrefix = "Emergence.Settings.";

        public void Initialize(InputSettingsController inputSettings)
        {
            if (inputSettings == null) throw new MissingReferenceException(name + ": inputSettings is required.");
            UnityEngine.Object[] required = { overlay, eventSystem, tabContainer,
                closeButton, quitButton, resolutionDropdown, windowModeDropdown, frameRateDropdown, vSyncDropdown, frameRateHint,
                audioMixer, musicSlider, soundEffectsSlider, musicValue, soundEffectsValue, rebindStatus,
                cancelRebindButton, resetBindingsButton };
            string[] fields = { nameof(overlay), nameof(eventSystem),
                nameof(tabContainer), nameof(closeButton), nameof(quitButton), nameof(resolutionDropdown), nameof(windowModeDropdown),
                nameof(frameRateDropdown), nameof(vSyncDropdown), nameof(frameRateHint), nameof(audioMixer),
                nameof(musicSlider), nameof(soundEffectsSlider), nameof(musicValue), nameof(soundEffectsValue),
                nameof(rebindStatus), nameof(cancelRebindButton), nameof(resetBindingsButton) };
            for (int i = 0; i < required.Length; i++)
                if (required[i] == null) throw new MissingReferenceException(name + ": " + fields[i] + " is required.");
            if ((menuCanvasGroup == null) != (returnSelection == null))
                throw new MissingReferenceException(name + ": menuCanvasGroup and returnSelection must both be assigned for the main menu, or both empty in a level.");
            if (!audioMixer.GetFloat("MusicVolume", out _) || !audioMixer.GetFloat("SoundEffectsVolume", out _))
                throw new InvalidOperationException(name + ": audioMixer must expose MusicVolume and SoundEffectsVolume.");
            if (bindings == null || bindings.Length == 0)
                throw new MissingReferenceException(name + ": bindings are required.");
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null) throw new MissingReferenceException(name + ": bindings[" + i + "] is required.");
                if (binding.action == null) throw new MissingReferenceException(name + ": bindings[" + i + "].action is required.");
                if (binding.button == null) throw new MissingReferenceException(name + ": bindings[" + i + "].button is required.");
                if (binding.label == null) throw new MissingReferenceException(name + ": bindings[" + i + "].label is required.");
                var action = inputSettings.GetAction(binding.action);
                if (action.type != InputActionType.Button && action.expectedControlType != "Vector2")
                    throw new InvalidOperationException(name + ": " + binding.action.name + " must be Button or Vector2 input.");
                if (binding.bindingIndex < 0 || binding.bindingIndex >= action.bindings.Count ||
                    action.bindings[binding.bindingIndex].isComposite ||
                    !action.bindings[binding.bindingIndex].path.StartsWith("<Keyboard>/", StringComparison.Ordinal))
                    throw new InvalidOperationException(name + ": " + binding.action.name + " requires a keyboard binding or composite part.");
            }
            if (settingsAction != null)
            {
                settings = inputSettings.GetAction(settingsAction);
                if (settings.type != InputActionType.Button)
                    throw new InvalidOperationException(name + ": settingsAction must be a Button action.");
                if (settings.actionMap == inputSettings.GetAction(bindings[0].action).actionMap)
                    throw new InvalidOperationException(name + ": settingsAction must belong to the UI action map, separate from gameplay input.");
            }
            inputs = inputSettings;
            tabContainer.Initialize();
            tabContainer.TabChanged += OnTabChanged;
            PopulateDisplay();
            musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(PreferencePrefix + "Music", 1));
            soundEffectsSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(PreferencePrefix + "SoundEffects", 1));
            ApplyWindow(0);
            ApplyTiming(0);
            SetMusicVolume(musicSlider.value);
            SetSoundEffectsVolume(soundEffectsSlider.value);
            resolutionDropdown.onValueChanged.AddListener(ApplyWindow);
            windowModeDropdown.onValueChanged.AddListener(ApplyWindow);
            frameRateDropdown.onValueChanged.AddListener(ApplyTiming);
            vSyncDropdown.onValueChanged.AddListener(ApplyTiming);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            soundEffectsSlider.onValueChanged.AddListener(SetSoundEffectsVolume);
            closeButton.onClick.AddListener(Close);
            quitButton.onClick.AddListener(QuitGame);
            if (settings != null) settings.performed += OnSettingsPressed;
            cancelRebindButton.onClick.AddListener(CancelRebind);
            resetBindingsButton.onClick.AddListener(ResetBindings);
            foreach (var binding in bindings)
            {
                var control = binding;
                binding.clicked = () => BeginRebind(control);
                binding.button.onClick.AddListener(binding.clicked);
            }
            inputs.BindingsChanged += RefreshBindings;
            RefreshBindings();
            overlay.SetActive(false);
            Cursor.visible = settingsAction == null;
            enabled = true;
        }

        private void PopulateDisplay()
        {
            resolutions = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct()
                .OrderBy(r => r.x).ThenBy(r => r.y).ToArray();
            if (resolutions.Length == 0) resolutions = new[] { new Vector2Int(Screen.width, Screen.height) };
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(resolutions.Select(r => r.x + " × " + r.y).ToList());
            var saved = new Vector2Int(PlayerPrefs.GetInt(PreferencePrefix + "Width", Screen.currentResolution.width),
                PlayerPrefs.GetInt(PreferencePrefix + "Height", Screen.currentResolution.height));
            int resolutionIndex = Array.IndexOf(resolutions, saved);
            // 显示器改变后，使用当前设备支持的分辨率。
            resolutionDropdown.SetValueWithoutNotify(resolutionIndex >= 0 ? resolutionIndex : resolutions.Length - 1);
            windowModeDropdown.ClearOptions();
            windowModeDropdown.AddOptions(new System.Collections.Generic.List<string> { "无边框全屏", "窗口", "独占全屏" });
            int mode = Array.IndexOf(WindowModes, Screen.fullScreenMode);
            windowModeDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt(PreferencePrefix + "WindowMode", Mathf.Max(0, mode)));
            frameRateDropdown.ClearOptions();
            frameRateDropdown.AddOptions(FrameRates.Select(f => f < 0 ? "不限帧率" : f + " FPS").ToList());
            frameRateDropdown.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(FrameRates, PlayerPrefs.GetInt(PreferencePrefix + "FrameRate", 60))));
            vSyncDropdown.ClearOptions();
            vSyncDropdown.AddOptions(new System.Collections.Generic.List<string> { "关闭", "开启" });
            vSyncDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt(PreferencePrefix + "VSync", QualitySettings.vSyncCount > 0 ? 1 : 0));
        }

        private void ApplyWindow(int value)
        {
            var resolution = resolutions[resolutionDropdown.value];
            Screen.SetResolution(resolution.x, resolution.y, WindowModes[windowModeDropdown.value]);
            PlayerPrefs.SetInt(PreferencePrefix + "Width", resolution.x);
            PlayerPrefs.SetInt(PreferencePrefix + "Height", resolution.y);
            PlayerPrefs.SetInt(PreferencePrefix + "WindowMode", windowModeDropdown.value);
            PlayerPrefs.Save();
        }

        private void ApplyTiming(int value)
        {
            QualitySettings.vSyncCount = vSyncDropdown.value;
            Application.targetFrameRate = FrameRates[frameRateDropdown.value];
            frameRateDropdown.interactable = vSyncDropdown.value == 0;
            frameRateHint.text = vSyncDropdown.value == 0 ? "选择游戏的帧率上限。" : "垂直同步已开启，帧率由显示器刷新率决定。";
            PlayerPrefs.SetInt(PreferencePrefix + "FrameRate", Application.targetFrameRate);
            PlayerPrefs.SetInt(PreferencePrefix + "VSync", QualitySettings.vSyncCount);
            PlayerPrefs.Save();
        }

        private void SetMusicVolume(float value)
        {
            audioMixer.SetFloat("MusicVolume", Decibels(value));
            musicValue.text = Mathf.RoundToInt(value * 100) + "%";
            PlayerPrefs.SetFloat(PreferencePrefix + "Music", value);
            PlayerPrefs.Save();
        }

        private void SetSoundEffectsVolume(float value)
        {
            audioMixer.SetFloat("SoundEffectsVolume", Decibels(value));
            soundEffectsValue.text = Mathf.RoundToInt(value * 100) + "%";
            PlayerPrefs.SetFloat(PreferencePrefix + "SoundEffects", value);
            PlayerPrefs.Save();
        }

        private static float Decibels(float value) => value <= 0 ? -80 : Mathf.Log10(value) * 20;

        public void Show()
        {
            if (overlay.activeSelf) return;
            gameplayMapWasEnabled = inputs.SetActionMapEnabled(bindings[0].action, false);
            if (menuCanvasGroup != null) menuCanvasGroup.interactable = false;
            overlay.SetActive(true);
            Cursor.visible = true;
            tabContainer.Select(0);
            eventSystem.SetSelectedGameObject(resolutionDropdown.gameObject);
        }

        public void Close()
        {
            if (!overlay.activeSelf) return;
            inputs.CancelRebind();
            overlay.SetActive(false);
            Cursor.visible = settingsAction == null;
            inputs.SetActionMapEnabled(bindings[0].action, gameplayMapWasEnabled);
            if (menuCanvasGroup != null) menuCanvasGroup.interactable = true;
            eventSystem.SetSelectedGameObject(returnSelection == null ? null : returnSelection.gameObject);
        }

        private void OnSettingsPressed(InputAction.CallbackContext context)
        {
            // 改键先取消时，动作回调可能在同一帧随后到达；这一次 Esc 不关闭面板。
            if (inputs.IsRebinding || inputs.LastRebindEndFrame == Time.frameCount) return;
            if (!overlay.activeSelf) { Show(); return; }
            // 展开的下拉列表由 UGUI 消费 Cancel，保留设置面板。
            if (resolutionDropdown.transform.Find("Dropdown List") != null ||
                windowModeDropdown.transform.Find("Dropdown List") != null ||
                frameRateDropdown.transform.Find("Dropdown List") != null ||
                vSyncDropdown.transform.Find("Dropdown List") != null) return;
            Close();
        }

        private void QuitGame()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnTabChanged(int index)
        {
            inputs.CancelRebind();
            eventSystem.SetSelectedGameObject(index == 0 ? resolutionDropdown.gameObject :
                index == 1 ? musicSlider.gameObject : bindings[0].button.gameObject);
        }

        private void BeginRebind(BindingControl binding)
        {
            inputs.BeginRebind(binding.action, binding.bindingIndex);
            RefreshBindings();
            binding.label.text = "请按键…";
            eventSystem.SetSelectedGameObject(cancelRebindButton.gameObject);
        }

        private void CancelRebind() => inputs.CancelRebind();
        private void ResetBindings() => inputs.ResetBindings();

        private void RefreshBindings()
        {
            foreach (var binding in bindings)
            {
                binding.label.text = inputs.GetBindingDisplayString(binding.action, binding.bindingIndex);
                binding.button.interactable = !inputs.IsRebinding;
            }
            resetBindingsButton.interactable = !inputs.IsRebinding;
            cancelRebindButton.gameObject.SetActive(inputs.IsRebinding);
            rebindStatus.text = inputs.IsRebinding ? "按下新按键，或按 Esc 取消。" : "点击按键即可修改，修改后自动保存。";
        }

        public void Shutdown()
        {
            enabled = false;
            if (settings != null) settings.performed -= OnSettingsPressed;
            settings = null;
            inputs.BindingsChanged -= RefreshBindings;
            inputs.CancelRebind();
            if (overlay.activeSelf) inputs.SetActionMapEnabled(bindings[0].action, gameplayMapWasEnabled);
            tabContainer.TabChanged -= OnTabChanged;
            tabContainer.Shutdown();
            resolutionDropdown.onValueChanged.RemoveListener(ApplyWindow);
            windowModeDropdown.onValueChanged.RemoveListener(ApplyWindow);
            frameRateDropdown.onValueChanged.RemoveListener(ApplyTiming);
            vSyncDropdown.onValueChanged.RemoveListener(ApplyTiming);
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
            soundEffectsSlider.onValueChanged.RemoveListener(SetSoundEffectsVolume);
            closeButton.onClick.RemoveListener(Close);
            quitButton.onClick.RemoveListener(QuitGame);
            cancelRebindButton.onClick.RemoveListener(CancelRebind);
            resetBindingsButton.onClick.RemoveListener(ResetBindings);
            foreach (var binding in bindings) binding.button.onClick.RemoveListener(binding.clicked);
            overlay.SetActive(false);
            Cursor.visible = true;
        }
    }
}
