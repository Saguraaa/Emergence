using Emergence.UI.Input;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class InputBootstrap : MonoBehaviour
    {
        [SerializeField] private InputSettingsController inputSettings;
        private bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            if (inputSettings == null) throw new MissingReferenceException(name + ": inputSettings is required.");
            inputSettings.Initialize();
            initialized = true;
        }

        public void Shutdown()
        {
            if (!initialized) return;
            if (inputSettings != null) inputSettings.Shutdown();
            initialized = false;
        }

        private void OnDestroy() => Shutdown();
    }
}
