using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.Gameplay.InteractCallback
{
    // 场景落点配置，不保存玩家状态，也不自行启动玩法。
    public sealed class SceneTransferPoint : MonoBehaviour
    {
        [SerializeField] private string pointId;
        [SerializeField] private Transform anchor;
        public string PointId => pointId;
        public Transform Anchor => anchor;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(pointId))
                throw new InvalidOperationException(name + ": pointId is required.");
            _ = new FixedString64Bytes(pointId);
            if (anchor == null) throw new MissingReferenceException(name + ": anchor is required.");
            if (anchor.gameObject.scene != gameObject.scene)
                throw new InvalidOperationException(name + ": anchor must belong to the same scene.");
            if (!math.all(math.isfinite((float3)anchor.position)))
                throw new InvalidOperationException(name + ": anchor position must be finite.");
        }

        private void Reset() => anchor = transform;
    }
}
