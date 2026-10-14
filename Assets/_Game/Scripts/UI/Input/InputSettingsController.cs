using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Emergence.UI.Input
{
    public sealed class InputSettingsController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        // 保留原存储键，已有改键记录继续有效。
        private const string SaveKey = "Emergence.Player.Bindings.v1";
        private InputActionAsset runtimeActions;
        private InputActionRebindingExtensions.RebindingOperation rebind;

        public bool IsRebinding { get; private set; }
        public event Action BindingsChanged;

        public void Initialize()
        {
            // 校验
            if (inputActions == null) throw new MissingReferenceException(name + ": inputActions is required.");
            // 初始化
            runtimeActions = Instantiate(inputActions);
            LoadBindings();
            if (isActiveAndEnabled) runtimeActions.Enable();
        }

        public InputAction GetAction(InputActionReference reference)
        {
            if (runtimeActions == null) throw new InvalidOperationException("Initialize input before the player.");
            if (reference == null || reference.action == null)
                throw new MissingReferenceException(name + ": a valid InputActionReference is required.");
            if (reference.asset != inputActions)
                throw new ArgumentException("Action reference must belong to the configured input asset.", nameof(reference));
            // Inspector 引用原资源；按稳定 ID 解析副本，保证改键与游戏读取同一个动作。
            return runtimeActions.FindAction(reference.action.id)
                ?? throw new InvalidOperationException($"Action {reference.name} is missing from the runtime input asset.");
        }

        public int GetBindingCount(InputActionReference action) => GetAction(action).bindings.Count;
        public InputBinding GetBinding(InputActionReference action, int index) => GetAction(action).bindings[index];
        public string GetBindingDisplayString(InputActionReference action, int index) =>
            GetAction(action).GetBindingDisplayString(index);

        public void BeginRebind(InputActionReference reference, int bindingIndex)
        {
            if (runtimeActions == null || !isActiveAndEnabled)
                throw new InvalidOperationException("Input settings are not active.");
            var action = GetAction(reference);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite)
                throw new ArgumentOutOfRangeException(nameof(bindingIndex), "Select a binding or composite part.");
            CancelRebind();
            runtimeActions.Disable();
            rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => FinishRebind(false))
                .OnComplete(_ => FinishRebind(true));
            IsRebinding = true;
            rebind.Start();
        }

        public void CancelRebind() => rebind?.Cancel();

        private void FinishRebind(bool save)
        {
            rebind.Dispose();
            rebind = null;
            IsRebinding = false;
            if (save) SaveBindings();
            if (isActiveAndEnabled) runtimeActions.Enable();
            BindingsChanged?.Invoke();
        }

        public void SaveBindings()
        {
            PlayerPrefs.SetString(SaveKey, runtimeActions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public void LoadBindings()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            runtimeActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(SaveKey));
        }

        public void ResetBindings()
        {
            CancelRebind();
            runtimeActions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }

        private void OnEnable() => runtimeActions?.Enable();
        private void OnDisable() { CancelRebind(); runtimeActions?.Disable(); }

        public void Shutdown()
        {
            CancelRebind();
            if (runtimeActions != null)
            {
                runtimeActions.Disable();
                if (Application.isPlaying) Destroy(runtimeActions);
                else DestroyImmediate(runtimeActions);
            }
            runtimeActions = null;
        }
    }
}
