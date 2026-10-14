using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Gameplay.Player
{
    [CreateAssetMenu(menuName = "Emergence/Player/Solid Config")]
    public sealed class PlayerSolidConfig : ScriptableObject
    {
        [Min(0.01f)] public float MoveVelocityMagnitude = 3f;
        [Min(0.01f)] public float ResponseTime = 0.4f;

        public bool IsValid => math.isfinite(MoveVelocityMagnitude) && MoveVelocityMagnitude > 0
            && math.isfinite(ResponseTime) && ResponseTime > 0;

        public PlayerSolidComponent CreateComponent() => new()
        {
            MoveVelocityMagnitude = MoveVelocityMagnitude,
            ResponseTime = ResponseTime
        };
    }
}
