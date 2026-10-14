using Emergence.UI.Input;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class InputBootstrap : MonoBehaviour
    {
        [SerializeField] private InputSettingsController inputSettings;

        public void Initialize()
        {
            if (inputSettings == null) throw new MissingReferenceException(name + ": inputSettings is required.");
            inputSettings.Initialize();
        }

        public void Shutdown()
        {
            inputSettings.Shutdown();
        }

    }
}
