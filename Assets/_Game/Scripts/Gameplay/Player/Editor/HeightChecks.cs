using System;
using Emergence.Gameplay.Converter;
using Emergence.Gameplay.Environment;
using Emergence.Gameplay.Interaction;
using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Physics.Editor;
using Emergence.WorldView;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using InteractionMode = Emergence.Gameplay.Interaction.InteractionMode;

namespace Emergence.Gameplay.Player.Editor
{
    public static class HeightChecks
    {
        [MenuItem("Emergence/Checks/Height")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            using var world = new World("Height checks");
            using var scene = new PhysicsChecksScene(world);
            var manager = world.EntityManager;
            var environment = manager.CreateEntity(typeof(EnvironmentComponent));
            manager.SetComponentData(environment, new EnvironmentComponent
            {
                MaximumHeight = 2, Gravity = 9.81f, WindVelocity = new float3(0, 0, .5f)
            });
            var player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerPositionComponent),
                typeof(PlayerMotionComponent), typeof(PlayerInputComponent), typeof(PlayerFormComponent),
                typeof(PlayerGasComponent), typeof(PlayerLiquidComponent), typeof(PlayerSolidComponent));
            manager.SetComponentData(player, new PlayerGasComponent { MoveVelocityMagnitude = 1 });
            manager.SetComponentData(player, new PlayerLiquidComponent { MoveVelocityMagnitude = 4 });
            manager.SetComponentData(player, new PlayerSolidComponent { MoveVelocityMagnitude = 3, ResponseTime = .4f });
            scene.AddBody(manager, player);
            var bathtub = manager.CreateEntity(typeof(ObstacleTagComponent), typeof(ObstacleStateComponent));
            var bathtubCollider = scene.AddObstacle(bathtub, Vector2.zero, new Vector2(1, 2), .6f);
            var point = manager.CreateEntity(typeof(InteractiveComponent), typeof(InteractionStateComponent));
            manager.AddComponentData(point, new InteractionRequirementComponent
            {
                MinimumHeight = 1.8f, MaximumHeight = 2, AllowedForms = InteractionForms.Gas
            });
            manager.AddBuffer<InteractionOverlapComponent>(point);
            var rangeRoot = new GameObject("Height checks shower XY");
            SceneManager.MoveGameObjectToScene(rangeRoot, scene.Gas.gameObject.scene);
            rangeRoot.transform.position = new Vector3(3, 0);
            var range = rangeRoot.AddComponent<CircleCollider2D>();
            range.isTrigger = true;
            range.radius = .5f;
            scene.Physics.RegisterInteraction(point, range, 1 << 31);
            var movement = world.GetOrCreateSystemManaged<PlayerSystem>();
            var interaction = world.GetOrCreateSystemManaged<InteractionSystem>();
            int passed = 0;
            double time = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + label);
                passed++;
            }
            void Reset(float2 xy, float height)
            {
                manager.SetComponentData(player, new PlayerPositionComponent { XY = xy, Height = height });
                manager.SetComponentData(player, new PlayerMotionComponent());
                PlayerSystem.SetForm(manager, player, PlayerFormKind.Gas);
                manager.SetComponentData(point, new InteractionStateComponent { Mode = InteractionMode.EnterRange });
            }
            void Tick(int count, float2 input)
            {
                manager.SetComponentData(player, new PlayerInputComponent { Move = input });
                for (int i = 0; i < count; i++)
                {
                    time += 1.0 / 60;
                    world.SetTime(new TimeData(time, 1f / 60));
                    movement.Update();
                    scene.Physics.Update();
                    interaction.Update();
                }
            }
            PlayerPositionComponent Position() => manager.GetComponentData<PlayerPositionComponent>(player);
            uint Take() => InteractionSystem.TakeActivations(manager, point);

            Reset(new float2(-1.102f, 0), 0);
            Tick(60, new float2(1, 0));
            Check(Position().XY.x < -1.09f && math.abs(Position().Height - .5f) < .001f,
                "Gas is blocked by the bathtub during its initial ascent");
            Tick(180, new float2(1, 0));
            Check(Position().XY.x > 1.1f && bathtubCollider.enabled,
                "Gas crosses the bathtub after rising above 0.6m without disabling its collider");

            Reset(new float2(-3, 0), 0);
            Tick(600, float2.zero);
            Check(Position().Height == 2 && manager.GetComponentData<PlayerMotionComponent>(player).Velocity.z == 0,
                "Long ascent stops at exactly 2m with zero vertical velocity");
            Tick(60, new float2(0, 1));
            Check(math.abs(Position().XY.y - 1) < .001f && Position().Height == 2,
                "W changes ground XY while height remains at the ceiling");
            Tick(60, new float2(0, -1));
            Check(math.abs(Position().XY.y) < .001f && Position().Height == 2,
                "S changes ground XY without lowering gas height");
            Reset(new float2(-3, 0), 0);
            Tick(60, new float2(0, -1));
            Check(math.abs(Position().Height - .5f) < .001f && Position().XY.y < -.99f,
                "Down input does not oppose ascent from environment wind");

            Reset(new float2(3, 0), 1.7f);
            Tick(1, float2.zero);
            Check(Take() == 0 && !manager.GetComponentData<InteractionStateComponent>(point).IsOccupied,
                "Entering the shower XY trigger below 1.8m cannot activate it");
            Tick(14, float2.zero);
            Check(Take() == 1, "Rising in place into the allowed height range activates the shower");
            float conversionHeight = Position().Height;
            Check(ConverterSystem.TryConvert(manager, player, ConverterKind.GasToLiquid) &&
                  Position().Height == conversionHeight, "Gas to liquid preserves current height");
            Tick(1, float2.zero);
            Check(Position().Height < conversionHeight && Take() == 0,
                "Liquid falls and cannot reactivate the gas-only shower");
            Tick(120, float2.zero);
            Check(Position().Height == 0, "Liquid lands on the floor");

            Reset(new float2(3, 0), 2);
            Tick(1, float2.zero);
            Check(Take() == 1 && ConverterSystem.TryConvert(manager, player, ConverterKind.GasToLiquid),
                "Entering the shower at the ceiling satisfies the inclusive upper height bound");
            Reset(new float2(3, 0), 2);
            manager.SetComponentData(point, new InteractionRequirementComponent
            {
                MinimumHeight = 1.8f, MaximumHeight = 1.9f, AllowedForms = InteractionForms.Gas
            });
            Tick(1, float2.zero);
            Check(Take() == 0, "Being inside XY at the ceiling still cannot bypass the maximum height requirement");
            Reset(float2.zero, 2);
            ConverterSystem.TryConvert(manager, player, ConverterKind.GasToLiquid);
            Tick(120, float2.zero);
            Check(Position().Height > .6f && Position().Height < .61f && math.length(Position().XY) < .001f,
                "Liquid descending above the bathtub is supported by its top");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Gameplay/Player.prefab");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene.Gas.gameObject.scene);
            try
            {
                var view = visual.GetComponent<PlayerView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("maximumDisplayY").floatValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var body = (Transform)serialized.FindProperty("bodyRoot").objectReferenceValue;
                var shadow = (SpriteRenderer)serialized.FindProperty("shadow").objectReferenceValue;
                view.Initialize(PlayerFormKind.Gas);
                var position = new PlayerPositionComponent { Height = 2 };
                var field = new DamageFieldComponent { Radius = 3 };
                view.Refresh(position, PlayerFormKind.Gas, new PlayerMotionComponent(), field, false);
                float shadowY = shadow.transform.position.y;
                position.XY.y = .5f;
                view.Refresh(position, PlayerFormKind.Gas, new PlayerMotionComponent(), field, false);
                Check(math.abs(body.position.y - 2) < .001f &&
                      math.abs(shadow.transform.position.y - shadowY - .5f) < .001f,
                    "At the display ceiling W moves the shadow while the body stays capped");
                Vector3 gasPosition = body.position;
                view.Refresh(position, PlayerFormKind.Liquid, new PlayerMotionComponent(), field, false);
                Check(Vector3.Distance(body.position, gasPosition) < .001f,
                    "Converting at the display ceiling does not jump the body anchor");
                Check(Position().Height > .6f && Position().Height < .61f,
                    "View projection does not modify ECS height");
                view.Refresh(position, PlayerFormKind.Gas, new PlayerMotionComponent(), field, false);
                shadowY = shadow.transform.position.y;
                serialized.Update();
                serialized.FindProperty("heightDisplayScale").floatValue = 2;
                serialized.FindProperty("maximumDisplayY").floatValue = float.MaxValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                view.Refresh(position, PlayerFormKind.Gas, new PlayerMotionComponent(), field, false);
                Check(math.abs(body.position.y - 4.5f) < .001f && math.abs(visual.transform.position.y - .5f) < .001f,
                    "Two meters project to four display units without moving ground XY");
                Check(math.abs(shadow.transform.position.y - shadowY) < .001f,
                    "Increasing the height display scale keeps the ground shadow in place");
                view.Refresh(position, PlayerFormKind.Liquid, new PlayerMotionComponent(), field, false);
                Check(math.abs(body.position.y - 4.5f) < .001f,
                    "Gas to liquid preserves the scaled display height");
                position.Height = 0;
                view.Refresh(position, PlayerFormKind.Liquid, new PlayerMotionComponent(), field, false);
                Check(math.abs(body.position.y - position.XY.y) < .001f,
                    "Zero gameplay height still displays on the ground");
                serialized.FindProperty("maximumDisplayY").floatValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                position.Height = 2;
                view.Refresh(position, PlayerFormKind.Gas, new PlayerMotionComponent(), field, false);
                Check(math.abs(body.position.y - 2) < .001f,
                    "Scaled height still respects the scene display ceiling");
                view.Shutdown();
            }
            finally { UnityEngine.Object.DestroyImmediate(visual); }
            scene.Physics.UnregisterInteraction(point);
            return passed + " height checks passed.";
        }
    }
}
