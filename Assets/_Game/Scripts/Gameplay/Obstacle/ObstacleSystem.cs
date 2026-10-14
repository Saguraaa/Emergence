using System;
using Emergence.Gameplay.Physics;
using Emergence.Gameplay.Player;
using Unity.Entities;

namespace Emergence.Gameplay.Obstacle
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystem))]
    public partial class ObstacleSystem : SystemBase
    {
        private PhysicsSystem physics;

        protected override void OnCreate() => RequireForUpdate<PlayerTagComponent>();

        protected override void OnStartRunning()
        {
            physics = World.GetExistingSystemManaged<PhysicsSystem>();
            if (physics == null) throw new InvalidOperationException("PhysicsSystem must be initialized before obstacle interaction.");
        }

        protected override void OnUpdate()
        {
            foreach (var (form, input, contacts) in
                     SystemAPI.Query<RefRO<PlayerFormComponent>, RefRW<PlayerInputComponent>, DynamicBuffer<PhysicsContactComponent>>()
                         .WithAll<PlayerTagComponent>())
            {
                bool interactPressed = input.ValueRO.InteractPressed;
                input.ValueRW.InteractPressed = false;
                if (!interactPressed || form.ValueRO.Value != PlayerFormKind.Solid) continue;
                foreach (var contact in contacts)
                {
                    if (!SystemAPI.HasComponent<ObstacleDestructibleTagComponent>(contact.Entity)) continue;
                    var state = SystemAPI.GetComponent<ObstacleStateComponent>(contact.Entity);
                    if (state.IsDestroyed) continue;
                    state.IsDestroyed = true;
                    SystemAPI.SetComponent(contact.Entity, state);
                    physics.SetObstacleEnabled(contact.Entity, false);
                }
            }
        }
    }
}