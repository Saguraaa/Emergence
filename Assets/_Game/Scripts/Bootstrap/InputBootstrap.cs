using Emergence.UI.Input;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class InputBootstrap : MonoBehaviour
    {
        [SerializeField] private InputSettingsController inputSettings;

        public InputSettingsController Initialize()
        {
            // 校验
            if (inputSettings == null) throw new MissingReferenceException(name + ": inputSettings is required.");

            // 初始化
            inputSettings.Initialize();
            return inputSettings;
        }

        public void Shutdown()
        {
            inputSettings.Shutdown();
        }

    }
}
