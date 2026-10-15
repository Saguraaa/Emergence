using System;
using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Physics.Editor;
using Emergence.Gameplay.Player;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emergence.Gameplay.Interaction.Editor
{
    public static class InteractionChecks
    {
        [MenuItem("Emergence/Checks/Interaction")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            using var world = new World("Interaction checks");
            var manager = world.EntityManager;
            var player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerInputComponent), typeof(PlayerFormComponent));
            var point = manager.CreateEntity(typeof(InteractiveComponent), typeof(InteractionStateComponent));
            manager.AddComponentData(point, new InteractionRequirementComponent { AllowedForms = InteractionForms.All });
            manager.AddBuffer<InteractionOverlapComponent>(point);
            using var scene = new PhysicsChecksScene(world);
            scene.AddBody(manager, player);
            var root = new GameObject("Interaction checks range");
            SceneManager.MoveGameObjectToScene(root, scene.Solid.gameObject.scene);
            var range = root.AddComponent<CircleCollider2D>();
            range.isTrigger = true;
            range.radius = 1;
            scene.Physics.RegisterInteraction(point, range, 1 << 31);
            var interaction = world.GetOrCreateSystemManaged<InteractionSystem>();
            var obstacleSystem = world.GetOrCreateSystemManaged<ObstacleSystem>();
            int passed = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + label);
                passed++;
            }
            void Tick(float x, bool pressed, PlayerFormKind form = PlayerFormKind.Solid, float height = 0)
            {
                manager.SetComponentData(player, new PlayerPositionComponent { XY = new float2(x, 0), Height = height });
                manager.SetComponentData(player, new PlayerInputComponent { InteractPressed = pressed });
                manager.SetComponentData(player, new PlayerFormComponent { Value = form });
                scene.Physics.Move(player, new float2(x, 0), float2.zero, float2.zero, (int)form);
                scene.Physics.Update();
                interaction.Update();
                obstacleSystem.Update();
            }
            uint Take() => InteractionSystem.TakeActivations(manager, point);

            Tick(3, true);
            Check(Take() == 0, "E outside range does not activate");
            Tick(0, false);
            Check(Take() == 0, "Entering manual range does not activate");
            Tick(0, true);
            Check(Take() == 1 && Take() == 0, "E activates once and output is consumed once");
            Check(!manager.GetComponentData<PlayerInputComponent>(player).InteractPressed, "Shared request clears after both readers");
            Tick(0, false);
            Check(Take() == 0, "Staying in manual range does not repeat");
            Tick(0, true, PlayerFormKind.Liquid);
            Check(Take() == 1, "Liquid can interact");
            Tick(0, true, PlayerFormKind.Gas);
            Check(Take() == 1, "Gas can interact");
            manager.SetComponentData(point, new InteractionStateComponent { Mode = InteractionMode.EnterRange });
            Tick(3, false);
            Tick(0, false);
            Check(Take() == 1, "Automatic mode activates on entry without E");
            Tick(0, true);
            Tick(0, false);
            Check(Take() == 0, "Staying in automatic range and pressing E do not repeat");
            Tick(3, false);
            Tick(0, false);
            Check(Take() == 1, "Leaving and reentering activates again");
            Tick(3, false);
            Tick(0, false);
            Tick(3, false);
            Tick(0, false);
            Check(Take() == 2, "Multiple fixed steps preserve pending outputs");
            range.radius = 4;
            Tick(3, false);
            Check(manager.GetComponentData<InteractionStateComponent>(point).IsOccupied, "Inspector radius controls range");

            manager.SetComponentData(point, new InteractionRequirementComponent
            {
                MinimumHeight = 3, AllowedForms = InteractionForms.Gas
            });
            Tick(0, true, PlayerFormKind.Gas, 2.99f);
            Check(Take() == 0 && !manager.GetComponentData<InteractionStateComponent>(point).IsOccupied,
                "Gas below minimum height cannot occupy or activate");
            Tick(0, false, PlayerFormKind.Gas, 3);
            Check(Take() == 1, "Rising in place to minimum height enters automatic range");
            Tick(0, true, PlayerFormKind.Gas, 8);
            Check(Take() == 0, "Minimum height has no upper limit and staying does not repeat");
            Tick(0, false, PlayerFormKind.Liquid, 8);
            Check(Take() == 0 && !manager.GetComponentData<InteractionStateComponent>(point).IsOccupied,
                "High liquid cannot occupy gas-only point");
            Tick(0, false, PlayerFormKind.Gas, 8);
            Check(Take() == 1, "Changing back to allowed form enters range again");
            manager.AddComponent<DeadTagComponent>(player);
            Tick(0, true, PlayerFormKind.Gas, 8);
            Check(Take() == 0 && !manager.GetComponentData<InteractionStateComponent>(point).IsOccupied,
                "Dead player cannot occupy interaction point");
            manager.SetComponentEnabled<DeadTagComponent>(player, false);
            manager.SetComponentData(point, new InteractionStateComponent());
            Tick(0, true, PlayerFormKind.Gas, 2);
            Check(Take() == 0, "Manual interaction rejects insufficient height");
            Tick(0, true, PlayerFormKind.Solid, 3);
            Check(Take() == 0, "Manual interaction rejects disallowed form");
            Tick(0, true, PlayerFormKind.Gas, 3);
            Check(Take() == 1, "Manual interaction accepts gas at minimum height");
            manager.SetComponentData(point, new InteractionRequirementComponent { AllowedForms = InteractionForms.All });

            var obstacle = manager.CreateEntity(typeof(ObstacleTagComponent), typeof(ObstacleStateComponent),
                typeof(ObstacleDestructibleTagComponent));
            var blocking = scene.AddObstacle(obstacle, new Vector2(1.402f, 0), new Vector2(2, 2));
            manager.SetComponentData(point, new InteractionStateComponent());
            Tick(0, true);
            Check(Take() == 1 && manager.GetComponentData<ObstacleStateComponent>(obstacle).IsDestroyed && !blocking.enabled,
                "Same E request reaches interaction and obstacle destruction");
            scene.Physics.UnregisterInteraction(point);
            Check(!range.enabled, "Unregister disables trigger");
            scene.Physics.RegisterInteraction(point, range, 1 << 31);
            UnityEngine.Object.DestroyImmediate(range);
            scene.Physics.UnregisterInteraction(point);
            var replacement = root.AddComponent<CircleCollider2D>();
            replacement.isTrigger = true;
            scene.Physics.RegisterInteraction(point, replacement, 1 << 31);
            scene.Physics.UnregisterInteraction(point);
            Check(!replacement.enabled, "Destroyed trigger unregisters fully and permits registration again");
            return passed + " interaction checks passed.";
        }
    }
}
