using System;
using System.IO;
using Emergence.Gameplay.Player;
using Emergence.Gameplay.Respawn;
using Unity.Entities;
using UnityEngine;

namespace Emergence.Gameplay.Save
{
    public static class LevelProgressSystem
    {
        [Serializable]
        private sealed class SaveData
        {
            public string level;
        }

        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");
        public static string SavePath => Path.Combine(SaveDirectory, "progress.json");

        public static bool IsLevelAvailable(string scenePath) =>
            !string.IsNullOrWhiteSpace(scenePath) &&
            scenePath.StartsWith("Assets/_Game/Scenes/Level/", StringComparison.Ordinal) &&
            UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0;

        public static bool TryReadSave(out string level)
        {
            level = string.Empty;
            if (!File.Exists(SavePath)) return false;
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data == null || !IsLevelAvailable(data.level)) return false;
            level = data.level;
            return true;
        }

        public static bool NewGame(EntityManager manager, string firstLevel)
        {
            if (!IsLevelAvailable(firstLevel))
                throw new ArgumentException("New game requires an enabled level scene.", nameof(firstLevel));
            if (IsLoading(manager)) return false;
            WriteSave(firstLevel);
            BeginLoad(manager, firstLevel, false);
            return true;
        }

        public static bool ContinueGame(EntityManager manager, out string level)
        {
            level = string.Empty;
            if (IsLoading(manager) || !TryReadSave(out level)) return false;
            BeginLoad(manager, level, true);
            return true;
        }

        private static bool IsLoading(EntityManager manager)
        {
            using var query = manager.CreateEntityQuery(typeof(LevelProgressComponent));
            return !query.IsEmptyIgnoreFilter && query.GetSingleton<LevelProgressComponent>().IsLoading;
        }

        private static void BeginLoad(EntityManager manager, string level, bool resumeAtRespawn)
        {
            using var query = manager.CreateEntityQuery(typeof(LevelProgressComponent));
            var entity = query.IsEmptyIgnoreFilter ? manager.CreateEntity(typeof(LevelProgressComponent)) : query.GetSingletonEntity();
            manager.SetComponentData(entity, new LevelProgressComponent
            {
                Level = level, IsLoading = true, ResumeAtRespawn = resumeAtRespawn
            });
        }

        // 在物理注册和 Controller 装配前，直接从 ECS 的复活点读取权威位置。
        public static void ApplyLoadedSpawn(EntityManager manager, Entity player, string scenePath)
        {
            using var progress = manager.CreateEntityQuery(typeof(LevelProgressComponent));
            if (progress.IsEmptyIgnoreFilter) return;
            var state = progress.GetSingleton<LevelProgressComponent>();
            if (!state.IsLoading) return;
            if (state.Level.ToString() != scenePath)
                throw new InvalidOperationException("Loaded level does not match the save request: " + scenePath);
            if (!state.ResumeAtRespawn) return;
            using var points = manager.CreateEntityQuery(typeof(RespawnPointComponent), typeof(RespawnEnabledTagComponent));
            if (points.CalculateEntityCount() != 1)
                throw new InvalidOperationException(scenePath + ": continuing a game requires exactly one enabled respawn point.");
            var point = points.GetSingleton<RespawnPointComponent>();
            manager.SetComponentData(player, new PlayerPositionComponent { XY = point.XY, Height = point.Height });
        }

        // 总门控完成关卡装配后调用；直接运行测试场景不创建或改写存档。
        public static void EnterLevel(EntityManager manager, string scenePath)
        {
            using var query = manager.CreateEntityQuery(typeof(LevelProgressComponent));
            if (query.IsEmptyIgnoreFilter) return;
            if (!IsLevelAvailable(scenePath)) throw new InvalidOperationException("Progress requires an enabled level scene: " + scenePath);
            var state = query.GetSingleton<LevelProgressComponent>();
            if (state.IsLoading && state.Level.ToString() != scenePath)
                throw new InvalidOperationException("Cannot complete a save request in a different level.");
            if (state.Level.ToString() != scenePath) WriteSave(scenePath);
            manager.SetComponentData(query.GetSingletonEntity(), new LevelProgressComponent { Level = scenePath });
        }

        private static void WriteSave(string level)
        {
            Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(SavePath, JsonUtility.ToJson(new SaveData { level = level }, true));
        }
    }
}
