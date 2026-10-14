using UnityEngine;

namespace Emergence.WorldView
{
    public sealed class InteractionView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public void Initialize()
        {
            // 校验
            if (spriteRenderer == null) throw new MissingReferenceException(name + ": spriteRenderer is required.");
            // 初始化
            spriteRenderer.enabled = true;
        }
    }
}
