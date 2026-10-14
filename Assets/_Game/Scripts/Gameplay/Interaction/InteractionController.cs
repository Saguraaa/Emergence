using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;

namespace Emergence.Gameplay.Interaction
{
    public sealed class InteractionController : MonoBehaviour
    {
        [SerializeField] private UnityEvent onActivated = new UnityEvent();
        private World world;
        private Entity interaction;

        public void Initialize(World targetWorld, Entity entity)
        {
            // 校验
            enabled = false;
            if (targetWorld == null || !targetWorld.IsCreated || !targetWorld.EntityManager.Exists(entity) ||
                !targetWorld.EntityManager.HasComponent<InteractionStateComponent>(entity))
                throw new ArgumentException(name + ": InteractionController requires a live interaction entity.");
            if (onActivated == null) throw new MissingReferenceException(name + ": onActivated is required.");
            // 初始化
            world = targetWorld;
            interaction = entity;
            enabled = true;
        }

        private void LateUpdate()
        {
            uint count = InteractionSystem.TakeActivations(world.EntityManager, interaction);
            for (uint i = 0; i < count; i++) onActivated.Invoke();
        }

        public void Shutdown()
        {
            enabled = false;
            world = null;
            interaction = Entity.Null;
        }
    }
}
