using System;
using Emergence.Gameplay.Respawn;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class RespawnBootstrap : MonoBehaviour
    {
        [SerializeField] private bool initiallyEnabled = true;
        [SerializeField, Min(0)] private float height;
        private World world;
        private Entity point;

        public void Initialize()
        {
            // 校验
            Vector3 position = transform.position;
            if (!math.all(math.isfinite((float3)position)) || !math.isfinite(height) || height < 0)
                throw new InvalidOperationException(name + ": position and height must be finite; height must be nonnegative.");
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) throw new InvalidOperationException(name + ": Default ECS World is unavailable.");
            // 初始化：Transform 只提供初值，权威位置与启用状态保存在 ECS。
            var manager = world.EntityManager;
            point = manager.CreateEntity(typeof(RespawnPointComponent), typeof(RespawnEnabledTagComponent));
            manager.SetName(point, name);
            manager.SetComponentData(point, new RespawnPointComponent { XY = new float2(position.x, position.y), Height = height });
            RespawnSystem.SetEnabled(manager, point, initiallyEnabled);
        }

        public void Shutdown()
        {
            if (world != null && world.IsCreated && point != Entity.Null) world.EntityManager.DestroyEntity(point);
            world = null;
            point = Entity.Null;
        }
    }
}
