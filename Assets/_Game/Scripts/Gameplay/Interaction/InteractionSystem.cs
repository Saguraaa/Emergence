using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Physics;
using Emergence.Gameplay.Player;
using Unity.Entities;

namespace Emergence.Gameplay.Interaction
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystem))]
    [UpdateBefore(typeof(ObstacleSystem))]
    public partial class InteractionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (state, overlaps) in
                     SystemAPI.Query<RefRW<InteractionStateComponent>, DynamicBuffer<InteractionOverlapComponent>>()
                         .WithAll<InteractiveComponent>())
            {
                bool occupied = false;
                bool pressed = false;
                foreach (var overlap in overlaps)
                {
                    if (!SystemAPI.HasComponent<PlayerTagComponent>(overlap.Entity)) continue;
                    occupied = true;
                    pressed |= SystemAPI.GetComponent<PlayerInputComponent>(overlap.Entity).InteractPressed;
                }
                if (state.ValueRO.Mode == InteractionMode.InteractPressed ? pressed : occupied && !state.ValueRO.IsOccupied)
                    state.ValueRW.PendingActivations++;
                state.ValueRW.IsOccupied = occupied;
            }
        }

        public static uint TakeActivations(EntityManager manager, Entity entity)
        {
            var state = manager.GetComponentData<InteractionStateComponent>(entity);
            uint count = state.PendingActivations;
            state.PendingActivations = 0;
            manager.SetComponentData(entity, state);
            return count;
        }
    }
}
