using System;
using System.Text;
using Emergence.Gameplay.Environment;
using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Physics.Editor;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Emergence.Gameplay.Player.Editor
{
    public static class PlayerMovementChecks
    {
        [MenuItem("Emergence/Checks/Player Movement")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            Debug.Log(Run());
            EditorApplication.Exit(0);
        }

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            var report = new StringBuilder();
            int passed = 0;

            using (var world = new World("Player movement checks"))
            {
                var manager = world.EntityManager;
                var environment = manager.CreateEntity(typeof(EnvironmentComponent));
                var player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerInputComponent),
                    typeof(PlayerPositionComponent), typeof(PlayerMotionComponent), typeof(PlayerFormComponent), typeof(PlayerSolidComponent),
                    typeof(PlayerLiquidComponent), typeof(PlayerGasComponent));
                manager.SetComponentData(player, new PlayerSolidComponent { MoveVelocityMagnitude = 3, ResponseTime = .4f });
                manager.SetComponentData(player, new PlayerLiquidComponent { MoveVelocityMagnitude = 4 });
                manager.SetComponentData(player, new PlayerGasComponent { MoveVelocityMagnitude = 1 });
                using var physicsScene = new PhysicsChecksScene(world);
                physicsScene.AddBody(manager, player);
                var system = world.GetOrCreateSystemManaged<PlayerSystem>();
                double time = 0;
                void Reset(PlayerFormKind form, float height = 0)
                {
                    manager.SetComponentData(player, new PlayerPositionComponent { Height = height });
                    manager.SetComponentData(player, new PlayerMotionComponent());
                    PlayerSystem.SetForm(manager, player, form);
                    manager.SetComponentData(player, new PlayerInputComponent());
                    manager.SetComponentData(environment, new EnvironmentComponent
                    {
                        WindVelocity = new float3(0, 0, 1), Gravity = 10
                    });
                }
                void Tick(int count, float2 direction, float dt = 1f / 60)
                {
                    manager.SetComponentData(player, new PlayerInputComponent { Move = direction });
                    for (int i = 0; i < count; i++)
                    {
                        time += dt;
                        world.SetTime(new TimeData(time, dt));
                        system.Update();
                        physicsScene.Physics.Update();
                    }
                }
                PlayerPositionComponent Position() => manager.GetComponentData<PlayerPositionComponent>(player);
                float3 Velocity() => manager.GetComponentData<PlayerMotionComponent>(player).Velocity;
                void Check(bool condition, string name)
                {
                    if (!condition) throw new InvalidOperationException("FAIL: " + name + "; velocity=" + Velocity() + "; height=" + Position().Height);
                    report.AppendLine("PASS: " + name);
                    passed++;
                }

                Reset(PlayerFormKind.Liquid);
                Tick(60, new float2(0, 1));
                Check(math.distance(Position().XY, new float2(0, 4)) < .001f && Position().Height == 0, "Map Y input does not change height");
                Reset(PlayerFormKind.Liquid);
                Tick(60, new float2(1, 1));
                Check(math.abs(math.length(Position().XY) - 4) < .001f, "Diagonal movement is normalized");

                Reset(PlayerFormKind.Solid);
                Tick(12, new float2(1, 0));
                Check(math.abs(Velocity().x - 1.5f) < .001f, "Solid half response reaches half target magnitude");
                Tick(12, new float2(1, 0));
                Check(math.abs(Velocity().x - 3) < .001f, "Solid full response reaches target magnitude");
                Tick(12, float2.zero);
                Check(math.abs(Velocity().x - 1.5f) < .001f, "Solid decelerates on release");
                Tick(12, float2.zero);
                Check(math.length(Velocity().xy) < .001f, "Solid stops after response time");
                Tick(24, new float2(1, 0));
                Tick(24, new float2(-1, 0));
                Check(math.abs(Velocity().x) < .001f, "Solid reversal first response reaches zero");
                Tick(24, new float2(-1, 0));
                Check(math.abs(Velocity().x + 3) < .001f, "Solid reversal second response reaches opposite target");

                Reset(PlayerFormKind.Gas);
                Tick(60, float2.zero);
                Check(math.length(Position().XY) == 0 && math.abs(Position().Height - 1) < .001f, "Default gas rises without map movement");
                manager.SetComponentData(environment, new EnvironmentComponent { WindVelocity = new float3(2, -1, .5f), Gravity = 10 });
                Tick(60, new float2(1, 0));
                Check(math.distance(Position().XY, new float2(3, -1)) < .001f && math.abs(Position().Height - 1.5f) < .001f, "Gas input combines with environment wind");

                Reset(PlayerFormKind.Gas);
                Tick(60, float2.zero);
                PlayerSystem.SetForm(manager, player, PlayerFormKind.Liquid);
                Tick(1, float2.zero);
                Check(Position().Height > 0 && Position().Height < 1 && Velocity().z < 0, "Gas to liquid preserves height and falls");
                Tick(60, float2.zero);
                Check(Position().Height == 0 && Velocity().z == 0, "Landing clamps height and vertical motion");
                Reset(PlayerFormKind.Gas, 2);
                Tick(1, float2.zero);
                PlayerSystem.SetForm(manager, player, PlayerFormKind.Solid);
                Tick(1, float2.zero);
                Check(Position().Height > 0 && Velocity().z < 0, "Gas to solid falls");
                Reset(PlayerFormKind.Liquid);
                Tick(1, new float2(1, 0));
                PlayerSystem.SetForm(manager, player, PlayerFormKind.Solid);
                Tick(1, float2.zero);
                Check(math.length(Velocity().xy) <= 3 && Velocity().x > 0, "Solid limits inherited motion without resetting it");

                Reset(PlayerFormKind.Liquid, 3);
                Tick(6, float2.zero, 1f / 30);
                float height30 = Position().Height;
                Reset(PlayerFormKind.Liquid, 3);
                Tick(24, float2.zero, 1f / 120);
                Check(math.abs(height30 - Position().Height) < .001f, "Gravity displacement is consistent across timesteps");
                Reset(PlayerFormKind.Gas);
                Tick(1, new float2(1, 0));
                float gasMagnitude = math.length(Velocity().xy);
                Reset(PlayerFormKind.Solid);
                Tick(24, new float2(1, 0));
                float solidMagnitude = math.length(Velocity().xy);
                Reset(PlayerFormKind.Liquid);
                Tick(1, new float2(1, 0));
                Check(gasMagnitude < solidMagnitude && solidMagnitude < math.length(Velocity().xy), "Gas < Solid < Liquid control magnitude");
                using (var query = manager.CreateEntityQuery(typeof(PlayerTagComponent)))
                    Check(query.CalculateEntityCount() == 1, "Form changes reuse one entity");

                var obstacle = manager.CreateEntity(typeof(ObstacleTagComponent), typeof(ObstacleStateComponent));
                physicsScene.AddObstacle(obstacle, Vector2.zero, new Vector2(2, 2), 1);
                Reset(PlayerFormKind.Gas, 2);
                Tick(1, float2.zero);
                Check(math.length(Position().XY) < .001f && Position().Height > 2,
                    "Gas above obstacle projection rises without horizontal correction");
                PlayerSystem.SetForm(manager, player, PlayerFormKind.Liquid);
                Tick(60, float2.zero);
                Check(Position().Height > 1 && Position().Height < 1.01f && Velocity().z == 0 &&
                    math.length(Position().XY) < .001f, "Converted liquid descent is constrained above obstacle");
                Tick(60, new float2(1, 0));
                Check(Position().XY.x > 3.9f && Position().Height == 0 && Velocity().z == 0,
                    "Liquid moves off obstacle and resumes falling to ground");
                Reset(PlayerFormKind.Gas, 2);
                manager.SetComponentData(environment, new EnvironmentComponent { WindVelocity = new float3(0, 0, -4), Gravity = 10 });
                Tick(60, float2.zero);
                Check(Position().Height > 1 && Position().Height < 1.01f && Velocity().z == 0,
                    "Downward wind uses the same height constraint");
                physicsScene.Physics.SetObstacleEnabled(obstacle, false);

                // 三份参数同时存在时，枚举仍只能选择一种运动逻辑，不能重复积分。
                foreach (PlayerFormKind form in Enum.GetValues(typeof(PlayerFormKind)))
                {
                    Reset(form);
                    Tick(1, new float2(1, 0));
                    float expectedMagnitude = form switch
                    {
                        PlayerFormKind.Solid => .125f,
                        PlayerFormKind.Liquid => 4,
                        PlayerFormKind.Gas => 1,
                        _ => throw new ArgumentOutOfRangeException(nameof(form))
                    };
                    float expectedHeight = form == PlayerFormKind.Gas ? 1f / 60 : 0;
                    Check(math.distance(Position().XY, new float2(expectedMagnitude / 60, 0)) < .001f
                        && math.abs(Position().Height - expectedHeight) < .001f
                        && manager.GetComponentData<PlayerFormComponent>(player).Value == form,
                        form + " executes only its own movement once per step");
                }

                // 将另两种形态参数置为无效值，确认当前分支不读取它们。
                foreach (PlayerFormKind form in Enum.GetValues(typeof(PlayerFormKind)))
                {
                    Reset(form);
                    manager.SetComponentData(player, new PlayerSolidComponent
                    {
                        MoveVelocityMagnitude = form == PlayerFormKind.Solid ? 3 : float.NaN,
                        ResponseTime = form == PlayerFormKind.Solid ? .4f : 0
                    });
                    manager.SetComponentData(player, new PlayerLiquidComponent
                    {
                        MoveVelocityMagnitude = form == PlayerFormKind.Liquid ? 4 : float.NaN
                    });
                    manager.SetComponentData(player, new PlayerGasComponent
                    {
                        MoveVelocityMagnitude = form == PlayerFormKind.Gas ? 1 : float.NaN
                    });
                    Tick(1, new float2(1, 0));
                    Check(math.all(math.isfinite(Position().XY)) && math.all(math.isfinite(Velocity()))
                        && Position().XY.x > 0, form + " ignores non-current form parameters");
                }

                PlayerSystem.SetForm(manager, player, PlayerFormKind.Liquid);
                bool rejected = false;
                try { PlayerSystem.SetForm(manager, player, (PlayerFormKind)255); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected && manager.GetComponentData<PlayerFormComponent>(player).Value == PlayerFormKind.Liquid,
                    "Invalid form preserves current form");

                var solidConfig = ScriptableObject.CreateInstance<PlayerSolidConfig>();
                var liquidConfig = ScriptableObject.CreateInstance<PlayerLiquidConfig>();
                var gasConfig = ScriptableObject.CreateInstance<PlayerGasConfig>();
                try
                {
                    Check(solidConfig.IsValid && liquidConfig.IsValid && gasConfig.IsValid, "Default form configs are valid");
                    var solidData = solidConfig.CreateComponent();
                    solidConfig.MoveVelocityMagnitude = 5;
                    Check(solidData.MoveVelocityMagnitude == 3, "Config conversion copies initial data");
                    solidConfig.ResponseTime = 0;
                    liquidConfig.MoveVelocityMagnitude = float.NaN;
                    gasConfig.MoveVelocityMagnitude = float.PositiveInfinity;
                    Check(!solidConfig.IsValid && !liquidConfig.IsValid && !gasConfig.IsValid, "Invalid form parameters are rejected");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(solidConfig);
                    UnityEngine.Object.DestroyImmediate(liquidConfig);
                    UnityEngine.Object.DestroyImmediate(gasConfig);
                }
            }
            report.AppendLine("Player checks passed: " + passed);
            return report.ToString();
        }
    }
}
