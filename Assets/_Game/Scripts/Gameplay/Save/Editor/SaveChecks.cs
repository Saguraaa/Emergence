using System;
using System.IO;
using System.Text.RegularExpressions;
using Emergence.Gameplay.Player;
using Emergence.Gameplay.Respawn;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Emergence.Gameplay.Save.Editor
{
    public static class SaveChecks
    {
        [MenuItem("Emergence/Checks/Save")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            const string tutorial = "Assets/_Game/Scenes/Level/Tutorial01.unity";
            const string pipe = "Assets/_Game/Scenes/Level/PipeMap.unity";
            string path = LevelProgressSystem.SavePath;
            bool hadDirectory = Directory.Exists(LevelProgressSystem.SaveDirectory);
            byte[] original = File.Exists(path) ? File.ReadAllBytes(path) : null;
            int passed = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + label);
                passed++;
            }
            using var world = new World("Save checks");
            var manager = world.EntityManager;
            try
            {
                if (File.Exists(path)) File.Delete(path);
                Check(!LevelProgressSystem.TryReadSave(out _), "An empty save folder has no continuation");
                Check(!LevelProgressSystem.ContinueGame(manager, out _), "Continue cannot start without a save");
                LevelProgressSystem.EnterLevel(manager, tutorial);
                Check(!File.Exists(path), "Direct scene testing does not create saves");
                Check(LevelProgressSystem.NewGame(manager, tutorial) && File.Exists(path), "New game creates a save immediately");
                Check(LevelProgressSystem.TryReadSave(out string saved) && saved == tutorial, "New save selects the first level");
                string json = File.ReadAllText(path);
                Check(Regex.Matches(json, "\"[^\"]+\"\\s*:").Count == 1 && json.Contains("\"level\""), "Only the level is persisted");
                Check(!LevelProgressSystem.NewGame(manager, pipe), "Repeated clicks cannot replace a pending load");
                using var progress = manager.CreateEntityQuery(typeof(LevelProgressComponent));
                Check(progress.CalculateEntityCount() == 1, "One ECS progress owner");
                var player = manager.CreateEntity(typeof(PlayerPositionComponent));
                var initial = new PlayerPositionComponent { XY = new float2(20, 30), Height = 4 };
                manager.SetComponentData(player, initial);
                LevelProgressSystem.ApplyLoadedSpawn(manager, player, tutorial);
                Check(math.all(manager.GetComponentData<PlayerPositionComponent>(player).XY == initial.XY), "New game retains the scene's initial spawn");
                LevelProgressSystem.EnterLevel(manager, tutorial);
                Check(!progress.GetSingleton<LevelProgressComponent>().IsLoading, "Level initialization completes the request");
                LevelProgressSystem.EnterLevel(manager, pipe);
                Check(LevelProgressSystem.TryReadSave(out saved) && saved == pipe, "Entering a different level updates the save");
                Check(LevelProgressSystem.ContinueGame(manager, out saved) && saved == pipe, "Continue reads the saved level");
                Check(!LevelProgressSystem.ContinueGame(manager, out _), "Repeated continue does not duplicate requests");
                bool rejected = false;
                try { LevelProgressSystem.ApplyLoadedSpawn(manager, player, tutorial); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "The wrong destination is rejected");
                rejected = false;
                try { LevelProgressSystem.ApplyLoadedSpawn(manager, player, pipe); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Continue requires an enabled respawn point");
                var point = manager.CreateEntity(typeof(RespawnPointComponent), typeof(RespawnEnabledTagComponent));
                manager.SetComponentData(point, new RespawnPointComponent { XY = new float2(2, 7), Height = 3 });
                var otherPoint = manager.CreateEntity(typeof(RespawnPointComponent), typeof(RespawnEnabledTagComponent));
                rejected = false;
                try { LevelProgressSystem.ApplyLoadedSpawn(manager, player, pipe); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Multiple enabled respawn points are rejected");
                RespawnSystem.SetEnabled(manager, otherPoint, false);
                LevelProgressSystem.ApplyLoadedSpawn(manager, player, pipe);
                var position = manager.GetComponentData<PlayerPositionComponent>(player);
                Check(math.all(position.XY == new float2(2, 7)) && position.Height == 3, "Continue uses ECS respawn XY and Height");
                LevelProgressSystem.EnterLevel(manager, pipe);
                Check(LevelProgressSystem.NewGame(manager, tutorial) && LevelProgressSystem.TryReadSave(out saved) && saved == tutorial,
                    "A new game resets the automatic save");
                LevelProgressSystem.EnterLevel(manager, tutorial);
                File.WriteAllText(path, "{\"level\":\"Assets/_Game/Scenes/Level/Missing.unity\"}");
                Check(!LevelProgressSystem.TryReadSave(out _) && !LevelProgressSystem.ContinueGame(manager, out _),
                    "Unavailable saved levels cannot be continued");
            }
            finally
            {
                if (original != null) File.WriteAllBytes(path, original);
                else if (File.Exists(path)) File.Delete(path);
                if (!hadDirectory && Directory.Exists(LevelProgressSystem.SaveDirectory) &&
                    Directory.GetFileSystemEntries(LevelProgressSystem.SaveDirectory).Length == 0)
                    Directory.Delete(LevelProgressSystem.SaveDirectory);
            }
            return passed + " save checks passed; original save restored.";
        }
    }
}
