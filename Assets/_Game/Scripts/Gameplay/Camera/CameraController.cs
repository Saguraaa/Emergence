using System;
using Emergence.Gameplay.Player;
using Emergence.Gameplay.Respawn;
using Emergence.WorldView;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Gameplay.Camera
{
    public sealed class CameraController : MonoBehaviour
    {
        private World world;
        private Entity player;
        private CameraView view;
        private RespawnSystem respawn;
        private float maximumDisplayY;

        public void Initialize(World targetWorld, Entity entity, CameraView cameraView, float maximumDisplayY = float.MaxValue)
        {
            enabled = false;
            if (targetWorld == null || !targetWorld.IsCreated || !targetWorld.EntityManager.Exists(entity) ||
                !targetWorld.EntityManager.HasComponent<PlayerPositionComponent>(entity))
                throw new ArgumentException(name + ": camera requires a live player with PlayerPositionComponent.");
            if (cameraView == null) throw new MissingReferenceException(name + ": CameraView is required.");
            if (!math.isfinite(maximumDisplayY))
                throw new InvalidOperationException(name + ": maximumDisplayY must be finite.");
            respawn = targetWorld.GetExistingSystemManaged<RespawnSystem>();
            if (respawn == null) throw new InvalidOperationException(name + ": RespawnSystem is required.");
            world = targetWorld;
            player = entity;
            view = cameraView;
            this.maximumDisplayY = maximumDisplayY;
            respawn.Respawned += OnRespawned;
            view.Snap(ReadTarget());
            enabled = true;
        }

        private Vector2 ReadTarget()
        {
            var position = world.EntityManager.GetComponentData<PlayerPositionComponent>(player);
            return new Vector2(position.XY.x, Mathf.Min(position.XY.y + position.Height, maximumDisplayY));
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
