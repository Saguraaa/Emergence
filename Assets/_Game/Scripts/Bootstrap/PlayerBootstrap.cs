using System;
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
    public sealed class PlayerBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerSolidConfig solidConfig;
        [SerializeField] private PlayerLiquidConfig liquidConfig;
        [SerializeField] private PlayerGasConfig gasConfig;
        [SerializeField] private PlayerFormKind initialForm = PlayerFormKind.Solid;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerView playerView;
        [SerializeField] private InputSettingsController inputSettings;
        [SerializeField, Min(0)] private float initialHeight;
        private World world;
        private Entity player;

        public void Initialize()
        {
            if (solidConfig == null) throw new MissingReferenceException(name + ": solidConfig is required.");
            if (liquidConfig == null) throw new MissingReferenceException(name + ": liquidConfig is required.");
            if (gasConfig == null) throw new MissingReferenceException(name + ": gasConfig is required.");
            if (!solidConfig.IsValid || !liquidConfig.IsValid || !gasConfig.IsValid)
                throw new InvalidOperationException(name + ": form parameters must be finite and positive.");
            if (!(gasConfig.MoveVelocityMagnitude < solidConfig.MoveVelocityMagnitude &&
                solidConfig.MoveVelocityMagnitude < liquidConfig.MoveVelocityMagnitude))
                throw new InvalidOperationException(name + ": require Gas < Solid < Liquid movement magnitude.");
            if (controller == null) throw new MissingReferenceException(name + ": controller is required.");
            if (playerView == null) throw new MissingReferenceException(name + ": playerView is required.");
            if (inputSettings == null) throw new MissingReferenceException(name + ": inputSettings is required.");
            if (!math.isfinite(initialHeight) || initialHeight < 0)
                throw new InvalidOperationException(name + ": initialHeight must be finite and nonnegative.");
            if (!Enum.IsDefined(typeof(PlayerFormKind), initialForm))
                throw new InvalidOperationException(name + ": initialForm is invalid.");
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) throw new InvalidOperationException("Default ECS World is unavailable.");
            var manager = world.EntityManager;
            using var environmentQuery = manager.CreateEntityQuery(typeof(EnvironmentComponent));
            if (environmentQuery.CalculateEntityCount() != 1)
                throw new InvalidOperationException("Initialize exactly one environment before the player.");
            player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerInputComponent),
                typeof(PlayerPositionComponent), typeof(PlayerMotionComponent), typeof(PlayerFormComponent),
                typeof(PlayerSolidComponent), typeof(PlayerLiquidComponent), typeof(PlayerGasComponent));
            manager.SetName(player, "Player");
            Vector3 spawn = playerView.transform.position;
            manager.SetComponentData(player, new PlayerPositionComponent
            {
                XY = new float2(spawn.x, spawn.y), Height = initialHeight
            });
            // Config 只提供初值，运行中的权威数据由 ECS 持有。
            manager.SetComponentData(player, solidConfig.CreateComponent());
            manager.SetComponentData(player, liquidConfig.CreateComponent());
            manager.SetComponentData(player, gasConfig.CreateComponent());
            PlayerSystem.SetForm(manager, player, initialForm);
            controller.Initialize(world, player, playerView, inputSettings);
        }

        public void Shutdown()
        {
            // 先停止 Controller 的逐帧访问，再销毁实体。
            if (controller != null) controller.Shutdown();
            if (world != null && world.IsCreated && player != Entity.Null)
                world.EntityManager.DestroyEntity(player);
            player = Entity.Null;
            world = null;
        }
    }
}
