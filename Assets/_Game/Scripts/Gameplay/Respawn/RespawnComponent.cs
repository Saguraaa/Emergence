using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Respawn
{
    public struct RespawnEnabledTagComponent : IComponentData, IEnableableComponent { }

    public struct RespawnPointComponent : IComponentData
    {
        public float2 XY;
        public float Height;
    }
}
