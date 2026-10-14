using Unity.Entities;

namespace Emergence.Gameplay.Obstacle
{
    public struct ObstacleTagComponent : IComponentData { }
    public struct ObstacleDestructibleTagComponent : IComponentData { }

    public struct ObstacleStateComponent : IComponentData
    {
        public bool IsDestroyed;
    }
}
