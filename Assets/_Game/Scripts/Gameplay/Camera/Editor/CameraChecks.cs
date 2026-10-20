using System;
using Emergence.Gameplay.Environment;
using Emergence.Gameplay.Physics.Editor;
using Emergence.Gameplay.Player;
using Emergence.Gameplay.Respawn;
using Emergence.WorldView;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Emergence.Gameplay.Camera.Editor
{
    public static class CameraChecks
    {
        [MenuItem("Emergence/Checks/Camera")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            using var world = new World("Camera checks");
            using var scene = new PhysicsChecksScene(world);
            var root = new GameObject("Camera checks");
            // 固定像素尺寸，避免编辑器窗口的非整数像素影响视口坐标断言。
            var renderTexture = new RenderTexture(640, 360, 0);
            try
            {
                var camera = root.AddComponent<UnityEngine.Camera>();
                camera.targetTexture = renderTexture;
                camera.orthographic = true;
                camera.orthographicSize = 5;
                root.transform.position = new Vector3(0, 0, -10);
                var view = root.AddComponent<CameraView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("targetCamera").objectReferenceValue = camera;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                view.Initialize();
                int passed = 0;
                void Check(bool condition, string message)
                {
                    if (!condition) throw new InvalidOperationException("FAIL: " + message);
                    passed++;
                }
                view.Snap(new Vector2(7, 12));
                Check(Vector3.Distance(root.transform.position, new Vector3(7, 12, -10)) < .001f, "Spawn snaps immediately");
                view.Refresh(new Vector2(8, 12), 1f / 60);
                Check(root.transform.position.x > 7 && root.transform.position.x < 8, "Normal movement is smoothed");
                foreach (float aspect in new[] { .6f, 1f, 16f / 9 })
                {
                    camera.aspect = aspect;
                    foreach (var target in new[] { new Vector2(100, 100), new Vector2(-100, -100), new Vector2(100, -100) })
                    {
                        view.Refresh(target, 1f / 60);
                        var viewport = camera.WorldToViewportPoint(new Vector3(target.x, target.y));
                        Check(viewport.x >= .1999f && viewport.x <= .8001f && viewport.y >= .1999f && viewport.y <= .8001f,
                            "Fast movement remains within viewport margins");
                    }
                }
                Check(root.transform.position.z == -10, "Follow preserves camera depth");

                var manager = world.EntityManager;
                var environment = manager.CreateEntity(typeof(EnvironmentComponent));
                manager.SetComponentData(environment, new EnvironmentComponent
                {
                    WindVelocity = new float3(0, 0, 4), Gravity = 10, MaximumHeight = float.MaxValue
                });
                var player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerPositionComponent), typeof(PlayerMotionComponent),
                    typeof(PlayerInputComponent), typeof(PlayerFormComponent), typeof(PlayerSolidComponent), typeof(PlayerLiquidComponent), typeof(PlayerGasComponent));
                manager.SetComponentData(player, new PlayerGasComponent { MoveVelocityMagnitude = 1 });
                PlayerSystem.SetForm(manager, player, PlayerFormKind.Gas);
                scene.AddBody(manager, player);
                var respawn = world.GetOrCreateSystemManaged<RespawnSystem>();
                var system = world.GetOrCreateSystemManaged<PlayerSystem>();
                var controller = root.AddComponent<CameraController>();
                controller.Initialize(world, player, view);
                for (int i = 0; i < 600; i++)
                {
                    world.SetTime(new TimeData((i + 1) / 60.0, 1f / 60));
                    system.Update();
                    controller.SendMessage("LateUpdate");
                    var position = manager.GetComponentData<PlayerPositionComponent>(player);
                    var viewport = camera.WorldToViewportPoint(new Vector3(position.XY.x, position.XY.y + position.Height));
                    Check(viewport.y >= .1999f && viewport.y <= .8001f, "Gas height stays inside viewport");
                }
                Check(manager.GetComponentData<PlayerPositionComponent>(player).Height > 39, "Gas rises beyond the initial screen");
                manager.AddComponent<DeadTagComponent>(player);
                var spawn = manager.CreateEntity(typeof(RespawnPointComponent), typeof(RespawnEnabledTagComponent));
                manager.SetComponentData(spawn, new RespawnPointComponent { XY = new float2(-8, 3), Height = 2 });
                respawn.Update();
                Check(Vector3.Distance(root.transform.position, new Vector3(-8, 5, -10)) < .001f,
                    "Respawn event immediately snaps camera to XY plus height");
                controller.Shutdown();
                Check(!controller.enabled, "Shutdown stops controller access");
                manager.SetComponentData(player, new PlayerPositionComponent { XY = new float2(0, .5f), Height = 2 });
                controller.Initialize(world, player, view, 2);
                Check(math.abs(root.transform.position.y - 2) < .001f, "Camera snaps to capped body display position");
                manager.SetComponentData(player, new PlayerPositionComponent { XY = new float2(0, 1), Height = 2 });
                controller.SendMessage("LateUpdate");
                Check(math.abs(root.transform.position.y - 2) < .001f, "Camera remains stable while shadow moves at display ceiling");
                controller.Shutdown();
                controller.Initialize(world, player, view, float.MaxValue, 2);
                Check(math.abs(root.transform.position.y - 5) < .001f, "Camera applies the player's height display scale");
                controller.Shutdown();
                controller.Initialize(world, player, view, 2, 2);
                Check(math.abs(root.transform.position.y - 2) < .001f, "Scaled camera target still respects the display ceiling");
                controller.Shutdown();
                controller.Initialize(world, player, view, float.MaxValue, 2, .5f);
                Check(math.abs(root.transform.position.y - 3) < .001f, "Half height following frames ground and scaled body together");
                controller.Shutdown();
                manager.SetComponentData(player, new PlayerPositionComponent { XY = new float2(0, 1) });
                controller.Initialize(world, player, view, float.MaxValue, 2, .5f);
                Check(math.abs(root.transform.position.y - 1) < .001f, "Half height following retains the ground target at zero height");
                controller.Shutdown();
                return "Camera checks passed: " + passed + " (including 600 gas movement steps).";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
