using System;
using System.Collections.Generic;
using Emergence.Gameplay.Obstacle;
using Emergence.Gameplay.Interaction;
using Emergence.Gameplay.Player;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emergence.Gameplay.Physics
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerSystem))]
    [UpdateBefore(typeof(ObstacleSystem))]
    public partial class PhysicsSystem : SystemBase
    {
        private const float ContactDistance = 0.002f;
        private const int MaxIterations = 4;
        private readonly Dictionary<Entity, Body> bodies = new();
        private readonly Dictionary<Collider2D, Entity> entities = new();
        private readonly Dictionary<Entity, Collider2D> obstacles = new();
        private readonly Dictionary<Entity, (Collider2D Collider, ContactFilter2D Filter)> interactions = new();
        private readonly List<RaycastHit2D> hits = new(16);
        private readonly List<Collider2D> overlaps = new(16);

        private sealed class Body
        {
            public Rigidbody2D Rigidbody;
            public Collider2D[] Shapes;
            public Collider2D ActiveShape;
            public ContactFilter2D Filter;
            public PhysicsScene2D Scene;
        }

        public void RegisterBody(Entity entity, Rigidbody2D rigidbody, Collider2D[] shapes, LayerMask collisionLayers)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasBuffer<PhysicsContactComponent>(entity))
                throw new ArgumentException("Physics body requires a live entity with PhysicsContactComponent.");
            if (rigidbody == null) throw new MissingReferenceException("Physics body requires Rigidbody2D.");
            if (rigidbody.bodyType != RigidbodyType2D.Kinematic || !rigidbody.simulated)
                throw new InvalidOperationException(rigidbody.name + ": Rigidbody2D must be simulated and Kinematic.");
            if (shapes == null || shapes.Length == 0) throw new ArgumentException(rigidbody.name + ": shapes are required.");
            if (collisionLayers.value == 0) throw new ArgumentException(rigidbody.name + ": collisionLayers must not be empty.");
            foreach (var shape in shapes)
            {
                ValidateCollider(shape);
                if (shape.attachedRigidbody != rigidbody)
                    throw new InvalidOperationException(shape.name + ": collider must belong to the registered Rigidbody2D.");
            }
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(collisionLayers);
            bodies.Add(entity, new Body
            {
                Rigidbody = rigidbody, Shapes = shapes, ActiveShape = shapes[0],
                Filter = filter, Scene = rigidbody.gameObject.scene.GetPhysicsScene2D()
            });
            foreach (var shape in shapes) entities.Add(shape, entity);
            rigidbody.linearVelocity = Vector2.zero;
            rigidbody.angularVelocity = 0;
        }

        public void RegisterObstacle(Entity entity, Collider2D collider)
        {
            if (!EntityManager.Exists(entity)) throw new ArgumentException("Physics obstacle requires a live entity.");
            ValidateCollider(collider);
            obstacles.Add(entity, collider);
            entities.Add(collider, entity);
        }

        public void RegisterInteraction(Entity entity, Collider2D collider, LayerMask interactionLayers)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasBuffer<InteractionOverlapComponent>(entity))
                throw new ArgumentException("Physics interaction requires a live entity with InteractionOverlapComponent.");
            ValidateCollider(collider, true);
            if (collider.attachedRigidbody != null &&
                (collider.attachedRigidbody.bodyType != RigidbodyType2D.Kinematic || !collider.attachedRigidbody.simulated))
                throw new InvalidOperationException(collider.name + ": interaction Rigidbody2D must be simulated and Kinematic.");
            if (interactionLayers.value == 0) throw new ArgumentException(collider.name + ": interactionLayers must not be empty.");
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(interactionLayers);
            interactions.Add(entity, (collider, filter));
            entities.Add(collider, entity);
            collider.enabled = true;
        }

        public void UnregisterInteraction(Entity entity)
        {
            var collider = interactions[entity].Collider;
            collider.enabled = false;
            entities.Remove(collider);
            interactions.Remove(entity);
        }

        private static void ValidateCollider(Collider2D collider, bool isTrigger = false)
        {
            if (collider == null) throw new MissingReferenceException("Physics registration requires Collider2D.");
            if (collider.isTrigger != isTrigger) throw new InvalidOperationException(collider.name + ": collider trigger setting is invalid.");
            Vector3 scale = collider.transform.lossyScale;
            if (!math.all(math.isfinite((float3)scale)) || scale.x <= 0 || scale.y <= 0 ||
                !math.all(math.isfinite((float2)collider.offset)))
                throw new InvalidOperationException(collider.name + ": collider scale and offset must be finite; scale must be positive.");
            if (collider is BoxCollider2D box && (!math.all(math.isfinite((float2)box.size)) || math.any((float2)box.size <= 0)) ||
                collider is CircleCollider2D circle && (!math.isfinite(circle.radius) || circle.radius <= 0))
                throw new InvalidOperationException(collider.name + ": collider dimensions must be finite and positive.");
        }

        public void UnregisterBody(Entity entity)
        {
            var body = bodies[entity];
            foreach (var shape in body.Shapes) entities.Remove(shape);
            bodies.Remove(entity);
        }

        public void UnregisterObstacle(Entity entity)
        {
            entities.Remove(obstacles[entity]);
            obstacles.Remove(entity);
        }

        public void SetObstacleEnabled(Entity entity, bool enabled) => obstacles[entity].enabled = enabled;

        public (float2 Displacement, float2 Velocity) Move(Entity entity, float2 origin, float2 displacement, float2 velocity, int shapeIndex)
        {
            var body = bodies[entity];
            for (int i = 0; i < body.Shapes.Length; i++) body.Shapes[i].enabled = i == shapeIndex;
            body.ActiveShape = body.Shapes[shapeIndex];
            body.Rigidbody.position = origin;
            Physics2D.SyncTransforms();
            Vector2 start = origin;
            Vector2 position = origin;
            Vector2 remaining = displacement;
            Vector2 resolvedVelocity = velocity;

            position = ResolveOverlap(body, position);

            for (int iteration = 0; iteration < MaxIterations && remaining.sqrMagnitude > 1e-10f; iteration++)
            {
                float length = remaining.magnitude;
                Vector2 direction = remaining / length;
                body.ActiveShape.Cast(position, body.Rigidbody.rotation, direction, body.Filter, hits, length + ContactDistance);
                float travel = length;
                Vector2 normal = Vector2.zero;
                foreach (var hit in hits)
                {
                    if (Vector2.Dot(direction, hit.normal) >= -0.0001f) continue;
                    if (hit.distance > travel + ContactDistance) continue;
                    travel = Mathf.Min(travel, Mathf.Max(0, hit.distance - ContactDistance));
                    normal = hit.normal;
                }
                position += direction * travel;
                remaining -= direction * travel;
                if (normal == Vector2.zero) break;
                remaining -= normal * Mathf.Min(0, Vector2.Dot(remaining, normal));
                resolvedVelocity -= normal * Mathf.Min(0, Vector2.Dot(resolvedVelocity, normal));
            }
            body.Rigidbody.position = position;
            Physics2D.SyncTransforms();
            position = ResolveOverlap(body, position);
            return ((float2)(position - start), (float2)resolvedVelocity);
        }

        private Vector2 ResolveOverlap(Body body, Vector2 position)
        {
            // Cast 与 Distance 的接触边距不同，移动后也须用 Unity 距离查询确认无穿透。
            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                FindNearby(body);
                bool moved = false;
                foreach (var other in overlaps)
                {
                    if (other.attachedRigidbody == body.Rigidbody) continue;
                    var distance = body.ActiveShape.Distance(other);
                    if (!distance.isOverlapped) continue;
                    position += distance.normal * (distance.distance - ContactDistance);
                    body.Rigidbody.position = position;
                    Physics2D.SyncTransforms();
                    moved = true;
                }
                if (!moved) break;
            }
            return position;
        }

        private void FindNearby(Body body)
        {
            Bounds bounds = body.ActiveShape.bounds;
            body.Scene.OverlapBox(bounds.center,
                (Vector2)bounds.size + Vector2.one * (ContactDistance + Physics2D.defaultContactOffset) * 4,
                0, body.Filter, overlaps);
        }

        protected override void OnUpdate()
        {
            Physics2D.SyncTransforms();
            foreach (var (entity, range) in interactions)
            {
                var contacts = EntityManager.GetBuffer<InteractionOverlapComponent>(entity);
                contacts.Clear();
                range.Collider.Overlap(range.Filter, overlaps);
                foreach (var other in overlaps)
                {
                    if (!entities.TryGetValue(other, out var target) || !bodies.ContainsKey(target)) continue;
                    contacts.Add(new InteractionOverlapComponent { Entity = target });
                }
            }
            foreach (var (entity, body) in bodies)
            {
                var contacts = EntityManager.GetBuffer<PhysicsContactComponent>(entity);
                contacts.Clear();
                FindNearby(body);
                foreach (var other in overlaps)
                {
                    if (other.attachedRigidbody == body.Rigidbody) continue;
                    var distance = body.ActiveShape.Distance(other);
                    if (distance.distance > ContactDistance * 2) continue;
                    contacts.Add(new PhysicsContactComponent { Entity = entities[other], Normal = distance.normal });
                }
            }
        }
    }
}
