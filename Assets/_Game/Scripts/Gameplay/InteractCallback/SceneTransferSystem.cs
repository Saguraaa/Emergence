using System;
using System.Collections.Generic;
using Emergence.Gameplay.Player;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emergence.Gameplay.InteractCallback
{
    public static class SceneTransferSystem
    {
        public static bool IsPending(EntityManager manager)
        {
            using var query = manager.CreateEntityQuery(typeof(SceneTransferComponent));
            return !query.IsEmptyIgnoreFilter;
        }

        public static bool Begin(World world, string scenePath, string pointId)
        {
            if (world == null || !world.IsCreated) throw new InvalidOperationException("Scene transfer requires a live World.");
            if (string.IsNullOrWhiteSpace(scenePath) || string.IsNullOrWhiteSpace(pointId))
                throw new ArgumentException("Scene transfer requires a destination scene and point ID.");
            var request = new SceneTransferComponent { DestinationScene = scenePath, DestinationPoint = pointId };
            var manager = world.EntityManager;
            if (IsPending(manager)) return false;
            using var players = manager.CreateEntityQuery(typeof(PlayerTagComponent));
            if (players.CalculateEntityCount() != 1)
                throw new InvalidOperationException("Scene transfer requires exactly one player.");
            var fixedStep = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            if (fixedStep == null) throw new InvalidOperationException("Scene transfer requires the fixed-step group.");
            manager.SetComponentData(manager.CreateEntity(typeof(SceneTransferComponent)), request);
            // 场景依赖解绑期间，所有固定步规则暂停，玩家状态不会被旧场景继续更新。
            fixedStep.Enabled = false;
            return true;
        }

        public static void Arrive(World world, Entity player, Scene scene)
        {
            var manager = world.EntityManager;
            using var query = manager.CreateEntityQuery(typeof(SceneTransferComponent));
            var request = query.GetSingleton<SceneTransferComponent>();
            if (request.DestinationScene.ToString() != scene.path)
                throw new InvalidOperationException("Loaded scene does not match the transfer destination: " + scene.path);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Transform anchor = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var point in root.GetComponentsInChildren<SceneTransferPoint>(true))
                {
                    point.Validate();
                    if (!ids.Add(point.PointId))
                        throw new InvalidOperationException(scene.name + ": duplicate transfer point ID " + point.PointId);
                    if (point.PointId == request.DestinationPoint.ToString()) anchor = point.Anchor;
                }
            if (anchor == null)
                throw new InvalidOperationException(scene.name + ": transfer point not found: " + request.DestinationPoint);
            var position = manager.GetComponentData<PlayerPositionComponent>(player);
            position.XY = new float2(anchor.position.x, anchor.position.y);
            manager.SetComponentData(player, position);
        }

        public static void Complete(World world, string scenePath)
        {
            var manager = world.EntityManager;
            using var query = manager.CreateEntityQuery(typeof(SceneTransferComponent));
            if (query.IsEmptyIgnoreFilter) return;
            if (query.GetSingleton<SceneTransferComponent>().DestinationScene.ToString() != scenePath)
                throw new InvalidOperationException("Cannot complete transfer in a different scene.");
            manager.DestroyEntity(query.GetSingletonEntity());
            world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Enabled = true;
        }
    }
}
