using System;
using Emergence.WorldView;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;

namespace Emergence.Gameplay.NPC
{
    public sealed class NPCController : MonoBehaviour
    {
        [SerializeField] private UnityEvent onAttack = new UnityEvent();
        [SerializeField, Min(0)] private float resistanceBarDuration = 1.5f;
        private World world;
        private Entity npc;
        private NPCView view;
        private float shownResistance;
        private float showUntil;

        public NPCResistanceComponent Resistance => world.EntityManager.GetComponentData<NPCResistanceComponent>(npc);
        public NPCBehaviorComponent Behavior => world.EntityManager.GetComponentData<NPCBehaviorComponent>(npc);

        public void Initialize(World targetWorld, Entity entity, NPCView npcView)
        {
            // 校验
            if (targetWorld == null || !targetWorld.IsCreated || !targetWorld.EntityManager.Exists(entity))
                throw new ArgumentException(name + ": NPCController requires a live NPC entity.");
            if (npcView == null) throw new MissingReferenceException(name + ": NPCView is required.");
            if (!float.IsFinite(resistanceBarDuration) || resistanceBarDuration < 0)
                throw new InvalidOperationException(name + ": resistanceBarDuration must be finite and nonnegative.");
            var manager = targetWorld.EntityManager;
            if (!manager.HasComponent<NPCPositionComponent>(entity) || !manager.HasComponent<NPCResistanceComponent>(entity) ||
                !manager.HasComponent<NPCBehaviorComponent>(entity) || !manager.HasComponent<NPCAttackComponent>(entity))
                throw new ArgumentException(name + ": NPC entity is missing required components.");
            bool hasAttack = false;
            if (onAttack != null)
            {
                for (int i = 0; i < onAttack.GetPersistentEventCount(); i++)
                {
                    if (onAttack.GetPersistentListenerState(i) == UnityEventCallState.Off) continue;
                    if (onAttack.GetPersistentTarget(i) == null || string.IsNullOrEmpty(onAttack.GetPersistentMethodName(i)))
                        throw new MissingReferenceException($"{name}: onAttack listener {i} requires a target and method.");
                    hasAttack = true;
                }
            }

            // 初始化
            manager.SetComponentData(entity, new NPCAttackComponent { HasAttack = hasAttack });
            npcView.Initialize();
            world = targetWorld;
            npc = entity;
            view = npcView;
            shownResistance = Resistance.Value;
            showUntil = float.NegativeInfinity;
            view.Refresh(manager.GetComponentData<NPCPositionComponent>(npc), Resistance, Behavior, false);
            enabled = true;
        }

        public void RequestAttack() => NPCSystem.RequestAttack(world.EntityManager, npc);

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            var resistance = Resistance;
            if (resistance.Value < shownResistance) showUntil = Time.time + resistanceBarDuration;
            shownResistance = resistance.Value;
            view.Refresh(world.EntityManager.GetComponentData<NPCPositionComponent>(npc), resistance, Behavior, Time.time < showUntil);
            uint count = NPCSystem.TakeAttacks(world.EntityManager, npc);
            for (uint i = 0; i < count; i++) onAttack.Invoke();
        }

        public void Shutdown()
        {
            enabled = false;
            if (view != null) view.Shutdown();
            view = null;
            world = null;
            npc = Entity.Null;
        }
    }
}
