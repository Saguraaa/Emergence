using System;
using System.Linq;
using Emergence.Bootstrap;
using Emergence.Gameplay.Environment;
using Emergence.Gameplay.Physics;
using Emergence.Gameplay.Respawn;
using Emergence.UI.Input;
using Emergence.WorldView;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Emergence.Gameplay.Player.Editor
{
    public static class PlayerPrefabChecks
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Gameplay/Player.prefab";

        [MenuItem("Emergence/Checks/Player Prefab")]
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
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(prefab != null && prefab.GetComponent<PlayerBootstrap>() != null, "Shared Player prefab exists");
            var prefabView = new SerializedObject(prefab.GetComponent<PlayerView>());
            var bodyRoot = prefabView.FindProperty("bodyRoot").objectReferenceValue;
            var shadow = ((SpriteRenderer)prefabView.FindProperty("shadow").objectReferenceValue).transform;
            var gasParticles = prefabView.FindProperty("gasParticles").objectReferenceValue;
            Check(new SerializedObject(prefab.GetComponent<PlayerBootstrap>()).FindProperty("inputSettings") == null,
                "Player prefab receives scene input during initialization");
            Check(prefab.transform.position == Vector3.zero && !prefab.GetComponent<PlayerController>().enabled &&
                  prefab.GetComponentsInChildren<Collider2D>(true).All(x => !x.enabled), "Prefab has a neutral spawn and gated components");
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                Check(component != null, "Prefab has no missing scripts");
                var property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                        Check(EditorUtility.IsPersistent(property.objectReferenceValue), "Prefab references only its own contents and project assets");
            }
            int players = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    foreach (var player in roots.SelectMany(x => x.GetComponentsInChildren<PlayerView>(true)))
                    {
                        Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player) == PrefabPath,
                            path + ": Player uses the shared prefab");
                        var game = roots.SelectMany(x => x.GetComponentsInChildren<GameBootstrap>(true)).Single();
                        Check(new SerializedObject(game).FindProperty("playerBootstrap").objectReferenceValue == player.GetComponent<PlayerBootstrap>(),
                            path + ": GameBootstrap references the prefab instance");
                        var changes = PrefabUtility.GetPropertyModifications(player.gameObject);
                        Check(changes == null || changes.All(x =>
                              (x.target == prefab && x.propertyPath == "m_Name") ||
                              (x.target == prefab.transform) ||
                              (x.target is Collider2D && x.propertyPath == "m_Enabled" && x.value == "0") ||
                              (x.target is PlayerView && (x.propertyPath == "maximumDisplayY" || x.propertyPath == "heightDisplayScale")) ||
                              (path == "Assets/_Game/Scenes/Test/heightTest.unity" &&
                                  (((x.target == bodyRoot || x.target == shadow) && x.propertyPath.StartsWith("m_LocalScale.")) ||
                                   (x.target == gasParticles && x.propertyPath == "scalingMode") ||
                                   (x.target is BoxCollider2D && (x.propertyPath.StartsWith("m_Size.") || x.propertyPath.StartsWith("m_Offset."))) ||
                                   (x.target is CircleCollider2D && x.propertyPath == "m_Radius"))) ||
                              (x.target is PlayerBootstrap && (x.propertyPath == "initialForm" || x.propertyPath == "initialHeight" ||
                                  x.propertyPath == "damageFieldRadius" || x.propertyPath == "damageFieldStrength"))),
                            path + ": prefab overrides are limited to scene configuration and heightTest scale");
                        players++;
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            Check(players > 0, "Project scenes contain shared Player instances");
            CheckLifecycle(prefab, Check);
            return "Player prefab code/data checks passed: " + passed + "; scene instances: " + players + ".";
        }

        private static void CheckLifecycle(GameObject prefab, Action<bool, string> check)
        {
            using var world = new World("Player prefab checks");
            var previousWorld = World.DefaultGameObjectInjectionWorld;
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = new Vector3(11, -7, 0);
            var bootstrap = instance.GetComponent<PlayerBootstrap>();
            var inputObject = new GameObject("Player prefab check input");
            SceneManager.MoveGameObjectToScene(inputObject, scene);
            var inputs = inputObject.AddComponent<InputSettingsController>();
            var inputBootstrap = inputObject.AddComponent<InputBootstrap>();
            var inputData = new SerializedObject(inputs);
            inputData.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            inputData.ApplyModifiedPropertiesWithoutUndo();
            var gateData = new SerializedObject(inputBootstrap);
            gateData.FindProperty("inputSettings").objectReferenceValue = inputs;
            gateData.ApplyModifiedPropertiesWithoutUndo();
            World.DefaultGameObjectInjectionWorld = world;
            try
            {
                world.GetOrCreateSystemManaged<PhysicsSystem>();
                world.GetOrCreateSystemManaged<RespawnSystem>();
                world.EntityManager.CreateEntity(typeof(EnvironmentComponent));
                var resolvedInputs = inputBootstrap.Initialize();
                check(resolvedInputs == inputs, "Input gate supplies its initialized scene input instance");
                bootstrap.Initialize(resolvedInputs);
                using var query = world.EntityManager.CreateEntityQuery(typeof(PlayerTagComponent));
                check(query.CalculateEntityCount() == 1, "Prefab bootstrap creates exactly one Player entity");
                var entity = query.GetSingletonEntity();
                check(math.all(world.EntityManager.GetComponentData<PlayerPositionComponent>(entity).XY == new float2(11, -7)) &&
                      world.EntityManager.HasComponent<DamageFieldComponent>(entity), "Scene spawn and field data initialize on the Player entity");
                check(instance.GetComponent<PlayerController>().enabled, "Injected input enables the Player controller");
                bootstrap.Shutdown();
                check(query.CalculateEntityCount() == 0 && !instance.GetComponent<PlayerController>().enabled &&
                      instance.GetComponentsInChildren<Collider2D>(true).All(x => !x.enabled), "Shutdown stops access, unregisters collision and destroys the entity");
            }
            finally
            {
                bootstrap.Shutdown();
                inputBootstrap.Shutdown();
                World.DefaultGameObjectInjectionWorld = previousWorld;
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previousScene);
            }
        }
    }
}
