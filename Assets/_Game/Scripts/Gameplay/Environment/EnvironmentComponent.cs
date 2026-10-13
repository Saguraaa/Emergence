using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Environment
{
    public struct EnvironmentComponent : IComponentData
    {
        // x/y move the ground projection; z changes height.
        public float3 WindVelocity;
        public float Gravity;
    }
}
