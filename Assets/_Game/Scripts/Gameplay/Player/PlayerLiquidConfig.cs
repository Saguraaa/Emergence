using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Gameplay.Player
{
    [CreateAssetMenu(menuName = "Emergence/Player/Liquid Config")]
    public sealed class PlayerLiquidConfig : ScriptableObject
    {
        [Min(0.01f)] public float MoveVelocityMagnitude = 4f;

        public bool IsValid => math.isfinite(MoveVelocityMagnitude) && MoveVelocityMagnitude > 0;

        public PlayerLiquidComponent CreateComponent() => new()
        {
            MoveVelocityMagnitude = MoveVelocityMagnitude
        };
    }
}
