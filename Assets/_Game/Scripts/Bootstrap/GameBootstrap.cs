using Emergence.Gameplay.Environment;
using Emergence.Gameplay.Player;
using Emergence.UI.Input;
using Emergence.UI.Player;
using Emergence.WorldView;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerWorldView playerView;
        [SerializeField] private InputSettingsController inputSettings;
        [SerializeField, Min(0)] private float initialHeight;
        [Header("Environment: X/Y wind moves the map position; Z wind changes height")]
        [SerializeField] private Vector3 windVelocity = new Vector3(0, 0, 1);
        [SerializeField, Min(0)] private float gravity = 9.81f;
        private World world;
        private Entity player;
        private Entity environment;

        private void Start()
        {
            if (config == null || !config.IsValid || controller == null || playerView == null || inputSettings == null ||
                !math.all(math.isfinite((float3)windVelocity)) || !math.isfinite(gravity) || gravity < 0 ||
                !math.isfinite(initialHeight) || initialHeight < 0)
            {
                Debug.LogError("Assign valid PlayerConfig, controllers and view. Require Gas < Solid < Liquid, positive response time and finite environment values.", this);
                return;
            }
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("The default ECS world is unavailable.", this);
                return;
            }
            using (var query = world.EntityManager.CreateEntityQuery(typeof(EnvironmentSettings)))
            {
                if (!query.IsEmptyIgnoreFilter)
                {
                    Debug.LogError("This scene must have only one environment owner.", this);
                    return;
                }
            }
            try
            {
                inputSettings.Initialize();
                var manager = world.EntityManager;
                environment = manager.CreateEntity(typeof(EnvironmentSettings));
                manager.SetComponentData(environment, new EnvironmentSettings
                {
                    WindVelocity = windVelocity, Gravity = gravity
                });
                player = manager.CreateEntity(typeof(PlayerTag), typeof(PlayerInput), typeof(PlayerPosition),
                    typeof(PlayerMoveSpeed), typeof(PlayerForm), typeof(PlayerMotionState));
                manager.SetName(player, "Player");
                Vector3 spawn = playerView.transform.position;
                manager.SetComponentData(player, new PlayerPosition { XY = new float2(spawn.x, spawn.y), Height = initialHeight });
                manager.SetComponentData(player, config.CreateSpeed());
                manager.SetComponentData(player, new PlayerForm { Value = config.InitialForm });
                controller.Initialize(world, player, playerView, inputSettings);
            }
            catch (System.Exception exception)
            {
                Cleanup();
                Debug.LogException(exception, this);
            }
        }

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            if (controller != null) controller.Shutdown();
            if (inputSettings != null) inputSettings.Shutdown();
            if (world != null && world.IsCreated)
            {
                var manager = world.EntityManager;
                if (manager.Exists(player)) manager.DestroyEntity(player);
                if (manager.Exists(environment)) manager.DestroyEntity(environment);
            }
            player = Entity.Null;
            environment = Entity.Null;
            world = null;
        }
    }
}
