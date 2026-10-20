using System;
using System.Reflection;
using Emergence.Gameplay.InteractCallback.Converter;
using Emergence.Gameplay.Interaction;
using Emergence.Gameplay.Player;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using InteractionMode = Emergence.Gameplay.Interaction.InteractionMode;

namespace Emergence.Gameplay.Converter.Editor
{
    public static class ConverterChecks
    {
        [MenuItem("Emergence/Checks/Converter")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            using var world = new World("Converter checks");
            var previousWorld = World.DefaultGameObjectInjectionWorld;
            var root = new GameObject("Converter callback checks");
            try
            {
                World.DefaultGameObjectInjectionWorld = world;
                var manager = world.EntityManager;
                var player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerInputComponent),
                    typeof(PlayerFormComponent), typeof(PlayerPositionComponent), typeof(PlayerMotionComponent), typeof(DeadTagComponent));
                manager.SetComponentEnabled<DeadTagComponent>(player, false);
                manager.SetComponentData(player, new PlayerPositionComponent { XY = new float2(2, 3), Height = 4 });
                manager.SetComponentData(player, new PlayerMotionComponent { Velocity = new float3(5, 6, 7) });
                var point = manager.CreateEntity(typeof(InteractiveComponent), typeof(InteractionStateComponent));
                manager.AddComponentData(point, new InteractionRequirementComponent
                {
                    MaximumHeight = float.MaxValue, AllowedForms = InteractionForms.All
                });
                manager.AddBuffer<InteractionOverlapComponent>(point).Add(new InteractionOverlapComponent { Entity = player });
                var system = world.GetOrCreateSystemManaged<InteractionSystem>();
                var action = root.AddComponent<ConverterAction>();
                var controller = root.AddComponent<InteractionController>();
                controller.Initialize(world, point);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var activated = (UnityEvent)typeof(InteractionController).GetField("onActivated", flags).GetValue(controller);
                activated.AddListener(action.Convert);
                int notifications = 0;
                activated.AddListener(() => notifications++);
                var refresh = typeof(InteractionController).GetMethod("LateUpdate", flags);
                void Tick() { system.Update(); refresh.Invoke(controller, null); }
                int passed = 0;
                void Check(bool condition, string label)
                {
                    if (!condition) throw new InvalidOperationException("FAIL: " + label);
                    passed++;
                }
                var sources = new[] { PlayerFormKind.Solid, PlayerFormKind.Liquid, PlayerFormKind.Gas, PlayerFormKind.Liquid };
                var targets = new[] { PlayerFormKind.Liquid, PlayerFormKind.Gas, PlayerFormKind.Liquid, PlayerFormKind.Solid };
                for (int i = 0; i < sources.Length; i++)
                {
                    typeof(ConverterAction).GetField("conversion", flags).SetValue(action, (ConverterKind)i);
                    for (int form = 0; form < 3; form++)
                    {
                        PlayerSystem.SetForm(manager, player, (PlayerFormKind)form);
                        manager.SetComponentData(point, new InteractionStateComponent());
                        manager.SetComponentData(player, new PlayerInputComponent { InteractPressed = true });
                        int before = notifications;
                        system.Update();
                        Check(manager.GetComponentData<PlayerFormComponent>(player).Value == (PlayerFormKind)form,
                            "InteractionSystem does not execute conversion");
                        refresh.Invoke(controller, null);
                        Check(manager.GetComponentData<PlayerFormComponent>(player).Value ==
                            ((PlayerFormKind)form == sources[i] ? targets[i] : (PlayerFormKind)form), "Callback checks source form");
                        Check(notifications == before + 1, "Interaction callback runs even when conversion is rejected");
                        refresh.Invoke(controller, null);
                        Check(notifications == before + 1, "Activation is consumed once");
                    }
                    PlayerSystem.SetForm(manager, player, sources[i]);
                    manager.SetComponentData(player, new PlayerInputComponent());
                    Tick();
                    Check(manager.GetComponentData<PlayerFormComponent>(player).Value == sources[i], "Manual conversion requires E");
                    manager.SetComponentData(point, new InteractionStateComponent { Mode = InteractionMode.EnterRange });
                    Tick();
                    Check(manager.GetComponentData<PlayerFormComponent>(player).Value == targets[i], "EnterRange invokes conversion");
                    PlayerSystem.SetForm(manager, player, sources[i]);
                    Tick();
                    Check(manager.GetComponentData<PlayerFormComponent>(player).Value == sources[i], "Staying does not repeat");
                    manager.GetBuffer<InteractionOverlapComponent>(point).Clear();
                    Tick();
                    manager.GetBuffer<InteractionOverlapComponent>(point).Add(new InteractionOverlapComponent { Entity = player });
                    Tick();
                    Check(manager.GetComponentData<PlayerFormComponent>(player).Value == targets[i], "Reentry invokes conversion again");
                    PlayerSystem.SetForm(manager, player, sources[i]);
                    manager.SetComponentEnabled<DeadTagComponent>(player, true);
                    action.Convert();
                    Check(manager.GetComponentData<PlayerFormComponent>(player).Value == sources[i], "Dead player cannot convert");
                    manager.SetComponentEnabled<DeadTagComponent>(player, false);
                }
                Check(math.all(manager.GetComponentData<PlayerPositionComponent>(player).XY == new float2(2, 3)) &&
                    manager.GetComponentData<PlayerPositionComponent>(player).Height == 4 &&
                    math.all(manager.GetComponentData<PlayerMotionComponent>(player).Velocity == new float3(5, 6, 7)),
                    "Conversion preserves position, height and velocity");
                controller.Shutdown();
                return passed + " converter callback checks passed.";
            }
            finally
            {
                World.DefaultGameObjectInjectionWorld = previousWorld;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
