using Emergence.UI.Input;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class InputBootstrap : MonoBehaviour
    {
        [SerializeField] private InputSettingsController inputSettings;

        public void Initialize()
        {
            // 校验
            if (inputSettings == null) throw new MissingReferenceException(name + ": inputSettings is required.");
            // 初始化
            inputSettings.Initialize();
        }

        public void Shutdown()
        {
            inputSettings.Shutdown();
        }

    }
}
