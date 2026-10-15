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
        [SerializeField] private ParticleSystem gasParticles;
        [SerializeField] private TrailRenderer gasTrail;
        [SerializeField] private SpriteRenderer shadow;
        private Vector3 solidScale;
        private Vector3 liquidScale;
        private Vector3 shadowScale;
        private float shadowWidth;
        // 仅缓存已显示形态，避免每帧重新播放粒子；权威形态仍在 ECS。
        private PlayerFormKind shownForm;
        private Vector3 bodyScale;
        private float respawnStarted = float.NegativeInfinity;

        public void PlayRespawn()
        {
            respawnStarted = Time.time;
            gasTrail.Clear();
            gasParticles.Clear(true);
        }

        public void Initialize(PlayerFormKind form)
        {
            // 校验
            if (bodyRoot == null) throw new MissingReferenceException(name + ": PlayerView.bodyRoot is required.");
            if (solidBody == null) throw new MissingReferenceException(name + ": PlayerView.solidBody is required.");
            if (liquidBody == null) throw new MissingReferenceException(name + ": PlayerView.liquidBody is required.");
            if (gasParticles == null) throw new MissingReferenceException(name + ": PlayerView.gasParticles is required.");
            if (gasParticles.GetComponent<ParticleSystemRenderer>() == null)
                throw new MissingReferenceException(name + ": gasParticles requires ParticleSystemRenderer.");
            if (gasTrail == null) throw new MissingReferenceException(name + ": PlayerView.gasTrail is required.");
            if (shadow == null) throw new MissingReferenceException(name + ": PlayerView.shadow is required.");
            if (shadow.sprite == null) throw new MissingReferenceException(name + ": shadow.sprite is required.");
            if (solidBody.sprite == null) throw new MissingReferenceException(name + ": solidBody.sprite is required.");
            if (liquidBody.sprite == null) throw new MissingReferenceException(name + ": liquidBody.sprite is required.");
            if (shadow.transform.parent != transform)
                throw new System.InvalidOperationException(name + ": shadow must be a direct child of the player ground root.");
            // 缓存表现参数
            solidScale = solidBody.transform.localScale;
            bodyScale = bodyRoot.localScale;
            respawnStarted = float.NegativeInfinity;
            liquidScale = liquidBody.transform.localScale;
            shadowScale = shadow.transform.localScale;
            shadowWidth = shadow.bounds.size.x;
            // 校验阴影尺寸
            if (!math.isfinite(shadowWidth) || shadowWidth <= 0)
                throw new System.InvalidOperationException(name + ": shadow width must be finite and positive.");
            // 初始化
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
            if (!gas) gasParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gasParticles.gameObject.SetActive(gas);
            if (gas) gasParticles.Play();
            gasTrail.Clear();
            gasTrail.emitting = gas;
            gasTrail.enabled = gas;
            shownForm = form;
        }

        public void Refresh(PlayerPositionComponent position, PlayerFormKind form, PlayerMotionComponent motion)
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
            liquidBody.transform.localScale = Vector3.Scale(liquidScale,
                new Vector3(1 + 0.15f * amount, 1 - 0.15f * amount, 1));
            if (amount > 0)
                liquidBody.transform.localRotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(motion.Velocity.y, motion.Velocity.x) * Mathf.Rad2Deg);
            RefreshShadow(position, form);
        }

        private void ResizeShadow(float width)
        {
            float scale = width / shadowWidth;
            shadow.transform.localScale = new Vector3(shadowScale.x * scale, shadowScale.y * scale, shadowScale.z);
        }

        private void RefreshShadow(PlayerPositionComponent position, PlayerFormKind form)
        {
            Vector2 groundCenter = new(position.XY.x, position.XY.y);
            if (form != PlayerFormKind.Gas)
            {
                var body = form == PlayerFormKind.Solid ? solidBody : liquidBody;
                Bounds bounds = body.bounds;
                ResizeShadow(bounds.size.x);
                groundCenter = new Vector2(bounds.center.x, bounds.min.y - (bodyRoot.position.y - transform.position.y));
            }
            Bounds shadowBounds = shadow.bounds;
            shadow.transform.position += new Vector3(groundCenter.x - shadowBounds.center.x, groundCenter.y - shadowBounds.center.y, 0);
        }

    }
}
