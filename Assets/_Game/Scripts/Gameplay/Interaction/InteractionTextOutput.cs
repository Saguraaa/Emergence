using UnityEngine;

namespace Emergence.Gameplay.Interaction
{
    public sealed class InteractionTextOutput : MonoBehaviour
    {
        [SerializeField] private string message = "ActivativeTest: interaction activated.";

        public void Output() => Debug.Log(message, this);
    }
}
