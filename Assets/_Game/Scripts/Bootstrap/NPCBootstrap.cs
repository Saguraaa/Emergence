using System;
using Emergence.Gameplay.NPC;
using Emergence.Gameplay.Physics;
using Emergence.WorldView;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class NPCBootstrap : MonoBehaviour
    {
        [SerializeField] private NPCController controller;
        [SerializeField] private NPCView npcView;
        [SerializeField] private Rigidbody2D physicsBody;
        [SerializeField] private Collider2D colliderBounds;
        [SerializeField] private LayerMask collisionLayers;
        [SerializeField, Min(.01f)] private float moveVelocityMagnitude = 2;
        [SerializeField, Min(.001f)] private float arrivalDistance = .1f;
        [SerializeField, Min(.01f)] private float blockedDuration = 2;
        [SerializeField, Min(.01f)] private float initialResistance = 100;
        [SerializeField, Range(.01f, .99f)] private float fleeThresholdRatio = .8f;
        [SerializeField] private NPCWaypointConfig[] waypoints = new NPCWaypointConfig[0];
        private World world;
        private Entity npc;
        private PhysicsSystem physics;

        public void Initialize()
        {
            // 校验
            if (controller == null) throw new MissingReferenceException(name + ": controller is required.");
            if (npcView == null) throw new MissingReferenceException(name + ": npcView is required.");
            if (physicsBody == null) throw new MissingReferenceException(name + ": physicsBody is required.");
            if (colliderBounds == null) throw new MissingReferenceException(name + ": colliderBounds is required.");
            if (physicsBody.transform != npcView.transform || controller.transform != npcView.transform)
                throw new InvalidOperationException(name + ": controller, view and Rigidbody2D must share the NPC ground root.");
            if (!math.isfinite(moveVelocityMagnitude) || moveVelocityMagnitude <= 0 ||
                !math.isfinite(arrivalDistance) || arrivalDistance <= 0 || !math.isfinite(blockedDuration) || blockedDuration <= 0 ||
                !math.isfinite(initialResistance) || initialResistance <= 0 ||
                !math.isfinite(fleeThresholdRatio) || fleeThresholdRatio <= 0 || fleeThresholdRatio >= 1)
                throw new InvalidOperationException(name + ": NPC parameters must be finite and positive; fleeThresholdRatio must be between 0 and 1.");
            if (waypoints == null || waypoints.Length == 0)
                throw new InvalidOperationException(name + ": configure at least one escape waypoint on the scene instance.");
            for (int i = 0; i < waypoints.Length; i++)
            {
                var waypoint = waypoints[i];
                if (waypoint.Point == null) throw new MissingReferenceException($"{name}: waypoints[{i}].Point is required.");
                if (waypoint.Point.IsChildOf(npcView.transform))
                    throw new InvalidOperationException($"{name}: waypoints[{i}] must not move with the NPC.");
                if (!math.all(math.isfinite((float3)waypoint.Point.position)) ||
                    !math.isfinite(waypoint.WaitDuration) || waypoint.WaitDuration < 0)
                    throw new InvalidOperationException($"{name}: waypoints[{i}] position and wait duration are invalid.");
            }
            Vector3 spawn = npcView.transform.position;
            if (!math.all(math.isfinite((float3)spawn))) throw new InvalidOperationException(name + ": NPC spawn must be finite.");
            var resolvedWorld = World.DefaultGameObjectInjectionWorld;
            if (resolvedWorld == null || !resolvedWorld.IsCreated) throw new InvalidOperationException(name + ": Default ECS World is unavailable.");
            var resolvedPhysics = resolvedWorld.GetExistingSystemManaged<PhysicsSystem>();
            if (resolvedPhysics == null) throw new InvalidOperationException(name + ": PhysicsSystem is required.");
            var manager = resolvedWorld.EntityManager;

            // 初始化
            world = resolvedWorld;
            physics = resolvedPhysics;
            npc = manager.CreateEntity(typeof(NPCTagComponent), typeof(NPCPositionComponent), typeof(NPCMotionComponent),
                typeof(NPCResistanceComponent), typeof(NPCBehaviorComponent), typeof(NPCAttackComponent));
            manager.SetName(npc, name);
            manager.SetComponentData(npc, new NPCPositionComponent { XY = new float2(spawn.x, spawn.y) });
            manager.SetComponentData(npc, new NPCMotionComponent
            {
                MoveVelocityMagnitude = moveVelocityMagnitude, ArrivalDistance = arrivalDistance, BlockedDuration = blockedDuration
            });
            manager.SetComponentData(npc, new NPCResistanceComponent { InitialValue = initialResistance, Value = initialResistance });
            manager.SetComponentData(npc, new NPCBehaviorComponent { FleeThresholdRatio = fleeThresholdRatio });
            var route = manager.AddBuffer<NPCWaypointComponent>(npc);
            foreach (var waypoint in waypoints)
            {
                Vector3 point = waypoint.Point.position;
                route.Add(new NPCWaypointComponent { XY = new float2(point.x, point.y), WaitDuration = waypoint.WaitDuration });
            }
            manager.AddBuffer<PhysicsContactComponent>(npc);
            physics.RegisterBody(npc, physicsBody, new[] { colliderBounds }, collisionLayers);
            controller.Initialize(world, npc, npcView);
        }

        public void Shutdown()
        {
            if (controller != null) controller.Shutdown();
            if (world != null && world.IsCreated && npc != Entity.Null)
            {
                physics.UnregisterBody(npc);
                world.EntityManager.DestroyEntity(npc);
            }
            npc = Entity.Null;
            world = null;
            physics = null;
        }
    }
}
