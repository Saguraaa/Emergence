using Unity.Entities;

namespace Emergence.Gameplay.Interaction
{
    public enum InteractionMode
    {
        InteractPressed,
        EnterRange
    }

    public struct InteractiveComponent : IComponentData { }

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
