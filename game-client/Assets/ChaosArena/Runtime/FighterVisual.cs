using UnityEngine;

namespace ChaosArena
{
    [RequireComponent(typeof(FighterMotor), typeof(Rigidbody))]
    public sealed class FighterVisual : MonoBehaviour
    {
        private Transform visualRoot;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftLeg;
        private Transform rightLeg;
        private FighterMotor motor;
        private Rigidbody body;
        private float stride;
        private float recoil;
        private float hitPulse;
        private float danger;
        private Renderer[] tintRenderers;
        private Color baseTint;

        public void Bind(Transform root, Transform armLeft, Transform armRight, Transform legLeft, Transform legRight)
        {
            visualRoot = root;
            leftArm = armLeft;
            rightArm = armRight;
            leftLeg = legLeft;
            rightLeg = legRight;
            tintRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer item in tintRenderers)
            {
                if (item.gameObject.name.StartsWith("Tint_"))
                {
                    baseTint = item.sharedMaterial.color;
                    break;
                }
            }
        }

        public void OnFire(float amount)
        {
            recoil = Mathf.Max(recoil, amount);
        }

        public void OnHit(float danger01)
        {
            hitPulse = 1f;
            danger = danger01;
        }

        private void Awake()
        {
            motor = GetComponent<FighterMotor>();
            body = GetComponent<Rigidbody>();
        }

        private void LateUpdate()
        {
            if (visualRoot == null)
            {
                return;
            }

            float speed01 = Mathf.Clamp01(Mathf.Abs(body.linearVelocity.x) / 7f);
            stride += Time.deltaTime * Mathf.Lerp(3f, 11f, speed01);
            float swing = Mathf.Sin(stride) * 24f * speed01;

            if (!motor.IsGrounded)
            {
                swing = Mathf.Clamp(body.linearVelocity.y * -2.5f, -20f, 20f);
            }

            leftArm.localRotation = Quaternion.Euler(0f, 0f, swing);
            rightArm.localRotation = Quaternion.Euler(0f, 0f, -swing * 0.45f - 8f);
            leftLeg.localRotation = Quaternion.Euler(0f, 0f, -swing);
            rightLeg.localRotation = Quaternion.Euler(0f, 0f, swing);

            float bob = motor.IsGrounded ? Mathf.Abs(Mathf.Sin(stride * 2f)) * 0.035f * speed01 : 0f;
            float tilt = motor.IsGrounded ? -body.linearVelocity.x * 0.7f : -body.linearVelocity.x * 1.2f;
            float dangerWobble = danger > 0.45f ? Mathf.Sin(Time.time * Mathf.Lerp(8f, 16f, danger)) * danger * 1.6f : 0f;
            visualRoot.localPosition = new Vector3(-recoil, bob + dangerWobble * 0.008f, 0f);
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(tilt + dangerWobble, -12f, 12f));
            float hitStretch = hitPulse * 0.18f;
            visualRoot.localScale = new Vector3(motor.Facing * (1f + hitStretch), 1f - hitStretch * 0.72f, 1f);

            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 1.8f);
            hitPulse = Mathf.MoveTowards(hitPulse, 0f, Time.deltaTime * 5f);
            if (tintRenderers != null)
            {
                Color feedback = Color.Lerp(baseTint, Color.white, hitPulse * 0.82f);
                foreach (Renderer item in tintRenderers)
                {
                    if (item != null && item.gameObject.name.StartsWith("Tint_")) PrototypeMaterials.SetColor(item, feedback);
                }
            }
        }
    }
}
