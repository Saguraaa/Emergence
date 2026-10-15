using System;
using Unity.Entities;

namespace Emergence.Gameplay.Interaction
{
    public enum InteractionMode
    {
        InteractPressed,
        EnterRange
    }

    public struct InteractiveComponent : IComponentData { }

    [Flags]
    public enum InteractionForms
    {
        Solid = 1,
        Liquid = 2,
        Gas = 4,
        All = Solid | Liquid | Gas
    }

    public struct InteractionRequirementComponent : IComponentData
    {
        public float MinimumHeight;
        public InteractionForms AllowedForms;
    }

    public struct InteractionStateComponent : IComponentData
    {
        public InteractionMode Mode;
        public bool IsOccupied;
        public uint PendingActivations;
    }

    public struct InteractionOverlapComponent : IBufferElementData
    {
        public Entity Entity;
    }
}
