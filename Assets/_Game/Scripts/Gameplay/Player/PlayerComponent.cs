using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Player
{
    public struct PlayerTag : IComponentData { }

    // Keep the existing serialized Solid/Liquid values.
    public enum PlayerFormKind : byte { Solid = 0, Liquid = 1, Gas = 2 }
    public struct PlayerForm : IComponentData { public PlayerFormKind Value; }

    public struct PlayerPosition : IComponentData
    {
        public float2 XY;
        public float Height;
    }

    public struct PlayerMoveSpeed : IComponentData
    {
        public float Gas;
        public float Liquid;
        public float Solid;
        public float SolidResponseTime;
    }

    public struct PlayerMotionState : IComponentData
    {
        // x/y are map velocity; z is height velocity.
        public float3 Velocity;
    }

    public struct PlayerInput : IComponentData
    {
        public float2 Move;
        public bool InteractPressed;
    }
}
