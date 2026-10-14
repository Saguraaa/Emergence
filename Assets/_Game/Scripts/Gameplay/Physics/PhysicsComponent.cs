using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Physics
{
    public struct PhysicsContactComponent : IBufferElementData
    {
        public Entity Entity;
        public float2 Normal;
    }
}
