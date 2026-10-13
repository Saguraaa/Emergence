using System;
using Emergence.Gameplay.Environment;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class EnvironmentBootstrap : MonoBehaviour
    {
        [Header("XY: map wind; Z: height wind")]
        [SerializeField] private Vector3 windVelocity = new Vector3(0, 0, 1);
        [SerializeField, Min(0)] private float gravity = 9.81f;
        private World world;
        private Entity environment;
        private bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            if (!math.all(math.isfinite((float3)windVelocity)) || !math.isfinite(gravity) || gravity < 0)
                throw new InvalidOperationException(name + ": wind must be finite; gravity must be finite and nonnegative.");
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) throw new InvalidOperationException("Default ECS World is unavailable.");
            var manager = world.EntityManager;
            using (var query = manager.CreateEntityQuery(typeof(EnvironmentComponent)))
                if (!query.IsEmptyIgnoreFilter)
                    throw new InvalidOperationException("Only one environment owner is supported.");
            try
            {
                environment = manager.CreateEntity(typeof(EnvironmentComponent));
                manager.SetComponentData(environment, new EnvironmentComponent { WindVelocity = windVelocity, Gravity = gravity });
                initialized = true;
            }
            catch { Shutdown(); throw; }
        }

        public void Shutdown()
        {
            if (world != null && world.IsCreated && world.EntityManager.Exists(environment))
                world.EntityManager.DestroyEntity(environment);
            environment = Entity.Null;
            world = null;
            initialized = false;
        }

        private void OnDestroy() => Shutdown();
    }
}
