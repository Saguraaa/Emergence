using Emergence.UI.Input;
using Emergence.UI.Settings;
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace Emergence.Bootstrap
{
    public sealed class SettingsBootstrap : MonoBehaviour
    {
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private InputSystemUIInputModule uiInputModule;

        public void Initialize(InputSettingsController inputs)
        {
            if (inputs == null) throw new MissingReferenceException(name + ": inputs is required.");
            if (settingsPanel == null) throw new MissingReferenceException(name + ": settingsPanel is required.");
            if (uiInputModule == null) throw new MissingReferenceException(name + ": uiInputModule is required.");
            uiInputModule.actionsAsset = inputs.RuntimeActions;
            uiInputModule.enabled = true;
            settingsPanel.Initialize(inputs);
        }

        public void Shutdown()
        {
            settingsPanel.Shutdown();
            uiInputModule.enabled = false;
        }
    }
}
