using System;
using System.Linq;
using Emergence.Bootstrap;
using Emergence.Gameplay.Environment;
using Emergence.Gameplay.InteractCallback;
using Emergence.Gameplay.NPC;
using Emergence.Gameplay.Physics;
using Emergence.Gameplay.Respawn;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emergence.Gameplay.Player.Editor
{
    public static class SceneTransferChecks
    {
        [MenuItem("Emergence/Checks/Scene Transfer")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            const string destination = "Assets/_Game/Scenes/Level/Tutorial01.unity";
            var source = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Level/PipeMap.unity");
            var target = EditorSceneManager.OpenPreviewScene(destination);
            T Find<T>(UnityEngine.SceneManagement.Scene scene) where T : Component =>
                scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).Single();
            var sourcePlayer = Find<PlayerBootstrap>(source);
            var targetPlayer = Find<PlayerBootstrap>(target);
            var sourceInput = Find<InputBootstrap>(source);
            var targetInput = Find<InputBootstrap>(target);
            using var world = new World("Scene transfer checks");
            var previousWorld = World.DefaultGameObjectInjectionWorld;
            World.DefaultGameObjectInjectionWorld = world;
            int passed = 0;
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + name);
                passed++;
            }
            try
            {
                var manager = world.EntityManager;
                world.GetOrCreateSystemManaged<PhysicsSystem>();
                world.GetOrCreateSystemManaged<RespawnSystem>();
                var fixedStep = world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
                manager.CreateEntity(typeof(EnvironmentComponent));
                sourcePlayer.Initialize(sourceInput.Initialize());
                using var players = manager.CreateEntityQuery(typeof(PlayerTagComponent));
                var player = players.GetSingletonEntity();
                PlayerSystem.SetForm(manager, player, PlayerFormKind.Gas);
                manager.SetComponentData(player, new PlayerPositionComponent { XY = new float2(9, 8), Height = 3.25f });
                manager.SetComponentData(player, new PlayerMotionComponent { Velocity = new float3(2, -3, 4) });
                manager.SetComponentData(player, new PlayerInputComponent { Move = new float2(.25f, -.5f), InteractPressed = true });
                manager.SetComponentData(player, new PlayerSolidComponent { MoveVelocityMagnitude = 5.75f, ResponseTime = 1.125f });
                manager.SetComponentData(player, new PlayerLiquidComponent { MoveVelocityMagnitude = 8.5f });
                manager.SetComponentData(player, new PlayerGasComponent { MoveVelocityMagnitude = 2.1f });
                manager.SetComponentData(player, new DamageFieldComponent { Radius = 7, Strength = 13.75f });
                manager.SetComponentEnabled<DeadTagComponent>(player, true);
                // 玩家门控不认识的额外组件也必须原样保留，不能靠列举状态复制实现。
                manager.AddComponentData(player, new NPCResistanceComponent { Value = 87.5f });
                Check(SceneTransferSystem.Begin(world, destination, "TutorialRight"), "First transfer is accepted");
                Check(!fixedStep.Enabled && SceneTransferSystem.IsPending(manager), "Fixed-step rules pause before scene unbinding");
                Check(!SceneTransferSystem.Begin(world, destination, "Other"), "Repeated activation does not replace a pending destination");
                sourcePlayer.Shutdown();
                sourceInput.Shutdown();
                Check(manager.Exists(player) && players.CalculateEntityCount() == 1, "Source shutdown retains the same player entity");
                Check(manager.GetComponentData<PlayerInputComponent>(player).InteractPressed, "Unbinding does not clear player input state");
                EditorSceneManager.ClosePreviewScene(source);
                Check(manager.Exists(player), "Unloading source objects does not destroy player state");
                targetPlayer.Initialize(targetInput.Initialize());
                Check(players.CalculateEntityCount() == 1 && players.GetSingletonEntity() == player, "Target rebinds the original entity and version");
                var point = Find<SceneTransferPoint>(target);
                var position = manager.GetComponentData<PlayerPositionComponent>(player);
                Check(math.all(position.XY == new float2(point.Anchor.position.x, point.Anchor.position.y)), "Arrival uses an arbitrary scene object's Transform");
                Check(position.Height == 3.25f, "Transfer preserves independent Height");
                Check(manager.GetComponentData<PlayerFormComponent>(player).Value == PlayerFormKind.Gas, "Target defaults cannot reset form");
                Check(math.all(manager.GetComponentData<PlayerMotionComponent>(player).Velocity == new float3(2, -3, 4)), "Transfer preserves all velocity axes");
                var input = manager.GetComponentData<PlayerInputComponent>(player);
                Check(math.all(input.Move == new float2(.25f, -.5f)) && input.InteractPressed, "Target binding preserves pending input");
                Check(manager.IsComponentEnabled<DeadTagComponent>(player), "Enableable gameplay state is preserved");
                Check(manager.GetComponentData<NPCResistanceComponent>(player).Value == 87.5f, "Unlisted extra components survive without copying");
                Check(manager.GetComponentData<PlayerSolidComponent>(player).MoveVelocityMagnitude == 5.75f &&
                    manager.GetComponentData<PlayerSolidComponent>(player).ResponseTime == 1.125f &&
                    manager.GetComponentData<PlayerLiquidComponent>(player).MoveVelocityMagnitude == 8.5f &&
                    manager.GetComponentData<PlayerGasComponent>(player).MoveVelocityMagnitude == 2.1f,
                    "Runtime movement parameters are not overwritten by destination configs");
                var field = manager.GetComponentData<DamageFieldComponent>(player);
                Check(field.Radius == 7 && field.Strength == 13.75f, "Runtime damage-field parameters are preserved");
                var data = new SerializedObject(targetPlayer);
                Check(((Collider2D)data.FindProperty("gasCollider").objectReferenceValue).enabled &&
                    !((Collider2D)data.FindProperty("solidCollider").objectReferenceValue).enabled,
                    "Physics registration immediately selects the retained form's collider");
                Check(!fixedStep.Enabled, "Simulation remains paused until all destination bindings are ready");
                SceneTransferSystem.Complete(world, destination);
                Check(fixedStep.Enabled && !SceneTransferSystem.IsPending(manager), "Completion consumes the request and resumes rules");
                Check(SceneTransferSystem.Begin(world, destination, "MissingPoint"), "Another transfer can start after completion");
                bool rejected = false;
                try { SceneTransferSystem.Arrive(world, player, target); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && math.all(manager.GetComponentData<PlayerPositionComponent>(player).XY == position.XY),
                    "Missing anchors fail explicitly without teleporting to a fallback");
                SceneTransferSystem.Complete(world, destination);
                targetPlayer.Shutdown();
                Check(!manager.Exists(player), "Normal final shutdown destroys the player");
            }
            finally
            {
                if (source.IsValid()) { sourcePlayer.Shutdown(); sourceInput.Shutdown(); EditorSceneManager.ClosePreviewScene(source); }
                targetPlayer.Shutdown();
                targetInput.Shutdown();
                EditorSceneManager.ClosePreviewScene(target);
                World.DefaultGameObjectInjectionWorld = previousWorld;
            }
            return passed + " scene transfer checks passed.";
        }
    }
}
