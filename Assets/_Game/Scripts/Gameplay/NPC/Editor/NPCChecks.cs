using System;
using System.Linq;
using Emergence.Bootstrap;
using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Physics;
using Emergence.Gameplay.Physics.Editor;
using Emergence.Gameplay.Player;
using Emergence.WorldView;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Emergence.Gameplay.NPC.Editor
{
    public static class NPCChecks
    {
        [MenuItem("Emergence/Checks/NPC")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            int passed = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + label);
                passed++;
            }
            CheckDamage(Check);
            CheckMovement(Check);
            CheckSceneData(Check, "Assets/_Game/Scenes/Test/AttackTest.unity", 0);
            CheckSceneData(Check, "Assets/_Game/Scenes/Test/NpcTest.unity", 2);
            return "NPC code/data checks passed: " + passed + ". No Play-mode scene acceptance or attack effects executed.";
        }

        private static void CheckDamage(Action<bool, string> check)
        {
            using var world = new World("Resistance checks");
            var manager = world.EntityManager;
            var player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerPositionComponent),
                typeof(PlayerFormComponent), typeof(DamageFieldComponent), typeof(DeadTagComponent));
            manager.SetComponentEnabled<DeadTagComponent>(player, false);
            manager.SetComponentData(player, new DamageFieldComponent { Radius = 4, Strength = 10 });
            var npc = manager.CreateEntity(typeof(NPCTagComponent), typeof(NPCPositionComponent), typeof(NPCResistanceComponent));
            var system = world.GetOrCreateSystemManaged<PlayerSystem>();
            float Evaluate(float distance, int steps = 1, float dt = 1)
            {
                manager.SetComponentData(npc, new NPCResistanceComponent { InitialValue = 100, Value = 100 });
                manager.SetComponentData(npc, new NPCPositionComponent { XY = new float2(distance, 0) });
                for (int i = 0; i < steps; i++)
                {
                    world.SetTime(new TimeData((i + 1) * dt, dt));
                    system.ApplyDamageFields(dt);
                }
                return manager.GetComponentData<NPCResistanceComponent>(npc).Value;
            }
            check(math.abs(Evaluate(0) - 90) < .001f, "Center applies full strength per second");
            check(math.abs(Evaluate(2) - 95) < .001f, "Half radius applies half strength");
            check(Evaluate(4) == 100 && Evaluate(5) == 100, "Boundary and outside do not reduce resistance");
            check(math.abs(Evaluate(0, 10, .1f) - Evaluate(0)) < .001f, "Reduction is independent of fixed-step size");
            foreach (var form in new[] { PlayerFormKind.Solid, PlayerFormKind.Liquid, PlayerFormKind.Gas })
            {
                manager.SetComponentData(player, new PlayerFormComponent { Value = form });
                manager.SetComponentData(player, new PlayerPositionComponent { Height = 10 });
                check(math.abs(Evaluate(2) - 95) < .001f, form + " shares field parameters and ignores visual height");
            }
            manager.SetComponentEnabled<DeadTagComponent>(player, true);
            check(Evaluate(0) == 100, "Dead player does not reduce resistance");
            manager.SetComponentEnabled<DeadTagComponent>(player, false);
            var other = manager.CreateEntity(typeof(NPCTagComponent), typeof(NPCPositionComponent), typeof(NPCResistanceComponent));
            manager.SetComponentData(other, new NPCPositionComponent { XY = new float2(2, 0) });
            manager.SetComponentData(other, new NPCResistanceComponent { InitialValue = 200, Value = 200 });
            Evaluate(0);
            check(manager.GetComponentData<NPCResistanceComponent>(other).Value == 195, "NPCs have independent resistance values");
            manager.SetComponentData(player, new DamageFieldComponent { Radius = 4, Strength = 1000 });
            check(Evaluate(0) == 0, "Resistance clamps at zero");
        }

        private static void CheckMovement(Action<bool, string> check)
        {
            using var world = new World("NPC movement checks");
            using var scene = new PhysicsChecksScene(world);
            var manager = world.EntityManager;
            var player = manager.CreateEntity(typeof(PlayerPositionComponent));
            scene.AddBody(manager, player);
            var npc = manager.CreateEntity(typeof(NPCTagComponent), typeof(NPCPositionComponent), typeof(NPCMotionComponent),
                typeof(NPCResistanceComponent), typeof(NPCBehaviorComponent), typeof(NPCAttackComponent));
            manager.SetComponentData(npc, new NPCPositionComponent { XY = new float2(3, 0) });
            manager.SetComponentData(npc, new NPCMotionComponent { MoveVelocityMagnitude = 2, ArrivalDistance = .05f, BlockedDuration = .5f });
            manager.SetComponentData(npc, new NPCResistanceComponent { InitialValue = 100, Value = 100 });
            manager.SetComponentData(npc, new NPCBehaviorComponent { FleeThresholdRatio = .8f });
            manager.AddBuffer<PhysicsContactComponent>(npc);
            var route = manager.AddBuffer<NPCWaypointComponent>(npc);
            route.Add(new NPCWaypointComponent { XY = new float2(5, 0), WaitDuration = .5f });
            route.Add(new NPCWaypointComponent { XY = new float2(5, 2) });
            route.Add(new NPCWaypointComponent { XY = new float2(7, 2), WaitDuration = .2f });
            var bodyObject = new GameObject("NPC checks body");
            SceneManager.MoveGameObjectToScene(bodyObject, scene.Solid.gameObject.scene);
            bodyObject.layer = 31;
            var body = bodyObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var collider = bodyObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(.8f, .8f);
            scene.Physics.RegisterBody(npc, body, new Collider2D[] { collider }, (1 << 30) | (1 << 31));
            try
            {
                var system = world.GetOrCreateSystemManaged<NPCSystem>();
                double time = 0;
                void Tick(int count = 1)
                {
                    for (int i = 0; i < count; i++)
                    {
                        time += .1;
                        world.SetTime(new TimeData(time, .1f));
                        system.Update();
                        scene.Physics.Update();
                    }
                }
                NPCBehaviorComponent State() => manager.GetComponentData<NPCBehaviorComponent>(npc);
                float2 Position() => manager.GetComponentData<NPCPositionComponent>(npc).XY;
                void Resistance(float value) => manager.SetComponentData(npc, new NPCResistanceComponent { InitialValue = 100, Value = value });
                check(!manager.HasComponent<PlayerPositionComponent>(npc), "NPC position is independent of player components");
                check(body.position == new Vector2(3, 0), "Registration synchronizes the initial physical position");
                Tick();
                check(State().Value == NPCBehaviorKind.Normal && math.distance(Position(), new float2(3, 0)) < .001f, "Full resistance waits normally");
                Resistance(99);
                Tick();
                check(State().Value == NPCBehaviorKind.Alert && Position().x == 3, "First reduction alerts without moving");
                Resistance(80);
                Tick();
                check(State().Value == NPCBehaviorKind.Fleeing && Position().x > 3, "Exactly 80 percent begins fleeing in the same decision step");
                for (int i = 0; i < 50 && State().RoutePhase != NPCRoutePhase.Waiting; i++) Tick();
                check(State().TargetIndex == 0 && State().RoutePhase == NPCRoutePhase.Waiting, "First waypoint starts its wait once");
                float2 waitPosition = Position();
                Tick(2);
                check(State().TargetIndex == 0 && State().WaitRemaining > 0 && math.distance(waitPosition, Position()) < .001f,
                    "Waiting consumes time without movement or restarting the timer");
                bool skippedZeroWait = false;
                for (int i = 0; i < 150 && State().RoutePhase != NPCRoutePhase.Completed; i++)
                {
                    int previous = State().TargetIndex;
                    Tick();
                    if (previous == 1 && State().TargetIndex == 2) skippedZeroWait = true;
                }
                check(skippedZeroWait && State().TargetIndex == 3 && State().RoutePhase == NPCRoutePhase.Completed,
                    "Waypoints run in order, including zero wait, then complete");
                check(math.distance(Position(), new float2(7, 2)) <= .051f, "Final position respects arrival tolerance");
                var finalPosition = Position();
                Tick(10);
                check(math.distance(finalPosition, Position()) < .001f && State().Value == NPCBehaviorKind.Fleeing,
                    "Completed route neither loops nor resets behavior");
                var wall = manager.CreateEntity(typeof(ObstacleTagComponent));
                scene.AddObstacle(wall, new Vector2(4, 0), new Vector2(.5f, 3));
                void ResetRoute(float2 target)
                {
                    manager.SetComponentData(npc, new NPCPositionComponent { XY = new float2(3, 0) });
                    manager.SetComponentData(npc, new NPCBehaviorComponent { FleeThresholdRatio = .8f });
                    var buffer = manager.GetBuffer<NPCWaypointComponent>(npc);
                    buffer.Clear();
                    buffer.Add(new NPCWaypointComponent { XY = target });
                }
                ResetRoute(new float2(5, 0));
                Tick(40);
                check(State().RoutePhase == NPCRoutePhase.Blocked && State().TargetIndex == 0 && Position().x < 3.352f,
                    "Wall blocks movement; stuck route stops without skipping or teleporting");
                bool wallContact = false;
                foreach (var contact in manager.GetBuffer<PhysicsContactComponent>(npc)) wallContact |= contact.Entity == wall;
                check(wallContact, "Unified physics reports NPC-wall contact");
                scene.Physics.SetObstacleEnabled(wall, false);
                ResetRoute(new float2(5, 0));
                Tick(40);
                check(State().RoutePhase == NPCRoutePhase.Completed, "Disabled wall immediately releases movement");
                ResetRoute(new float2(-2, 0));
                Tick(50);
                check(State().RoutePhase == NPCRoutePhase.Blocked && Position().x >= .798f,
                    "NPC collider also blocks against the registered player");
                manager.AddComponent<PlayerTagComponent>(player);
                manager.AddComponentData(player, new DamageFieldComponent { Radius = 4, Strength = 10 });
                manager.AddComponent<DeadTagComponent>(player);
                manager.SetComponentEnabled<DeadTagComponent>(player, false);
                ResetRoute(new float2(5, 0));
                Resistance(80.25f);
                world.GetOrCreateSystemManaged<PlayerSystem>().ApplyDamageFields(.1f);
                Tick();
                check(manager.GetComponentData<NPCResistanceComponent>(npc).Value == 80 &&
                      State().Value == NPCBehaviorKind.Fleeing && Position().x > 3,
                    "Actual player field feeds the NPC threshold and movement in one fixed-step sequence");
                ResetRoute(new float2(3.02f, 0));
                var slow = manager.GetComponentData<NPCMotionComponent>(npc);
                slow.MoveVelocityMagnitude = .01f;
                slow.ArrivalDistance = .001f;
                manager.SetComponentData(npc, slow);
                Tick(10);
                check(State().RoutePhase == NPCRoutePhase.Moving && Position().x > 3.005f,
                    "Slow but progressing movement is not falsely classified as blocked");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Gameplay/NPC.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bodyObject.scene);
                var controller = instance.GetComponent<NPCController>();
                try
                {
                    var view = instance.GetComponent<NPCView>();
                    controller.Initialize(world, npc, view);
                    var bar = instance.transform.Find("HeadUI").gameObject;
                    check(!bar.activeSelf, "Resistance bar starts hidden");
                    Resistance(70);
                    controller.Refresh();
                    check(bar.activeSelf && math.abs(bar.GetComponentsInChildren<Image>().Single(x => x.type == Image.Type.Filled).fillAmount - .7f) < .001f,
                        "Actual resistance reduction reveals the correct bar fraction");
                    controller.Shutdown();
                    check(!controller.enabled && !bar.activeSelf, "NPC shutdown stops access and hides its UI");
                    bool rejected = false;
                    try { instance.GetComponent<NPCBootstrap>().Initialize(); }
                    catch (InvalidOperationException exception) { rejected = exception.Message.Contains("waypoint"); }
                    check(rejected, "Prefab without scene route fails initialization with a specific error");
                }
                finally
                {
                    controller.Shutdown();
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            finally { scene.Physics.UnregisterBody(npc); }
        }

        private static void CheckSceneData(Action<bool, string> check, string path, int expectedNPCs)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var objects = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
                check(objects.All(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject) == 0), path + ": no missing scripts");
                var all = objects.SelectMany(x => x.GetComponents<MonoBehaviour>()).ToArray();
                var game = all.OfType<GameBootstrap>().Single();
                var data = new SerializedObject(game);
                foreach (string field in new[] { "inputBootstrap", "environmentBootstrap", "playerBootstrap" })
                    check(data.FindProperty(field).objectReferenceValue != null, path + ": " + field + " is assigned");
                foreach (string field in new[] { "cameraBootstraps", "npcBootstraps", "obstacleBootstraps", "interactionBootstraps", "respawnBootstraps" })
                {
                    var array = data.FindProperty(field);
                    for (int i = 0; i < array.arraySize; i++)
                        check(array.GetArrayElementAtIndex(i).objectReferenceValue != null, path + ": " + field + " contains valid entries");
                }
                check(data.FindProperty("damageFieldBootstraps") == null,
                    path + ": no independent damage field gate remains");
                check(all.OfType<NPCBootstrap>().Count() == expectedNPCs && data.FindProperty("npcBootstraps").arraySize == expectedNPCs,
                    path + ": expected NPC gates are registered");
                foreach (var player in all.OfType<PlayerView>())
                {
                    var playerData = new SerializedObject(player);
                    var root = (Transform)playerData.FindProperty("damageFieldRoot").objectReferenceValue;
                    check(root != null && root.parent == player.transform && root.localPosition == Vector3.zero,
                        path + ": damage field belongs to the player ground root");
                    check(root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 && !PrefabUtility.IsAnyPrefabInstanceRoot(root.gameObject),
                        path + ": damage field has no controller, gate, view component or independent prefab instance");
                    check(new SerializedObject(player.GetComponent<PlayerController>()).FindProperty("showDamageFieldAction").objectReferenceValue != null,
                        path + ": PlayerController owns the field input reference");
                }
                if (expectedNPCs > 0)
                {
                    var wall = all.OfType<ObstacleBootstrap>().Single(x => x.name == "Route Wall");
                    var collider = (BoxCollider2D)new SerializedObject(wall).FindProperty("colliderBounds").objectReferenceValue;
                    check(wall.transform.localScale == Vector3.one && collider.size == new Vector2(1.2f, 4),
                        "Route wall visual sizing does not enlarge its physical footprint");
                    check(wall.GetComponent<SpriteRenderer>().size == collider.size,
                        "Route wall renderer size agrees with the configured collider");
                }
                foreach (var npc in all.OfType<NPCBootstrap>())
                {
                    var npcData = new SerializedObject(npc);
                    foreach (string field in new[] { "controller", "npcView", "physicsBody", "colliderBounds" })
                        check(npcData.FindProperty(field).objectReferenceValue != null, npc.name + ": " + field + " is assigned");
                    var waypoints = npcData.FindProperty("waypoints");
                    check(waypoints.arraySize > 0, npc.name + ": scene has an escape route");
                    for (int i = 0; i < waypoints.arraySize; i++)
                    {
                        var point = waypoints.GetArrayElementAtIndex(i);
                        var target = (Transform)point.FindPropertyRelative("Point").objectReferenceValue;
                        check(target != null && !target.IsChildOf(npc.transform) && point.FindPropertyRelative("WaitDuration").floatValue >= 0,
                            npc.name + ": fixed external waypoint and valid wait duration");
                    }
                    var body = npc.GetComponent<Rigidbody2D>();
                    check(body.bodyType == RigidbodyType2D.Kinematic && body.simulated &&
                          (body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0, npc.name + ": valid physical body");
                    check(!npc.GetComponent<NPCController>().enabled && !npc.GetComponentInChildren<Collider2D>(true).enabled,
                        npc.name + ": controller and collider are gated before initialization");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
