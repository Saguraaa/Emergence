using UnityEngine;

namespace Emergence.WorldView
{
    public sealed class ObstacleView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public void Initialize()
        {
            // 校验
            if (spriteRenderer == null) throw new MissingReferenceException(name + ": spriteRenderer is required.");
            // 初始化
            Refresh(false);
        }

        public void Refresh(bool isDestroyed)
        {
            spriteRenderer.enabled = !isDestroyed;
        }
    }
}
