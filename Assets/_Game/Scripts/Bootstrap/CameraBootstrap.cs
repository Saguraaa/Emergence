using System;
using Emergence.Gameplay.Camera;
using Emergence.Gameplay.Player;
using Emergence.WorldView;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Bootstrap
{
    public sealed class CameraBootstrap : MonoBehaviour
    {
        [SerializeField] private CameraController controller;
        [SerializeField] private CameraView cameraView;
        [Tooltip("高度跟随比例：0 跟随地面，1 跟随主体，0.5 兼顾主体和影子。")]
        [SerializeField, Range(0, 1)] private float heightFollowRatio = 1;

        public void Initialize()
        {
            if (controller == null) throw new MissingReferenceException(name + ": controller is required.");
            if (cameraView == null) throw new MissingReferenceException(name + ": cameraView is required.");
            if (!math.isfinite(heightFollowRatio) || heightFollowRatio < 0 || heightFollowRatio > 1)
                throw new InvalidOperationException(name + ": heightFollowRatio must be finite and between 0 and 1.");
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) throw new InvalidOperationException(name + ": Default ECS World is unavailable.");
            using var query = world.EntityManager.CreateEntityQuery(typeof(PlayerTagComponent), typeof(PlayerPositionComponent));
            if (query.CalculateEntityCount() != 1)
                throw new InvalidOperationException(name + ": initialize exactly one player before the camera.");
            var playerViews = FindObjectsByType<PlayerView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (playerViews.Length != 1)
                throw new InvalidOperationException(name + ": camera requires exactly one PlayerView.");
            cameraView.Initialize();
            controller.Initialize(world, query.GetSingletonEntity(), cameraView,
                playerViews[0].MaximumDisplayY, playerViews[0].HeightDisplayScale, heightFollowRatio);
        }

        public void Shutdown() => controller.Shutdown();
    }
}
