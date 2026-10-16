using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Player
{
    public struct PlayerTagComponent : IComponentData { }
    public struct DeadTagComponent : IComponentData, IEnableableComponent { }
    public enum PlayerFormKind : byte { Solid = 0, Liquid = 1, Gas = 2 }
    // 单一枚举字段保存当前形态；三份参数不承担形态状态。
    public struct PlayerFormComponent : IComponentData
    {
        public PlayerFormKind Value;
    }
    public struct PlayerSolidComponent : IComponentData
    {
        public float MoveVelocityMagnitude;
        public float ResponseTime;
    }

    public struct PlayerLiquidComponent : IComponentData
    {
        public float MoveVelocityMagnitude;
    }

    public struct PlayerGasComponent : IComponentData
    {
        public float MoveVelocityMagnitude;
    }

    // 位置与实际运动跨形态共享，切换时不复制或重置。
    public struct PlayerPositionComponent : IComponentData
    {
        public float2 XY;
        public float Height;
    }

    public struct PlayerMotionComponent : IComponentData
    {
        // XY 为地图运动，Z 为独立高度的运动。
        public float3 Velocity;
    }

    public struct DamageFieldComponent : IComponentData
    {
        public float Radius;
        public float Strength;
    }

    public struct PlayerInputComponent : IComponentData
    {
        public float2 Move;
        public bool InteractPressed;
    }
}
