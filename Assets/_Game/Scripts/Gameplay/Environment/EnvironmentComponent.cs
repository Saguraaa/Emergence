using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Environment
{
    public enum MapMovementMode : byte { TopDown = 0, SideView = 1 }

    public struct EnvironmentComponent : IComponentData
    {
        public MapMovementMode MovementMode;
        // TopDown: XY moves the ground projection, Z changes Height. SideView: XY moves on the map.
        public float3 WindVelocity;
        public float Gravity;
    }
}
