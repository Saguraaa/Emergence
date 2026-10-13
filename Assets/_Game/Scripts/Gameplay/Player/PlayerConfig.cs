using UnityEngine;

namespace Emergence.Gameplay.Player
{
    [CreateAssetMenu(menuName = "Emergence/Player Config")]
    public sealed class PlayerConfig : ScriptableObject
    {
        public PlayerFormKind InitialForm = PlayerFormKind.Solid;
        [Min(0.01f)] public float GasSpeed = 1f;
        [Min(0.01f)] public float LiquidSpeed = 4f;
        [Min(0.01f)] public float SolidSpeed = 3f;
        [Min(0.01f)] public float SolidResponseTime = 0.4f;

        public bool IsValid =>
            GasSpeed > 0 && SolidSpeed > GasSpeed && LiquidSpeed > SolidSpeed &&
            !float.IsInfinity(LiquidSpeed) &&
            SolidResponseTime > 0 && !float.IsInfinity(SolidResponseTime) &&
            (InitialForm == PlayerFormKind.Solid || InitialForm == PlayerFormKind.Liquid ||
             InitialForm == PlayerFormKind.Gas);

        public PlayerMoveSpeed CreateSpeed() => new PlayerMoveSpeed
        {
            Gas = GasSpeed,
            Liquid = LiquidSpeed,
            Solid = SolidSpeed,
            SolidResponseTime = SolidResponseTime
        };
    }
}
