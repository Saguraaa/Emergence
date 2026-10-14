using System;
using Emergence.WorldView;
using Unity.Entities;
using UnityEngine;

namespace Emergence.Gameplay.Obstacle
{
    public sealed class ObstacleController : MonoBehaviour
    {
        private World world;
        private Entity obstacle;
        private ObstacleView view;

        public void Initialize(World targetWorld, Entity entity, ObstacleView obstacleView)
        {
            // 校验
            enabled = false;
            if (targetWorld == null || !targetWorld.IsCreated || !targetWorld.EntityManager.Exists(entity) ||
                !targetWorld.EntityManager.HasComponent<ObstacleStateComponent>(entity))
                throw new ArgumentException(name + ": ObstacleController requires a live obstacle entity with ObstacleStateComponent.");
            if (obstacleView == null) throw new MissingReferenceException(name + ": obstacleView is required.");
            // 初始化
            world = targetWorld;
            obstacle = entity;
            view = obstacleView;
            enabled = true;
        }

        private void LateUpdate() => view.Refresh(world.EntityManager.GetComponentData<ObstacleStateComponent>(obstacle).IsDestroyed);

        public void Shutdown()
        {
            enabled = false;
            world = null;
            obstacle = Entity.Null;
            view = null;
        }
    }
}
