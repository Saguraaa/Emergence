using System;
using System.Reflection;
using Emergence.Gameplay.InteractCallback;
using Emergence.Gameplay.Interaction;
using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Physics.Editor;
using Emergence.Gameplay.Player;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using InteractionMode = Emergence.Gameplay.Interaction.InteractionMode;

namespace Emergence.Gameplay.Respawn.Editor
{
    public static class RespawnChecks
    {
        [MenuItem("Emergence/Checks/Respawn")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            using var world = new World("Respawn checks");
            var manager = world.EntityManager;
            var player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerInputComponent),
                typeof(PlayerPositionComponent), typeof(PlayerMotionComponent), typeof(PlayerFormComponent),
                typeof(DeadTagComponent));
            manager.SetComponentEnabled<DeadTagComponent>(player, false);
            var point = manager.CreateEntity(typeof(RespawnPointComponent), typeof(RespawnEnabledTagComponent));
            manager.SetComponentData(point, new RespawnPointComponent { XY = new float2(4, 0), Height = 2 });
            var zone = manager.CreateEntity(typeof(InteractiveComponent), typeof(InteractionStateComponent));
            manager.AddComponentData(zone, new InteractionRequirementComponent
            {
                MaximumHeight = float.MaxValue, AllowedForms = InteractionForms.All
            });
            manager.SetComponentData(zone, new InteractionStateComponent { Mode = InteractionMode.EnterRange });
            manager.AddBuffer<InteractionOverlapComponent>(zone);
            using var scene = new PhysicsChecksScene(world);
            scene.AddBody(manager, player);
            var obstacle = manager.CreateEntity(typeof(ObstacleTagComponent), typeof(ObstacleStateComponent));
            scene.AddObstacle(obstacle, new Vector2(4, 0), Vector2.one, 1);
            var root = new GameObject("Respawn checks death range");
            SceneManager.MoveGameObjectToScene(root, scene.Solid.gameObject.scene);
            var range = root.AddComponent<CircleCollider2D>();
            range.isTrigger = true;
            range.radius = 1;
            scene.Physics.RegisterInteraction(zone, range, 1 << 31);
            var interaction = world.GetOrCreateSystemManaged<InteractionSystem>();
            var respawn = world.GetOrCreateSystemManaged<RespawnSystem>();
            int notifications = 0;
            bool notificationStateValid = true;
            void OnRespawned(Entity entity)
            {
                notifications++;
                notificationStateValid &= entity == player && !manager.IsComponentEnabled<DeadTagComponent>(entity) &&
                    math.all(manager.GetComponentData<PlayerPositionComponent>(entity).XY == new float2(4, 0)) &&
                    math.all(manager.GetComponentData<PlayerMotionComponent>(entity).Velocity == float3.zero);
            }
            respawn.Respawned += OnRespawned;
            var controller = root.AddComponent<InteractionController>();
            controller.Initialize(world, zone);
            var onActivated = (UnityEvent)typeof(InteractionController).GetField("onActivated", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller);
            var death = root.AddComponent<PlayerDeathAction>();
            onActivated.AddListener(death.Die);
            int passed = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + label);
                passed++;
            }
            void Enter(PlayerFormKind form)
            {
                scene.Physics.Move(player, new float2(4, 0), float2.zero, float2.zero, (int)form);
                scene.Physics.Update();
                interaction.Update();
                manager.SetComponentData(player, new PlayerFormComponent { Value = form });
                manager.SetComponentData(player, new PlayerInputComponent { Move = new float2(1, 0) });
                manager.SetComponentData(player, new PlayerMotionComponent { Velocity = new float3(3, 4, 5) });
                scene.Physics.Move(player, float2.zero, float2.zero, float2.zero, (int)form);
                scene.Physics.Update();
                interaction.Update();
                var previousWorld = World.DefaultGameObjectInjectionWorld;
                World.DefaultGameObjectInjectionWorld = world;
                try
                {
                    controller.SendMessage("LateUpdate");
                }
                finally
                {
                    World.DefaultGameObjectInjectionWorld = previousWorld;
                }
            }
            RespawnSystem.SetEnabled(manager, point, false);
            Enter(PlayerFormKind.Solid);
            Check(manager.IsComponentEnabled<DeadTagComponent>(player), "Entering range invokes death callback without E requirement");
            respawn.Update();
            Check(manager.IsComponentEnabled<DeadTagComponent>(player) && notifications == 0,
                "Disabled point does not respawn");
            RespawnSystem.SetEnabled(manager, point, true);
            foreach (var form in new[] { PlayerFormKind.Solid, PlayerFormKind.Liquid, PlayerFormKind.Gas })
            {
                Enter(form);
                respawn.Update();
                var position = manager.GetComponentData<PlayerPositionComponent>(player);
                Check(!manager.IsComponentEnabled<DeadTagComponent>(player) && math.all(position.XY == new float2(4, 0)) && position.Height == 2,
                    form + " returns to enabled point immediately");
                Check(math.all(manager.GetComponentData<PlayerMotionComponent>(player).Velocity == float3.zero) &&
                      !manager.GetComponentData<PlayerInputComponent>(player).InteractPressed, form + " resets motion and input");
                Check(manager.GetComponentData<PlayerFormComponent>(player).Value == form &&
                      scene.Solid.attachedRigidbody.position == new Vector2(4, 0), form + " preserves form and synchronizes physics");
            }
            respawn.Update();
            Check(notifications == 3 && notificationStateValid,
                "Each completed respawn signals its player after state changes; alive updates do not repeat");
            respawn.Respawned -= OnRespawned;
            Enter(PlayerFormKind.Solid);
            respawn.Update();
            Check(notifications == 3, "Unsubscribed listener receives no signal");
            scene.Physics.Update();
            Check(manager.GetBuffer<InteractionOverlapComponent>(zone).Length == 0, "Respawn removes player from death range on next query");
            controller.Shutdown();
            scene.Physics.UnregisterInteraction(zone);
            return passed + " respawn checks passed.";
        }
    }
}
