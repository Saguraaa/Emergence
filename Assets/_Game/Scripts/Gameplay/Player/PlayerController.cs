using System;
using Emergence.Gameplay.Respawn;
using Emergence.UI.Input;
using Emergence.WorldView;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Emergence.Gameplay.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private InputActionReference showDamageFieldAction;
        private World world;
        private Entity player;
        private PlayerView view;
        private InputSettingsController inputs;
        private InputAction move;
        private InputAction interact;
        private InputAction showDamageField;
        private RespawnSystem respawn;

        public void Initialize(World targetWorld, Entity entity, PlayerView playerView,
            InputSettingsController inputSettings)
        {
            // 校验
            if (targetWorld == null || !targetWorld.IsCreated || !targetWorld.EntityManager.Exists(entity))
                throw new ArgumentException(name + ": PlayerController requires a live player entity.");
            if (playerView == null) throw new MissingReferenceException(name + ": PlayerView is required.");
            if (inputSettings == null) throw new MissingReferenceException(name + ": inputSettings is required.");
            if (moveAction == null) throw new MissingReferenceException(name + ": moveAction is required.");
            if (interactAction == null) throw new MissingReferenceException(name + ": interactAction is required.");
            if (showDamageFieldAction == null) throw new MissingReferenceException(name + ": showDamageFieldAction is required.");
            var resolvedMove = inputSettings.GetAction(moveAction);
            var resolvedInteract = inputSettings.GetAction(interactAction);
            var resolvedShowDamageField = inputSettings.GetAction(showDamageFieldAction);
            if (resolvedMove is not { expectedControlType: "Vector2", type: not InputActionType.Button })
                throw new InvalidOperationException(name + ": moveAction must provide Vector2 input.");
            if (resolvedInteract.type != InputActionType.Button)
                throw new InvalidOperationException(name + ": interactAction must be a Button action.");
            if (resolvedShowDamageField.type != InputActionType.Button)
                throw new InvalidOperationException(name + ": showDamageFieldAction must be a Button action.");
            var manager = targetWorld.EntityManager;
            if (!manager.HasComponent<PlayerInputComponent>(entity) ||
                !manager.HasComponent<PlayerPositionComponent>(entity) || !manager.HasComponent<PlayerMotionComponent>(entity) ||
                !manager.HasComponent<PlayerFormComponent>(entity) || !manager.HasComponent<DamageFieldComponent>(entity) ||
                !manager.HasComponent<DeadTagComponent>(entity))
                throw new ArgumentException(name + ": player entity is missing required components.");
            var respawnSystem = targetWorld.GetExistingSystemManaged<RespawnSystem>();
            if (respawnSystem == null) throw new InvalidOperationException(name + ": RespawnSystem is required.");
            var form = manager.GetComponentData<PlayerFormComponent>(entity).Value;

            // 初始化
            playerView.Initialize(form);
            world = targetWorld;
            player = entity;
            view = playerView;
            inputs = inputSettings;
            move = resolvedMove;
            interact = resolvedInteract;
            showDamageField = resolvedShowDamageField;
            respawn = respawnSystem;
            respawn.Respawned += OnRespawned;
            interact.performed += OnInteract;
            view.Refresh(manager.GetComponentData<PlayerPositionComponent>(player), form,
                manager.GetComponentData<PlayerMotionComponent>(player), manager.GetComponentData<DamageFieldComponent>(player), false);
            enabled = true;
        }

        private void Update()
        {
            bool active = inputs.isActiveAndEnabled && !inputs.IsRebinding;
            Vector2 value = active ? move.ReadValue<Vector2>() : Vector2.zero;
            var request = world.EntityManager.GetComponentData<PlayerInputComponent>(player);
            request.Move = new float2(value.x, value.y);
            if (!active) request.InteractPressed = false;
            world.EntityManager.SetComponentData(player, request);
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            if (!isActiveAndEnabled || !inputs.isActiveAndEnabled || inputs.IsRebinding) return;
            var request = world.EntityManager.GetComponentData<PlayerInputComponent>(player);
            request.InteractPressed = true;
            world.EntityManager.SetComponentData(player, request);
        }

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            var manager = world.EntityManager;
            bool showField = inputs.isActiveAndEnabled && !inputs.IsRebinding && showDamageField.IsPressed() &&
                             !manager.IsComponentEnabled<DeadTagComponent>(player);
            view.Refresh(manager.GetComponentData<PlayerPositionComponent>(player),
                manager.GetComponentData<PlayerFormComponent>(player).Value, manager.GetComponentData<PlayerMotionComponent>(player),
                manager.GetComponentData<DamageFieldComponent>(player), showField);
        }

        private void OnRespawned(Entity entity)
        {
            if (entity == player && isActiveAndEnabled) view.PlayRespawn();
        }

        private void OnDisable()
        {
            // World 可能先于对象销毁；只在生命周期边界校验有效性。
            if (world != null && world.IsCreated && player != Entity.Null)
            {
                world.EntityManager.SetComponentData(player, new PlayerInputComponent());
                view.HideDamageField();
            }
        }


        public void Shutdown()
        {
            enabled = false;
            if (view != null) view.HideDamageField();
            if (respawn != null) respawn.Respawned -= OnRespawned;
            respawn = null;
            if (interact != null) interact.performed -= OnInteract;
            move = null;
            interact = null;
            showDamageField = null;
            inputs = null;
            world = null;
            player = Entity.Null;
            view = null;
        }
    }
}
