using Emergence.Gameplay.Player;
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
        private Vector3 solidScale;
        private Vector3 liquidScale;
        // 仅缓存已显示形态，避免每帧重新播放粒子；权威形态仍在 ECS。
        private PlayerFormKind shownForm;

        public void Initialize(PlayerFormKind form)
        {
            if (bodyRoot == null) throw new MissingReferenceException(name + ": PlayerView.bodyRoot is required.");
            if (solidBody == null) throw new MissingReferenceException(name + ": PlayerView.solidBody is required.");
            if (liquidBody == null) throw new MissingReferenceException(name + ": PlayerView.liquidBody is required.");
            if (gasParticles == null) throw new MissingReferenceException(name + ": PlayerView.gasParticles is required.");
            if (gasParticles.GetComponent<ParticleSystemRenderer>() == null)
                throw new MissingReferenceException(name + ": gasParticles requires ParticleSystemRenderer.");
            if (gasTrail == null) throw new MissingReferenceException(name + ": PlayerView.gasTrail is required.");
            solidScale = solidBody.transform.localScale;
            liquidScale = liquidBody.transform.localScale;
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
            if (shownForm != form) ShowForm(form);
            float amount = Mathf.Min(1, new Vector2(motion.Velocity.x, motion.Velocity.y).magnitude);
            float pulse = Mathf.Sin(Time.time * 10) * 0.08f * amount;
            solidBody.transform.localScale = Vector3.Scale(solidScale, new Vector3(1 + pulse, 1 - pulse, 1));
            liquidBody.transform.localScale = Vector3.Scale(liquidScale,
                new Vector3(1 + 0.15f * amount, 1 - 0.15f * amount, 1));
            if (amount > 0)
                liquidBody.transform.localRotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(motion.Velocity.y, motion.Velocity.x) * Mathf.Rad2Deg);
        }

    }
}
