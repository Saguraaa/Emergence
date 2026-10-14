using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Gameplay.Player
{
    [CreateAssetMenu(menuName = "Emergence/Player/Gas Config")]
    public sealed class PlayerGasConfig : ScriptableObject
    {
        [Min(0.01f)] public float MoveVelocityMagnitude = 1f;

        public bool IsValid => math.isfinite(MoveVelocityMagnitude) && MoveVelocityMagnitude > 0;

        public PlayerGasComponent CreateComponent() => new()
        {
            MoveVelocityMagnitude = MoveVelocityMagnitude
        };
    }
}
