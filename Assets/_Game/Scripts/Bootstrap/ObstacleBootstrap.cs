using System;
using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Physics;
using Emergence.WorldView;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class ObstacleBootstrap : MonoBehaviour
    {
        [SerializeField] private Collider2D colliderBounds;
        [SerializeField] private bool isDestructible;
        [SerializeField, Min(0)] private float obstacleHeight = 1f;
        [SerializeField] private ObstacleController controller;
        [SerializeField] private ObstacleView obstacleView;
        private World world;
        private Entity obstacle;
        private PhysicsSystem physics;

        public void Initialize()
        {
            // 校验
            if (!math.isfinite(obstacleHeight) || obstacleHeight < 0)
                throw new InvalidOperationException(name + ": obstacleHeight must be finite and nonnegative.");
            if (controller == null) throw new MissingReferenceException(name + ": controller is required.");
            if (obstacleView == null) throw new MissingReferenceException(name + ": obstacleView is required.");
            if (colliderBounds == null)
                throw new MissingReferenceException(name + ": colliderBounds is required.");
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                throw new InvalidOperationException(name + ": Default ECS World is unavailable.");
            var manager = world.EntityManager;
            physics = world.GetExistingSystemManaged<PhysicsSystem>();
            if (physics == null) throw new InvalidOperationException("Default ECS World requires PhysicsSystem.");
            // 初始化
            obstacle = manager.CreateEntity(typeof(ObstacleTagComponent), typeof(ObstacleStateComponent), typeof(ObstacleHeightComponent));
            manager.SetComponentData(obstacle, new ObstacleHeightComponent { Height = obstacleHeight });
            manager.SetName(obstacle, name);
            physics.RegisterObstacle(obstacle, colliderBounds);
            if (isDestructible) manager.AddComponent<ObstacleDestructibleTagComponent>(obstacle);
            if (obstacleView.Initialize()) controller.Initialize(world, obstacle, obstacleView);
            else controller.Shutdown();
        }

        public void Shutdown()
        {
            if (controller != null) controller.Shutdown();
            if (world != null && world.IsCreated && obstacle != Entity.Null)
            {
                physics.UnregisterObstacle(obstacle);
                world.EntityManager.DestroyEntity(obstacle);
            }
            obstacle = Entity.Null;
            world = null;
            physics = null;
        }
    }
}
