using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Player
{
    public struct PlayerTagComponent : IComponentData { }
    public enum PlayerFormKind : byte { Solid = 0, Liquid = 1, Gas = 2 }

    // 三份参数常驻实体，恰好启用一个；启用状态是当前形态的唯一权威来源。
    public struct PlayerSolidComponent : IComponentData, IEnableableComponent
    {
        public float MoveVelocityMagnitude;
        public float ResponseTime;
    }

    public struct PlayerLiquidComponent : IComponentData, IEnableableComponent
    {
        public float MoveVelocityMagnitude;
    }

    public struct PlayerGasComponent : IComponentData, IEnableableComponent
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

    public struct PlayerInputComponent : IComponentData
    {
        public float2 Move;
        public bool InteractPressed;
    }
}
