using System;
using Emergence.Gameplay.Physics;
using Emergence.Gameplay.Player;
using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.NPC
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerSystem))]
    [UpdateBefore(typeof(PhysicsSystem))]
    public partial class NPCSystem : SystemBase
    {
        private PhysicsSystem physics;

        protected override void OnCreate() => RequireForUpdate<NPCTagComponent>();

        protected override void OnStartRunning()
        {
            // 校验
            var resolvedPhysics = World.GetExistingSystemManaged<PhysicsSystem>();
            if (resolvedPhysics == null) throw new InvalidOperationException("PhysicsSystem must be initialized before NPC movement.");

            // 初始化
            physics = resolvedPhysics;
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (position, motion, resistance, behavior, route, entity) in
                     SystemAPI.Query<RefRW<NPCPositionComponent>, RefRW<NPCMotionComponent>, RefRO<NPCResistanceComponent>,
                         RefRW<NPCBehaviorComponent>, DynamicBuffer<NPCWaypointComponent>>()
                         .WithAll<NPCTagComponent>().WithEntityAccess())
            {
                var state = behavior.ValueRO;
                var next = SelectBehavior(resistance.ValueRO, state);
                ChangeBehavior(ref state, next);
                float2 velocity = TickBehavior(position.ValueRO, motion.ValueRO, ref state, route, dt);
                Move(entity, ref position.ValueRW, ref motion.ValueRW, velocity, dt);
                behavior.ValueRW = state;
            }
        }

        private static NPCBehaviorKind SelectBehavior(NPCResistanceComponent resistance, NPCBehaviorComponent state)
        {
            if (state.Value == NPCBehaviorKind.Fleeing || resistance.Value <= resistance.InitialValue * state.FleeThresholdRatio)
                return NPCBehaviorKind.Fleeing;
            return resistance.Value < resistance.InitialValue ? NPCBehaviorKind.Alert : NPCBehaviorKind.Normal;
        }

        private static void ChangeBehavior(ref NPCBehaviorComponent state, NPCBehaviorKind next)
        {
            if (state.Value == next) return;
            state.Value = next;
            if (next != NPCBehaviorKind.Fleeing) return;
            // 只在进入逃跑时重置一次，逐步执行不会重新开始路线或等待。
            state.RoutePhase = NPCRoutePhase.Moving;
            state.TargetIndex = 0;
            state.WaitRemaining = 0;
            state.BlockedElapsed = 0;
            state.BestDistance = float.MaxValue;
        }

        private static float2 TickBehavior(NPCPositionComponent position, NPCMotionComponent motion,
            ref NPCBehaviorComponent state, DynamicBuffer<NPCWaypointComponent> route, float dt)
        {
            return state.Value switch
            {
                NPCBehaviorKind.Normal or NPCBehaviorKind.Alert => float2.zero,
                NPCBehaviorKind.Fleeing => TickFlee(position, motion, ref state, route, dt),
                _ => throw new ArgumentOutOfRangeException(nameof(state))
            };
        }

        private static float2 TickFlee(NPCPositionComponent position, NPCMotionComponent motion,
            ref NPCBehaviorComponent state, DynamicBuffer<NPCWaypointComponent> route, float dt)
        {
            return state.RoutePhase switch
            {
                NPCRoutePhase.Moving => MoveToWaypoint(position, motion, ref state, route, dt),
                NPCRoutePhase.Waiting => WaitAtWaypoint(ref state, route.Length, dt),
                NPCRoutePhase.Completed or NPCRoutePhase.Blocked => float2.zero,
                _ => throw new ArgumentOutOfRangeException(nameof(state))
            };
        }

        private static float2 MoveToWaypoint(NPCPositionComponent position, NPCMotionComponent motion,
            ref NPCBehaviorComponent state, DynamicBuffer<NPCWaypointComponent> route, float dt)
        {
            var target = route[state.TargetIndex];
            float2 offset = target.XY - position.XY;
            float distance = math.length(offset);
            if (distance <= motion.ArrivalDistance)
            {
                state.WaitRemaining = target.WaitDuration;
                state.RoutePhase = NPCRoutePhase.Waiting;
                if (state.WaitRemaining == 0) Advance(ref state, route.Length);
                return float2.zero;
            }
            float progressTolerance = math.min(.01f, motion.MoveVelocityMagnitude * dt * .5f);
            if (distance < state.BestDistance - progressTolerance)
            {
                state.BestDistance = distance;
                state.BlockedElapsed = 0;
            }
            else state.BlockedElapsed += dt;
            if (state.BlockedElapsed >= motion.BlockedDuration)
            {
                state.RoutePhase = NPCRoutePhase.Blocked;
                return float2.zero;
            }
            return offset / distance * math.min(motion.MoveVelocityMagnitude, distance / dt);
        }

        private static float2 WaitAtWaypoint(ref NPCBehaviorComponent state, int count, float dt)
        {
            state.WaitRemaining = math.max(0, state.WaitRemaining - dt);
            if (state.WaitRemaining == 0) Advance(ref state, count);
            return float2.zero;
        }

        private void Move(Entity entity, ref NPCPositionComponent position, ref NPCMotionComponent motion, float2 velocity, float dt)
        {
            var movement = physics.Move(entity, position.XY, velocity * dt, velocity, 0, 0, 0, 0);
            position.XY += movement.Displacement;
            motion.Velocity = movement.Velocity;
        }

        private static void Advance(ref NPCBehaviorComponent state, int count)
        {
            state.TargetIndex++;
            state.RoutePhase = state.TargetIndex == count ? NPCRoutePhase.Completed : NPCRoutePhase.Moving;
            state.BlockedElapsed = 0;
            state.BestDistance = float.MaxValue;
        }

        public static void RequestAttack(EntityManager manager, Entity npc)
        {
            var attack = manager.GetComponentData<NPCAttackComponent>(npc);
            if (!attack.HasAttack) return;
            attack.PendingAttacks++;
            manager.SetComponentData(npc, attack);
        }

        public static uint TakeAttacks(EntityManager manager, Entity npc)
        {
            var attack = manager.GetComponentData<NPCAttackComponent>(npc);
            uint count = attack.PendingAttacks;
            attack.PendingAttacks = 0;
            manager.SetComponentData(npc, attack);
            return count;
        }
    }
}
