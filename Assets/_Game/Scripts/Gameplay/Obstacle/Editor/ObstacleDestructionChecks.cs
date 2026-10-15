using System;
using Emergence.Gameplay.Environment;
using Emergence.Gameplay.Physics;
using Emergence.Gameplay.Physics.Editor;
using Emergence.Gameplay.Player;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Emergence.Gameplay.Obstacle.Editor
{
    public static class ObstacleDestructionChecks
    {
        [MenuItem("Emergence/Checks/Obstacle Destruction")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            using var world = new World("Obstacle physics checks");
            var manager = world.EntityManager;
            var player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerPositionComponent),
                typeof(PlayerFormComponent), typeof(PlayerInputComponent), typeof(PlayerMotionComponent),
                typeof(PlayerSolidComponent), typeof(PlayerLiquidComponent), typeof(PlayerGasComponent));
            manager.CreateEntity(typeof(EnvironmentComponent));
            manager.SetComponentData(player, new PlayerSolidComponent { MoveVelocityMagnitude = 3, ResponseTime = 0.4f });
            manager.SetComponentData(player, new PlayerLiquidComponent { MoveVelocityMagnitude = 4 });
            manager.SetComponentData(player, new PlayerGasComponent { MoveVelocityMagnitude = 1 });
            var obstacle = manager.CreateEntity(typeof(ObstacleTagComponent), typeof(ObstacleStateComponent),
                typeof(ObstacleDestructibleTagComponent));
            using var scene = new PhysicsChecksScene(world);
            scene.AddBody(manager, player);
            var collider = scene.AddObstacle(obstacle, Vector2.zero, new Vector2(2, 2));
            var system = world.GetOrCreateSystemManaged<ObstacleSystem>();
            int passed = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + label);
                passed++;
            }
            void Reset(PlayerFormKind form, float2 xy, bool pressed = true)
            {
                manager.SetComponentData(player, new PlayerPositionComponent { XY = xy });
                manager.SetComponentData(player, new PlayerFormComponent { Value = form });
                manager.SetComponentData(player, new PlayerInputComponent { InteractPressed = pressed });
                manager.SetComponentData(obstacle, new ObstacleStateComponent());
                scene.Physics.SetObstacleEnabled(obstacle, true);
            }
            bool Destroyed() => manager.GetComponentData<ObstacleStateComponent>(obstacle).IsDestroyed;
            void Tick()
            {
                var position = manager.GetComponentData<PlayerPositionComponent>(player);
                var form = manager.GetComponentData<PlayerFormComponent>(player).Value;
                var movement = scene.Physics.Move(player, position.XY, float2.zero, float2.zero, (int)form);
                position.XY += movement.Displacement;
                manager.SetComponentData(player, position);
                scene.Physics.Update();
                system.Update();
            }

            Reset(PlayerFormKind.Solid, new float2(-1.402f, 0));
            Tick();
            Check(Destroyed() && !collider.enabled, "Solid E destroys a contacted obstacle and immediately disables its collider");
            Reset(PlayerFormKind.Liquid, new float2(-1.402f, 0));
            Tick();
            Check(!Destroyed() && scene.Liquid.enabled && !scene.Solid.enabled && !scene.Gas.enabled,
                "Liquid collider is selected and cannot destroy");
            Reset(PlayerFormKind.Gas, new float2(-1.602f, 0));
            Tick();
            Check(!Destroyed() && scene.Gas.enabled && !scene.Solid.enabled && !scene.Liquid.enabled,
                "Gas collider is selected and cannot destroy");
            Reset(PlayerFormKind.Solid, new float2(-1.402f, 0), false);
            Tick();
            Check(!Destroyed(), "Contact without E cannot destroy");
            Reset(PlayerFormKind.Solid, new float2(-2, 0));
            Tick();
            Check(!Destroyed(), "E without contact cannot destroy");
            manager.RemoveComponent<ObstacleDestructibleTagComponent>(obstacle);
            Reset(PlayerFormKind.Solid, new float2(-1.402f, 0));
            Tick();
            Check(!Destroyed(), "Indestructible contacted obstacle survives E");
            manager.AddComponent<ObstacleDestructibleTagComponent>(obstacle);

            Reset(PlayerFormKind.Solid, new float2(-3, 0), false);
            var movement = scene.Physics.Move(player, new float2(-3, 0), new float2(4, 0), new float2(4, 0), 0);
            float2 stopped = new float2(-3, 0) + movement.Displacement;
            Check(stopped.x <= -1.4f && stopped.x > -1.45f && math.abs(movement.Velocity.x) < 0.001f &&
                !scene.Solid.Distance(collider).isOverlapped,
                "Unity cast blocks movement and clears velocity into obstacle");
            movement = scene.Physics.Move(player, stopped, new float2(-1, 0), new float2(-1, 0), 0);
            Check(math.abs(movement.Displacement.x + 1) < 0.001f,
                "Player can move away from obstacle; displacement=" + movement.Displacement +
                "; body=" + scene.Solid.attachedRigidbody.position + "; transform=" + scene.Solid.transform.position);
            movement = scene.Physics.Move(player, stopped, new float2(1, 1), new float2(1, 1), 0);
            Check(math.abs(movement.Displacement.x) < 0.01f && movement.Displacement.y > 0.9f,
                "Blocked movement retains motion along obstacle");
            scene.Solid.size = new Vector2(1.6f, 0.8f);
            movement = scene.Physics.Move(player, new float2(-3, 0), new float2(4, 0), new float2(4, 0), 0);
            Check(math.abs((-3 + movement.Displacement.x) - (stopped.x - 0.4f)) < 0.001f,
                "Inspector collider dimensions are the sole source of movement bounds");
            scene.Solid.size = new Vector2(0.8f, 0.8f);
            movement = scene.Physics.Move(player, new float2(-1.45f, 0), float2.zero, float2.zero, 2);
            Check(-1.45f + movement.Displacement.x <= -1.59f, "Switching to larger gas collider resolves overlap");

            movement = scene.Physics.Move(player, new float2(-3, 0), new float2(6, 0), new float2(6, 0), 2, 1, 0, 0);
            Check(-3 + movement.Displacement.x < -1.59f, "Gas at obstacle height remains blocked");
            movement = scene.Physics.Move(player, new float2(-3, 0), new float2(6, 0), new float2(6, 0), 2, 1.01f, 0, 0);
            Check(math.abs(movement.Displacement.x - 6) < .001f && collider.enabled,
                "Gas above obstacle crosses without globally disabling collider");
            movement = scene.Physics.Move(player, float2.zero, float2.zero, float2.zero, 2, 2, 0, 0);
            Check(math.length(movement.Displacement) < .001f, "High gas overlapping ground projection is not pushed out");
            movement = scene.Physics.Move(player, float2.zero, float2.zero, float2.zero, 2, 2, -3, -10);
            Check(movement.Height > 1 && movement.Height < 1.01f && movement.VerticalVelocity == 0 &&
                math.length(movement.Displacement) < .001f, "Descent stops above obstacle without horizontal ejection");
            float heldHeight = movement.Height;
            movement = scene.Physics.Move(player, float2.zero, new float2(3, 0), new float2(3, 0), 1, heldHeight, -.2f, -2);
            Check(math.abs(movement.Displacement.x - 3) < .001f && math.abs(movement.Height - heldHeight) < .001f,
                "Changing to liquid above obstacle permits moving away");
            movement = scene.Physics.Move(player, new float2(3, 0), float2.zero, float2.zero, 1, heldHeight, -.2f, -2);
            Check(movement.Height < 1 && movement.VerticalVelocity == -2, "Descent resumes after leaving ground projection");
            movement = scene.Physics.Move(player, new float2(-3, 0), new float2(6, 0), new float2(6, 0), 2, 0, 2, 1);
            Check(math.abs(movement.Displacement.x - 6) < .001f && movement.Height == 2,
                "Same-step ascent uses resolved height for movement");
            Reset(PlayerFormKind.Solid, float2.zero);
            manager.SetComponentData(player, new PlayerPositionComponent { Height = 2 });
            movement = scene.Physics.Move(player, float2.zero, float2.zero, float2.zero, 0);
            scene.Physics.Update();
            system.Update();
            Check(manager.GetBuffer<PhysicsContactComponent>(player).Length == 0 && !Destroyed(),
                "High solid receives no false obstacle contact and cannot destroy it");
            scene.Physics.SetObstacleEnabled(obstacle, false);
            movement = scene.Physics.Move(player, new float2(-3, 0), new float2(4, 0), new float2(4, 0), 0);
            Check(math.abs(movement.Displacement.x - 4) < 0.001f, "Destroyed obstacle no longer blocks movement");
            Check(!manager.GetComponentData<PlayerInputComponent>(player).InteractPressed, "Shared interaction request is consumed once");
            return passed + " physics and obstacle checks passed.";
        }
    }
}
