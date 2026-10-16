using System;
using Emergence.Gameplay.Environment;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class EnvironmentBootstrap : MonoBehaviour
    {
        [SerializeField] private MapMovementMode movementMode;
        [Header("TopDown: XY map / Z height; SideView: XY map")]
        [SerializeField] private Vector3 windVelocity = new(0, 0, 1);
        [SerializeField, Min(0)] private float gravity = 9.81f;
        private World world;
        private Entity environment;

        public void Initialize()
        {
            // 校验
            if (!Enum.IsDefined(typeof(MapMovementMode), movementMode))
                throw new InvalidOperationException(name + ": movementMode is invalid.");
            if (!math.all(math.isfinite((float3)windVelocity)) || !math.isfinite(gravity) || gravity < 0)
                throw new InvalidOperationException(name + ": wind must be finite; gravity must be finite and nonnegative.");
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) throw new InvalidOperationException("Default ECS World is unavailable.");
            var manager = world.EntityManager;
            using var environmentQuery = manager.CreateEntityQuery(typeof(EnvironmentComponent));
            if (!environmentQuery.IsEmptyIgnoreFilter)
                throw new InvalidOperationException("Only one environment owner is supported.");
            // 初始化
            environment = manager.CreateEntity(typeof(EnvironmentComponent));
            manager.SetComponentData(environment, new EnvironmentComponent
            {
                MovementMode = movementMode, WindVelocity = windVelocity, Gravity = gravity
            });
        }

        public void Shutdown()
        {
            if (world != null && world.IsCreated && environment != Entity.Null)
                world.EntityManager.DestroyEntity(environment);
            environment = Entity.Null;
            world = null;
        }

    }
}
