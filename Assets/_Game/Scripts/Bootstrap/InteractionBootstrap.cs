using System;
using Emergence.Gameplay.Interaction;
using Emergence.Gameplay.Physics;
using Emergence.WorldView;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class InteractionBootstrap : MonoBehaviour
    {
        [SerializeField] private InteractionMode interactionMode;
        [SerializeField, Min(0)] private float minimumHeight;
        [SerializeField, Min(0)] private float maximumHeight = float.MaxValue;
        [SerializeField] private InteractionForms allowedForms = InteractionForms.All;
        [SerializeField] private Collider2D triggerRange;
        [SerializeField] private LayerMask interactionLayers;
        [SerializeField] private InteractionController controller;
        [SerializeField] private InteractionView interactionView;
        private World world;
        private Entity interaction;
        private PhysicsSystem physics;

        public void Initialize()
        {
            // 校验
            if (!math.isfinite(minimumHeight) || minimumHeight < 0)
                throw new InvalidOperationException(name + ": minimumHeight must be finite and nonnegative.");
            if (!math.isfinite(maximumHeight) || maximumHeight < minimumHeight)
                throw new InvalidOperationException(name + ": maximumHeight must be finite and at least minimumHeight.");
            // Unity 的 Flags Inspector 使用 -1 表示 Everything；ECS 只保存已定义的形态位。
            InteractionForms forms = (int)allowedForms == -1 ? InteractionForms.All : allowedForms;
            if (forms == 0 || (forms & ~InteractionForms.All) != 0)
                throw new InvalidOperationException(name + ": allowedForms must contain valid player forms.");
            if (controller == null) throw new MissingReferenceException(name + ": controller is required.");
            if (interactionView == null) throw new MissingReferenceException(name + ": interactionView is required.");
            if (triggerRange == null) throw new MissingReferenceException(name + ": triggerRange is required.");
            if (triggerRange.transform != transform && !triggerRange.transform.IsChildOf(transform))
                throw new InvalidOperationException(name + ": triggerRange must belong to this interaction point.");
            if (!Enum.IsDefined(typeof(InteractionMode), interactionMode))
                throw new InvalidOperationException(name + ": interactionMode is invalid.");
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) throw new InvalidOperationException(name + ": Default ECS World is unavailable.");
            physics = world.GetExistingSystemManaged<PhysicsSystem>();
            if (physics == null) throw new InvalidOperationException("Default ECS World requires PhysicsSystem.");
            // 初始化
            interactionView.Initialize();
            var manager = world.EntityManager;
            interaction = manager.CreateEntity(typeof(InteractiveComponent), typeof(InteractionStateComponent), typeof(InteractionRequirementComponent));
            manager.SetComponentData(interaction, new InteractionRequirementComponent
            {
                MinimumHeight = minimumHeight, MaximumHeight = maximumHeight, AllowedForms = forms
            });
            manager.AddBuffer<InteractionOverlapComponent>(interaction);
            manager.SetName(interaction, name);
            manager.SetComponentData(interaction, new InteractionStateComponent { Mode = interactionMode });
            physics.RegisterInteraction(interaction, triggerRange, interactionLayers);
            controller.Initialize(world, interaction);
        }

        public void Shutdown()
        {
            if (controller != null) controller.Shutdown();
            if (world != null && world.IsCreated && interaction != Entity.Null)
            {
                physics.UnregisterInteraction(interaction);
                world.EntityManager.DestroyEntity(interaction);
            }
            interaction = Entity.Null;
            world = null;
            physics = null;
        }
    }
}
