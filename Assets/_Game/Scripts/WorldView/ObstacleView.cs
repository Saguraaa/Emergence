using UnityEngine;

namespace Emergence.WorldView
{
    public sealed class ObstacleView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public bool Initialize()
        {
            // 无独立精灵的障碍物只参与碰撞，不启动显示刷新。
            if (spriteRenderer == null) return false;
            Refresh(false);
            return true;
        }

        public void Refresh(bool isDestroyed)
        {
            spriteRenderer.enabled = !isDestroyed;
        }
    }
}
