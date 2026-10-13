using Emergence.Gameplay.Player;
using UnityEngine;

namespace Emergence.WorldView
{
    public sealed class PlayerWorldView : MonoBehaviour
    {
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private SpriteRenderer solidBody;
        [SerializeField] private SpriteRenderer liquidBody;
        [SerializeField] private ParticleSystem gasParticles;
        [SerializeField] private TrailRenderer gasTrail;
        private Vector3 solidScale;
        private Vector3 liquidScale;
        private bool hasForm;
        private PlayerFormKind shownForm;

        private void Awake()
        {
            if (solidBody != null) solidScale = solidBody.transform.localScale;
            if (liquidBody != null) liquidScale = liquidBody.transform.localScale;
        }

        public void Refresh(PlayerPosition position, PlayerFormKind form, PlayerMotionState motion)
        {
            transform.position = new Vector3(position.XY.x, position.XY.y, transform.position.z);
            if (bodyRoot != null) bodyRoot.localPosition = new Vector3(0, position.Height, 0);

            bool gas = form == PlayerFormKind.Gas;
            if (solidBody != null) solidBody.gameObject.SetActive(form == PlayerFormKind.Solid);
            if (liquidBody != null) liquidBody.gameObject.SetActive(form == PlayerFormKind.Liquid);
            if (!hasForm || shownForm != form)
            {
                if (gasParticles != null)
                {
                    gasParticles.gameObject.SetActive(gas);
                    if (gas) gasParticles.Play();
                    else gasParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                if (gasTrail != null)
                {
                    gasTrail.Clear();
                    gasTrail.emitting = gas;
                    gasTrail.enabled = gas;
                }
                shownForm = form;
                hasForm = true;
            }

            float amount = Mathf.Min(1, new Vector2(motion.Velocity.x, motion.Velocity.y).magnitude);
            if (solidBody != null)
            {
                float pulse = Mathf.Sin(Time.time * 10) * 0.08f * amount;
                solidBody.transform.localScale = Vector3.Scale(solidScale, new Vector3(1 + pulse, 1 - pulse, 1));
            }
            if (liquidBody != null)
            {
                liquidBody.transform.localScale = Vector3.Scale(liquidScale,
                    new Vector3(1 + 0.15f * amount, 1 - 0.15f * amount, 1));
                if (amount > 0)
                    liquidBody.transform.localRotation = Quaternion.Euler(0, 0,
                        Mathf.Atan2(motion.Velocity.y, motion.Velocity.x) * Mathf.Rad2Deg);
            }
        }
    }
}
