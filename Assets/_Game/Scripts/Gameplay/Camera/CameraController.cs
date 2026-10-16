using System;
using Emergence.Gameplay.Player;
using Emergence.Gameplay.Respawn;
using Emergence.WorldView;
using Unity.Entities;
using UnityEngine;

namespace Emergence.Gameplay.Camera
{
    public sealed class CameraController : MonoBehaviour
    {
        private World world;
        private Entity player;
        private CameraView view;
        private RespawnSystem respawn;

        public void Initialize(World targetWorld, Entity entity, CameraView cameraView)
        {
            enabled = false;
            if (targetWorld == null || !targetWorld.IsCreated || !targetWorld.EntityManager.Exists(entity) ||
                !targetWorld.EntityManager.HasComponent<PlayerPositionComponent>(entity))
                throw new ArgumentException(name + ": camera requires a live player with PlayerPositionComponent.");
            if (cameraView == null) throw new MissingReferenceException(name + ": CameraView is required.");
            respawn = targetWorld.GetExistingSystemManaged<RespawnSystem>();
            if (respawn == null) throw new InvalidOperationException(name + ": RespawnSystem is required.");
            world = targetWorld;
            player = entity;
            view = cameraView;
            respawn.Respawned += OnRespawned;
            view.Snap(ReadTarget());
            enabled = true;
        }

        private Vector2 ReadTarget()
        {
            var position = world.EntityManager.GetComponentData<PlayerPositionComponent>(player);
            return new Vector2(position.XY.x, position.XY.y + position.Height);
        }

        private void LateUpdate() => view.Refresh(ReadTarget(), Time.deltaTime);

        private void OnRespawned(Entity entity)
        {
            if (entity == player) view.Snap(ReadTarget());
        }

        public void Shutdown()
        {
            enabled = false;
            if (respawn != null) respawn.Respawned -= OnRespawned;
            respawn = null;
            view = null;
            world = null;
            player = Entity.Null;
        }
    }
}
