using Emergence.Gameplay.Player;
using Emergence.UI.Input;
using Emergence.WorldView;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = Emergence.Gameplay.Player.PlayerInput;

namespace Emergence.UI.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        private World world;
        private Entity player;
        private PlayerWorldView view;
        private InputSettingsController inputs;
        private InputAction move;
        private InputAction interact;

        public void Initialize(World targetWorld, Entity entity, PlayerWorldView worldView,
            InputSettingsController inputSettings)
        {
            Shutdown();
            world = targetWorld;
            player = entity;
            view = worldView;
            inputs = inputSettings;
            move = inputs.GetAction("Player/Move");
            interact = inputs.GetAction("Player/Interact");
            interact.performed += OnInteract;
        }

        private bool HasPlayer => world != null && world.IsCreated && world.EntityManager.Exists(player);

        private void Update()
        {
            if (!HasPlayer) return;
            bool active = inputs != null && inputs.isActiveAndEnabled && !inputs.IsRebinding;
            Vector2 value = active ? move.ReadValue<Vector2>() : Vector2.zero;
            var request = world.EntityManager.GetComponentData<PlayerInput>(player);
            request.Move = new float2(value.x, value.y);
            if (!active) request.InteractPressed = false;
            world.EntityManager.SetComponentData(player, request);
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            if (!HasPlayer || !isActiveAndEnabled || inputs == null ||
                !inputs.isActiveAndEnabled || inputs.IsRebinding) return;
            var request = world.EntityManager.GetComponentData<PlayerInput>(player);
            request.InteractPressed = true;
            world.EntityManager.SetComponentData(player, request);
        }

        private void LateUpdate()
        {
            if (!HasPlayer || view == null) return;
            var manager = world.EntityManager;
            view.Refresh(manager.GetComponentData<PlayerPosition>(player),
                manager.GetComponentData<PlayerForm>(player).Value,
                manager.GetComponentData<PlayerMotionState>(player));
        }

        private void ClearInput()
        {
            if (HasPlayer) world.EntityManager.SetComponentData(player, new PlayerInput());
        }

        private void OnDisable() => ClearInput();
        private void OnDestroy() => Shutdown();

        public void Shutdown()
        {
            if (interact != null) interact.performed -= OnInteract;
            ClearInput();
            move = null;
            interact = null;
            inputs = null;
            world = null;
            player = Entity.Null;
            view = null;
        }
    }
}
