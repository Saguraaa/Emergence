using System;
using Emergence.Gameplay.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace Emergence.WorldView
{
    public sealed class NPCView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private GameObject resistanceBar;
        [SerializeField] private Image resistanceFill;
        [SerializeField] private Color normalColor = new(.8f, .8f, .7f);
        [SerializeField] private Color alertColor = new(1, .65f, .15f);
        [SerializeField] private Color fleeingColor = new(1, .3f, .35f);

        public void Initialize()
        {
            // 校验
            if (body == null || body.sprite == null) throw new MissingReferenceException(name + ": body and its sprite are required.");
            if (resistanceBar == null) throw new MissingReferenceException(name + ": resistanceBar is required.");
            if (resistanceFill == null) throw new MissingReferenceException(name + ": resistanceFill is required.");
            if (resistanceFill.type != Image.Type.Filled || resistanceFill.fillMethod != Image.FillMethod.Horizontal)
                throw new InvalidOperationException(name + ": resistanceFill must use horizontal Filled Image.");

            // 初始化
            resistanceBar.SetActive(false);
        }

        public void Refresh(NPCPositionComponent position, NPCResistanceComponent resistance,
            NPCBehaviorComponent behavior, bool showResistance)
        {
            transform.position = new Vector3(position.XY.x, position.XY.y, transform.position.z);
            body.color = behavior.Value switch
            {
                NPCBehaviorKind.Normal => normalColor,
                NPCBehaviorKind.Alert => alertColor,
                NPCBehaviorKind.Fleeing => fleeingColor,
                _ => throw new ArgumentOutOfRangeException(nameof(behavior))
            };
            resistanceFill.fillAmount = resistance.Value / resistance.InitialValue;
            resistanceBar.SetActive(showResistance);
        }

        public void Shutdown() => resistanceBar.SetActive(false);
    }
}
