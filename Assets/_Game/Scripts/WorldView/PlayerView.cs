using Emergence.Gameplay.Player;
using Unity.Mathematics;
using UnityEngine;

namespace Emergence.WorldView
{
    public sealed class PlayerView : MonoBehaviour
    {
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private SpriteRenderer solidBody;
        [SerializeField] private SpriteRenderer liquidBody;
        [SerializeField] private LiquidView liquidView;
        [SerializeField] private SpriteRenderer gasBody;
        [SerializeField] private ParticleSystem gasParticles;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private Transform damageFieldRoot;
        [SerializeField] private MeshRenderer damageFieldOutline;
        [SerializeField] private ParticleSystem damageFieldParticles;
        private Vector3 solidScale;
        private Vector3 gasScale;
        private Vector3 gasPosition;
        private Vector3 gasParticlesPosition;
        private Vector3 shadowScale;
        private float shadowWidth;
        // 仅缓存已显示形态，避免每帧重新播放粒子；权威形态仍在 ECS。
        private PlayerFormKind shownForm;
        private Vector3 bodyScale;
        private float respawnStarted = float.NegativeInfinity;

        public void PlayRespawn()
        {
            respawnStarted = Time.time;
            gasParticles.Clear(true);
            liquidView.ResetMotion();
        }

        public void Initialize(PlayerFormKind form)
        {
            // 校验
            if (bodyRoot == null) throw new MissingReferenceException(name + ": PlayerView.bodyRoot is required.");
            if (solidBody == null) throw new MissingReferenceException(name + ": PlayerView.solidBody is required.");
            if (liquidBody == null) throw new MissingReferenceException(name + ": PlayerView.liquidBody is required.");
            if (liquidView == null) throw new MissingReferenceException(name + ": PlayerView.liquidView is required.");
            if (gasBody == null) throw new MissingReferenceException(name + ": PlayerView.gasBody is required.");
            if (gasBody.sprite == null) throw new MissingReferenceException(name + ": gasBody.sprite is required.");
            if (gasParticles == null) throw new MissingReferenceException(name + ": PlayerView.gasParticles is required.");
            var gasRenderer = gasParticles.GetComponent<ParticleSystemRenderer>();
            if (gasRenderer == null || gasRenderer.sharedMaterial == null)
                throw new MissingReferenceException(name + ": gasParticles requires ParticleSystemRenderer and material.");
            if (shadow == null) throw new MissingReferenceException(name + ": PlayerView.shadow is required.");
            if (shadow.sprite == null) throw new MissingReferenceException(name + ": shadow.sprite is required.");
            if (solidBody.sprite == null) throw new MissingReferenceException(name + ": solidBody.sprite is required.");
            if (liquidBody.sprite == null) throw new MissingReferenceException(name + ": liquidBody.sprite is required.");
            if (shadow.transform.parent != transform)
                throw new System.InvalidOperationException(name + ": shadow must be a direct child of the player ground root.");
            if (damageFieldRoot == null) throw new MissingReferenceException(name + ": damageFieldRoot is required.");
            if (damageFieldOutline == null) throw new MissingReferenceException(name + ": damageFieldOutline is required.");
            if (damageFieldParticles == null) throw new MissingReferenceException(name + ": damageFieldParticles is required.");
            if (damageFieldRoot.parent != transform || damageFieldRoot.localPosition != Vector3.zero ||
                damageFieldRoot.localRotation != Quaternion.identity || !damageFieldOutline.transform.IsChildOf(damageFieldRoot) ||
                !damageFieldParticles.transform.IsChildOf(damageFieldRoot))
                throw new System.InvalidOperationException(name + ": damage field visuals must belong to a centered child of the player ground root.");
            var outlineMesh = damageFieldOutline.GetComponent<MeshFilter>();
            if (outlineMesh == null || outlineMesh.sharedMesh == null || damageFieldOutline.sharedMaterial == null)
                throw new MissingReferenceException(name + ": damageFieldOutline requires a saved mesh and material.");
            var fieldRenderer = damageFieldParticles.GetComponent<ParticleSystemRenderer>();
            if (fieldRenderer == null || fieldRenderer.sharedMaterial == null)
                throw new MissingReferenceException(name + ": damageFieldParticles requires a renderer and material.");
            float resolvedShadowWidth = shadow.bounds.size.x;
            if (!math.isfinite(resolvedShadowWidth) || resolvedShadowWidth <= 0)
                throw new System.InvalidOperationException(name + ": shadow width must be finite and positive.");

            // 初始化
            solidScale = solidBody.transform.localScale;
            bodyScale = bodyRoot.localScale;
            respawnStarted = float.NegativeInfinity;
            gasScale = gasBody.transform.localScale;
            gasPosition = gasBody.transform.localPosition;
            gasParticlesPosition = gasParticles.transform.localPosition;
            liquidView.Initialize(liquidBody);
            shadowScale = shadow.transform.localScale;
            shadowWidth = resolvedShadowWidth;
            HideDamageField();
            ResizeShadow(solidBody.bounds.size.x);
            // 初始化显式应用出生形态，替代 hasForm 首帧标志。
            ShowForm(form);
        }

        private void ShowForm(PlayerFormKind form)
        {
            var (solid, liquid, gas) = form switch
            {
                PlayerFormKind.Solid => (true, false, false),
                PlayerFormKind.Liquid => (false, true, false),
                PlayerFormKind.Gas => (false, false, true),
                _ => throw new System.ArgumentOutOfRangeException(nameof(form))
            };
            solidBody.gameObject.SetActive(solid);
            liquidBody.gameObject.SetActive(liquid);
            liquidView.ResetMotion();
            gasBody.gameObject.SetActive(gas);
            if (!gas) gasParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gasParticles.gameObject.SetActive(gas);
            if (gas) gasParticles.Play();
            shownForm = form;
        }

        public void Refresh(PlayerPositionComponent position, PlayerFormKind form, PlayerMotionComponent motion,
            DamageFieldComponent damageField, bool showDamageField)
        {
            transform.position = new(position.XY.x, position.XY.y, transform.position.z);
            bodyRoot.localPosition = new(0, position.Height, 0);
            // 0.6 秒由小放大并轻微弹起，仅缩放表现节点。
            float progress = Mathf.Clamp01((Time.time - respawnStarted) / 0.6f);
            float scale = progress >= 1 ? 1 : Mathf.Lerp(0.15f, 1, progress) + Mathf.Sin(progress * Mathf.PI) * 0.2f;
            bodyRoot.localScale = bodyScale * scale;
            if (shownForm != form) ShowForm(form);
            float amount = Mathf.Min(1, new Vector2(motion.Velocity.x, motion.Velocity.y).magnitude);
            float pulse = Mathf.Sin(Time.time * 10) * 0.08f * amount;
            solidBody.transform.localScale = Vector3.Scale(solidScale, new Vector3(1 + pulse, 1 - pulse, 1));
            Vector2 velocity = new(motion.Velocity.x, motion.Velocity.y);
            if (form == PlayerFormKind.Liquid) liquidView.Refresh(velocity);
            if (form == PlayerFormKind.Gas)
            {
                float breath = Mathf.Sin(Time.time * 2.2f) * 0.025f;
                gasBody.transform.localScale = Vector3.Scale(gasScale, new Vector3(1 + breath, 1 - breath, 1));
                gasBody.transform.localPosition = gasPosition + Vector3.up * (Mathf.Sin(Time.time * 1.8f) * 0.06f);
                gasParticles.transform.localPosition = gasParticlesPosition - (Vector3)velocity.normalized * 0.25f;
            }
            RefreshShadow(position, form);
            damageFieldRoot.localScale = new Vector3(damageField.Radius, damageField.Radius, 1);
            damageFieldRoot.gameObject.SetActive(showDamageField);
        }

        public void HideDamageField() => damageFieldRoot.gameObject.SetActive(false);

        public void Shutdown()
        {
            gasParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            liquidView.Shutdown();
        }

        private void ResizeShadow(float width)
        {
            float scale = width / shadowWidth;
            shadow.transform.localScale = new Vector3(shadowScale.x * scale, shadowScale.y * scale, shadowScale.z);
        }

        private void RefreshShadow(PlayerPositionComponent position, PlayerFormKind form)
        {
            Bounds bounds = form == PlayerFormKind.Liquid ? liquidView.Bounds :
                form == PlayerFormKind.Gas ? gasBody.bounds : solidBody.bounds;
            ResizeShadow(bounds.size.x);
            Vector2 groundCenter = new(bounds.center.x, bounds.min.y - (bodyRoot.position.y - transform.position.y));
            Bounds shadowBounds = shadow.bounds;
            shadow.transform.position += new Vector3(groundCenter.x - shadowBounds.center.x, groundCenter.y - shadowBounds.center.y, 0);
        }

    }
}
