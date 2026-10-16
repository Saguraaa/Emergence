using Unity.Entities;
using Unity.Mathematics;

namespace Emergence.Gameplay.NPC
{
    public struct NPCTagComponent : IComponentData { }

    public struct NPCPositionComponent : IComponentData
    {
        public float2 XY;
    }

    public struct NPCMotionComponent : IComponentData
    {
        public float2 Velocity;
        public float MoveVelocityMagnitude;
        public float ArrivalDistance;
        public float BlockedDuration;
    }

    public struct NPCResistanceComponent : IComponentData
    {
        public float InitialValue;
        public float Value;
    }

    public enum NPCBehaviorKind : byte { Normal, Alert, Fleeing }
    public enum NPCRoutePhase : byte { Moving, Waiting, Completed, Blocked }

    public struct NPCBehaviorComponent : IComponentData
    {
        public NPCBehaviorKind Value;
        public float FleeThresholdRatio;
        public NPCRoutePhase RoutePhase;
        public int TargetIndex;
        public float WaitRemaining;
        public float BlockedElapsed;
        public float BestDistance;
    }

    public struct NPCWaypointComponent : IBufferElementData
    {
        public float2 XY;
        public float WaitDuration;
    }

    public struct NPCAttackComponent : IComponentData
    {
        public bool HasAttack;
        public uint PendingAttacks;
    }
}
