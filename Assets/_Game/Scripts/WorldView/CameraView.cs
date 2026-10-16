using System;
using UnityEngine;

namespace Emergence.WorldView
{
    public sealed class CameraView : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector2 offset;
        [SerializeField, Min(0)] private float smoothTime = .2f;
        [SerializeField, Range(0, .45f)] private float viewportMargin = .2f;
        private Transform cameraTransform;
        private Vector2 smoothingVelocity;
        private float cameraZ;

        public void Initialize()
        {
            if (targetCamera == null) throw new MissingReferenceException(name + ": targetCamera is required.");
            if (!targetCamera.orthographic || Quaternion.Angle(targetCamera.transform.rotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException(name + ": camera requires an orthographic lens and zero world rotation.");
            if (!float.IsFinite(offset.x) || !float.IsFinite(offset.y) || !float.IsFinite(smoothTime) || smoothTime < 0 ||
                !float.IsFinite(viewportMargin) || viewportMargin < 0 || viewportMargin >= .5f)
                throw new InvalidOperationException(name + ": camera follow parameters are invalid.");
            cameraTransform = targetCamera.transform;
            cameraZ = cameraTransform.position.z;
            smoothingVelocity = Vector2.zero;
        }

        public void Snap(Vector2 target)
        {
            smoothingVelocity = Vector2.zero;
            Apply(target, target + offset);
        }

        public void Refresh(Vector2 target, float deltaTime)
        {
            Vector2 current = cameraTransform.position;
            Vector2 desired = target + offset;
            Vector2 next = smoothTime == 0 ? desired : deltaTime > 0
                ? Vector2.SmoothDamp(current, desired, ref smoothingVelocity, smoothTime, Mathf.Infinity, deltaTime)
                : current;
            Apply(target, next);
        }

        private void Apply(Vector2 target, Vector2 next)
        {
            // 平滑后约束玩家的视口位置，避免高速移动或高度变化将玩家甩出画面。
            float halfHeight = targetCamera.orthographicSize * (1 - 2 * viewportMargin);
            float halfWidth = halfHeight * targetCamera.aspect;
            Vector2 constrained = new(Mathf.Clamp(next.x, target.x - halfWidth, target.x + halfWidth),
                Mathf.Clamp(next.y, target.y - halfHeight, target.y + halfHeight));
            if (constrained.x != next.x) smoothingVelocity.x = 0;
            if (constrained.y != next.y) smoothingVelocity.y = 0;
            cameraTransform.position = new Vector3(constrained.x, constrained.y, cameraZ);
        }
    }
}
