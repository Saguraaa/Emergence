using System;
using Emergence.Gameplay.Environment;
using Emergence.Gameplay.InteractCallback;
using Emergence.Gameplay.Physics;
using Emergence.UI.Input;
using Emergence.Gameplay.Player;
using Emergence.Gameplay.Save;
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
        [SerializeField, Min(0)] private float initialHeight;
        [SerializeField] private Rigidbody2D physicsBody;
        [SerializeField] private BoxCollider2D solidCollider;
        [SerializeField] private CircleCollider2D liquidCollider;
        [SerializeField] private CircleCollider2D gasCollider;
        [SerializeField] private LayerMask collisionLayers;
        [SerializeField, Min(0.01f)] private float damageFieldRadius = 3;
        [SerializeField, Min(0)] private float damageFieldStrength = 25;
        private PhysicsSystem physics;
        private World world;
        private Entity player;

        public void Initialize(InputSettingsController inputSettings)
        {
            // 校验
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
            if (physicsBody == null) throw new MissingReferenceException(name + ": physicsBody is required.");
            if (solidCollider == null) throw new MissingReferenceException(name + ": solidCollider is required.");
            if (liquidCollider == null) throw new MissingReferenceException(name + ": liquidCollider is required.");
            if (gasCollider == null) throw new MissingReferenceException(name + ": gasCollider is required.");
            if (!math.isfinite(initialHeight) || initialHeight < 0)
                throw new InvalidOperationException(name + ": initialHeight must be finite and nonnegative.");
            if (!Enum.IsDefined(typeof(PlayerFormKind), initialForm))
                throw new InvalidOperationException(name + ": initialForm is invalid.");
            if (!math.isfinite(damageFieldRadius) || damageFieldRadius <= 0 ||
                !math.isfinite(damageFieldStrength) || damageFieldStrength < 0)
                throw new InvalidOperationException(name + ": damage field radius must be positive and strength nonnegative; both must be finite.");
            var resolvedWorld = World.DefaultGameObjectInjectionWorld;
            if (resolvedWorld == null || !resolvedWorld.IsCreated) throw new InvalidOperationException("Default ECS World is unavailable.");
            var manager = resolvedWorld.EntityManager;
            var resolvedPhysics = resolvedWorld.GetExistingSystemManaged<PhysicsSystem>();
            if (resolvedPhysics == null) throw new InvalidOperationException("Default ECS World requires PhysicsSystem.");
            using var environmentQuery = manager.CreateEntityQuery(typeof(EnvironmentComponent));
            if (environmentQuery.CalculateEntityCount() != 1)
                throw new InvalidOperationException("Initialize exactly one environment before the player.");
            Vector3 spawn = playerView.transform.position;
            if (!math.all(math.isfinite((float3)spawn))) throw new InvalidOperationException(name + ": player spawn must be finite.");

            // 初始化
            world = resolvedWorld;
            physics = resolvedPhysics;
            using var players = manager.CreateEntityQuery(typeof(PlayerTagComponent));
            if (players.IsEmptyIgnoreFilter)
            {
                if (SceneTransferSystem.IsPending(manager))
                    throw new InvalidOperationException(name + ": transferred player entity is missing.");
                player = manager.CreateEntity(typeof(PlayerTagComponent), typeof(PlayerInputComponent),
                    typeof(PlayerPositionComponent), typeof(PlayerMotionComponent), typeof(PlayerFormComponent),
                    typeof(PlayerSolidComponent), typeof(PlayerLiquidComponent), typeof(PlayerGasComponent));
                manager.SetName(player, "Player");
                manager.AddComponent<DeadTagComponent>(player);
                manager.SetComponentEnabled<DeadTagComponent>(player, false);
                manager.AddBuffer<PhysicsContactComponent>(player);
                manager.AddComponentData(player, new DamageFieldComponent { Radius = damageFieldRadius, Strength = damageFieldStrength });
                manager.SetComponentData(player, new PlayerPositionComponent
                {
                    XY = new float2(spawn.x, spawn.y), Height = initialHeight
                });
                // 初始配置只在首次创建实体时应用，传送不重置任何玩家参数。
                manager.SetComponentData(player, solidConfig.CreateComponent());
                manager.SetComponentData(player, liquidConfig.CreateComponent());
                manager.SetComponentData(player, gasConfig.CreateComponent());
                PlayerSystem.SetForm(manager, player, initialForm);
            }
            else
            {
                if (players.CalculateEntityCount() != 1 || !SceneTransferSystem.IsPending(manager))
                    throw new InvalidOperationException(name + ": only a pending transfer may rebind the existing player.");
                player = players.GetSingletonEntity();
                SceneTransferSystem.Arrive(world, player, gameObject.scene);
            }
            LevelProgressSystem.ApplyLoadedSpawn(manager, player, gameObject.scene.path);
            int shape = (int)manager.GetComponentData<PlayerFormComponent>(player).Value;
            physics.RegisterBody(player, physicsBody, new Collider2D[] { solidCollider, liquidCollider, gasCollider }, collisionLayers, shape);
            controller.Initialize(world, player, playerView, inputSettings);
        }

        public void Shutdown()
        {
            // 先停止 Controller 的逐帧访问，再销毁实体。
            bool transferring = world != null && world.IsCreated && SceneTransferSystem.IsPending(world.EntityManager);
            if (controller != null) controller.Shutdown(transferring);
            if (world != null && world.IsCreated && player != Entity.Null)
            {
                physics.UnregisterBody(player);
                if (!transferring) world.EntityManager.DestroyEntity(player);
            }
            player = Entity.Null;
            world = null;
            physics = null;
        }
    }
}
