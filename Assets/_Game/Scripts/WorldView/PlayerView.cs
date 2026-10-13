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
            solidBody.gameObject.SetActive(form == PlayerFormKind.Solid);
            liquidBody.gameObject.SetActive(form == PlayerFormKind.Liquid);
            bool gas = form == PlayerFormKind.Gas;
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
            transform.position = new Vector3(position.XY.x, position.XY.y, transform.position.z);
            bodyRoot.localPosition = new Vector3(0, position.Height, 0);
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

        public void Shutdown()
        {
            // 场景销毁时子对象可能已销毁；这些检查只发生在关闭阶段。
            if (gasParticles != null) gasParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (gasTrail != null) { gasTrail.Clear(); gasTrail.emitting = false; }
            if (solidBody != null) solidBody.transform.localScale = solidScale;
            if (liquidBody != null) liquidBody.transform.localScale = liquidScale;
        }
    }
}
