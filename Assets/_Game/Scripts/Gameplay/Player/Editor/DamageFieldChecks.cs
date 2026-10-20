using System;
using System.Linq;
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
    public static class DamageFieldChecks
    {
        [MenuItem("Emergence/Checks/Damage Field")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            using var world = new World("Player damage field checks");
            var previousScene = SceneManager.GetActiveScene();
            var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var source = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Test/AttackTest.unity");
            GameObject root;
            try
            {
                var sourceView = source.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PlayerView>(true)).Single();
                SceneManager.SetActiveScene(fixture);
                root = UnityEngine.Object.Instantiate(sourceView.gameObject);
                SceneManager.MoveGameObjectToScene(root, fixture);
            }
            finally { EditorSceneManager.ClosePreviewScene(source); }
            var view = root.GetComponent<PlayerView>();
            var controller = root.GetComponent<PlayerController>();
            var inputObject = new GameObject("Player field check input");
            SceneManager.MoveGameObjectToScene(inputObject, fixture);
            var inputs = inputObject.AddComponent<InputSettingsController>();
            try
            {
                int passed = 0;
                void Check(bool condition, string label)
                {
                    if (!condition) throw new InvalidOperationException("FAIL: " + label);
                    passed++;
                }
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
                var controllerData = new SerializedObject(controller);
                var reference = (InputActionReference)controllerData.FindProperty("showDamageFieldAction").objectReferenceValue;
                var inputData = new SerializedObject(inputs);
                inputData.FindProperty("inputActions").objectReferenceValue = asset;
                inputData.ApplyModifiedPropertiesWithoutUndo();
                inputs.Initialize();
                var action = inputs.GetAction(reference);
                Check(action != reference.action && action.id == reference.action.id, "Player resolves its shared runtime action by ID");
                Check(action.type == InputActionType.Button && action.bindings.Any(x => x.path == "<Keyboard>/leftAlt") &&
                      action.bindings.Any(x => x.path == "<Keyboard>/rightAlt"), "Player owns a Button action bound to both Alt keys");
                var manager = world.EntityManager;
                var player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerPositionComponent), typeof(PlayerMotionComponent),
                    typeof(PlayerFormComponent), typeof(PlayerInputComponent), typeof(DamageFieldComponent), typeof(DeadTagComponent));
                manager.SetComponentEnabled<DeadTagComponent>(player, false);
                manager.SetComponentData(player, new PlayerPositionComponent { XY = new float2(2, -1), Height = 5 });
                manager.SetComponentData(player, new DamageFieldComponent { Radius = 3, Strength = 25 });
                world.GetOrCreateSystemManaged<RespawnSystem>();
                controller.Initialize(world, player, view, inputs);
                var visual = root.transform.Find("DamageField");
                var outline = visual.GetComponentInChildren<MeshFilter>(true);
                var renderer = outline.GetComponent<MeshRenderer>();
                var particles = visual.GetComponentInChildren<ParticleSystem>(true);
                Check(visual.parent == root.transform && visual.localPosition == Vector3.zero, "Field is a centered child of Player");
                Check(visual.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Field child has no independent lifecycle scripts");
                Check(!PrefabUtility.IsAnyPrefabInstanceRoot(visual.gameObject) &&
                      !System.IO.File.Exists("Assets/_Game/Prefabs/Gameplay/DamageField.prefab"), "No independent field prefab remains");
                using var fields = manager.CreateEntityQuery(typeof(DamageFieldComponent));
                Check(fields.CalculateEntityCount() == 1 && fields.GetSingletonEntity() == player,
                    "Field data belongs to the player entity without an extra field entity");
                Check(!visual.gameObject.activeSelf, "Player initializes the field hidden");
                Check(AssetDatabase.GetAssetPath(outline.sharedMesh) == "Assets/_Game/Data/DamageField/Outline.asset" &&
                      outline.sharedMesh.bounds.size == new Vector3(2, 2, 0), "Saved unit ring mesh has exact diameter two");
                foreach (var vertex in outline.sharedMesh.vertices)
                    Check(vertex.magnitude >= .9899f && vertex.magnitude <= 1.0001f, "Outline is a hollow unit ring");
                Check(outline.sharedMesh.colors.All(x => x.g > x.r && x.g < .5f) && renderer.sharedMaterial != null,
                    "Saved ring mesh and material provide the dark green outline");
                Check(particles.shape.shapeType == ParticleSystemShapeType.Circle && particles.shape.radius < 1 &&
                      particles.main.simulationSpace == ParticleSystemSimulationSpace.Local &&
                      particles.main.scalingMode == ParticleSystemScalingMode.Hierarchy, "Particles use the player-local scaled disk");
                Check(particles.main.maxParticles <= 40 && particles.emission.rateOverTime.constant <= 8, "Particle fill is sparse");
                var texture = (Texture2D)particles.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture;
                var pixels = new Texture2D(64, 64);
                try
                {
                    pixels.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(texture)));
                    Check(pixels.GetPixel(32, 32).a == 0 && pixels.GetPixel(55, 32).a > .5f, "Saved particle texture is hollow");
                }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                var position = manager.GetComponentData<PlayerPositionComponent>(player);
                var field = manager.GetComponentData<DamageFieldComponent>(player);
                // 编辑模式只检查输入绑定；实际按住 Alt 的体验由场景人工验收完成。
                view.Refresh(position, PlayerFormKind.Solid, new PlayerMotionComponent(), field, true);
                Check(visual.gameObject.activeSelf, "PlayerView shows the field");
                view.HideDamageField();
                Check(!visual.gameObject.activeSelf, "PlayerView hides the field");
                foreach (var form in new[] { PlayerFormKind.Solid, PlayerFormKind.Liquid, PlayerFormKind.Gas })
                {
                    manager.SetComponentData(player, new PlayerFormComponent { Value = form });
                    controller.Refresh();
                    Check(visual.localScale == new Vector3(3, 3, 1) && (Vector2)visual.position == new Vector2(2, -1),
                        form + " preserves the field radius and ground center");
                }
                manager.SetComponentData(player, new DamageFieldComponent { Radius = 5, Strength = 25 });
                controller.Refresh();
                Check(visual.localScale == new Vector3(5, 5, 1) && outline.sharedMesh.bounds.size.x == 2,
                    "PlayerController reads ECS radius without rewriting the saved mesh");
                inputs.enabled = false;
                controller.Refresh();
                Check(!visual.gameObject.activeSelf, "Disabled player input hides the field");
                view.Refresh(position, PlayerFormKind.Solid, new PlayerMotionComponent(), field, true);
                controller.Shutdown();
                Check(!controller.enabled && !visual.gameObject.activeSelf, "Player shutdown also hides its field");
                return "Player damage field code/data checks passed: " + passed + ".";
            }
            finally
            {
                controller.Shutdown();
                inputs.Shutdown();
                EditorSceneManager.CloseScene(fixture, true);
                SceneManager.SetActiveScene(previousScene);
            }
        }
    }
}
