using System;
using System.Collections.Generic;
using Emergence.Gameplay.Physics;
using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Player;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Emergence.Gameplay.Physics.Editor
{
    public sealed class PhysicsChecksScene : IDisposable
    {
        private readonly Scene scene;
        private readonly Scene previousScene;
        private readonly List<Entity> bodies = new();
        private readonly List<Entity> obstacles = new();
        public PhysicsSystem Physics { get; }
        public BoxCollider2D Solid { get; private set; }
        public CircleCollider2D Liquid { get; private set; }
        public CircleCollider2D Gas { get; private set; }

        public PhysicsChecksScene(World world)
        {
            previousScene = SceneManager.GetActiveScene();
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Physics = world.GetOrCreateSystemManaged<PhysicsSystem>();
        }

        public void AddBody(EntityManager manager, Entity entity)
        {
            if (!manager.HasComponent<PlayerPositionComponent>(entity)) manager.AddComponent<PlayerPositionComponent>(entity);
            var root = new GameObject("Physics checks player");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = 31;
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            Solid = root.AddComponent<BoxCollider2D>();
            Solid.size = new Vector2(0.8f, 0.8f);
            Liquid = root.AddComponent<CircleCollider2D>();
            Liquid.radius = 0.4f;
            Gas = root.AddComponent<CircleCollider2D>();
            Gas.radius = 0.6f;
            manager.AddBuffer<PhysicsContactComponent>(entity);
            Physics.RegisterBody(entity, body, new Collider2D[] { Solid, Liquid, Gas }, 1 << 30);
            bodies.Add(entity);
        }

        public BoxCollider2D AddObstacle(Entity entity, Vector2 position, Vector2 size, float height = 1)
        {
            Physics.EntityManager.AddComponentData(entity, new ObstacleHeightComponent { Height = height });
            var root = new GameObject("Physics checks obstacle");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = 30;
            root.transform.position = position;
            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = size;
            Physics.RegisterObstacle(entity, collider);
            obstacles.Add(entity);
            return collider;
        }

        public void Dispose()
        {
            foreach (var entity in bodies) Physics.UnregisterBody(entity);
            foreach (var entity in obstacles) Physics.UnregisterObstacle(entity);
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previousScene);
        }
    }
}
