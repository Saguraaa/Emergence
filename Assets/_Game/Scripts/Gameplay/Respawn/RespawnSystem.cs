using System;
using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Physics;
using Emergence.Gameplay.Player;
using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.Respawn
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(ObstacleSystem))]
    public partial class RespawnSystem : SystemBase
    {
        private PhysicsSystem physics;
        public event Action<Entity> Respawned;

        protected override void OnStartRunning()
        {
            physics = World.GetExistingSystemManaged<PhysicsSystem>();
            if (physics == null) throw new InvalidOperationException("Respawn requires PhysicsSystem.");
        }

        public static void SetEnabled(EntityManager manager, Entity point, bool enabled) =>
            manager.SetComponentEnabled<RespawnEnabledTagComponent>(point, enabled);

        protected override void OnUpdate()
        {
            Entity point = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<RespawnPointComponent>>()
                         .WithAll<RespawnEnabledTagComponent>().WithEntityAccess())
            {
                if (point != Entity.Null)
                    throw new InvalidOperationException("Enable exactly one respawn point at a time.");
                point = entity;
            }
            foreach (var (position, motion, input, form, entity) in
                     SystemAPI.Query<RefRW<PlayerPositionComponent>, RefRW<PlayerMotionComponent>,
                         RefRW<PlayerInputComponent>, RefRO<PlayerFormComponent>>()
                         .WithAll<PlayerTagComponent, DeadTagComponent>().WithEntityAccess())
            {
                motion.ValueRW = new PlayerMotionComponent();
                input.ValueRW = new PlayerInputComponent();
                // 没有启用点时保留死亡状态；启用点后再复活。
                if (point == Entity.Null) continue;
                var spawn = SystemAPI.GetComponent<RespawnPointComponent>(point);
                var movement = physics.Move(entity, spawn.XY, float2.zero, float2.zero, (int)form.ValueRO.Value);
                position.ValueRW = new PlayerPositionComponent { XY = spawn.XY + movement.Displacement, Height = spawn.Height };
                EntityManager.GetBuffer<PhysicsContactComponent>(entity).Clear();
                EntityManager.SetComponentEnabled<DeadTagComponent>(entity, false);
                Respawned?.Invoke(entity);
            }
        }
    }
}
