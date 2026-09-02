using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// Personality layer for the geometric fighters. Abstract shapes risk reading as cold, so all of the
    /// character comes from motion: squash on impact, lean into speed, tumble when launched, and shake harder
    /// as the fighter gets easier to knock out.
    /// </summary>
    [RequireComponent(typeof(FighterMotor), typeof(Rigidbody))]
    public sealed class FighterVisual : MonoBehaviour
    {
        private Transform visualRoot;
        private Transform body;
        private Transform eye;
        private FighterMotor motor;
        private Rigidbody rigidBody;
        private Renderer bodyRenderer;
        private Color baseTint;

        private float recoil;
        private float hitPulse;
        private float danger;
        private float tumble;
        private float bounce;

        public void Bind(Transform root, Transform bodyTransform, Transform eyeTransform, Renderer tintRenderer)
        {
            visualRoot = root;
            body = bodyTransform;
            eye = eyeTransform;
            bodyRenderer = tintRenderer;
            if (bodyRenderer != null) baseTint = bodyRenderer.sharedMaterial.color;
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
            rigidBody = GetComponent<Rigidbody>();
        }

        private void LateUpdate()
        {
            if (visualRoot == null || body == null) return;

            Vector3 velocity = rigidBody.linearVelocity;
            float speed01 = Mathf.Clamp01(Mathf.Abs(velocity.x) / 7f);

            // Airborne shapes tumble in their travel direction; grounded ones settle upright.
            float spinRate = motor.IsGrounded ? 0f : Mathf.Clamp(-velocity.x * 42f, -520f, 520f);
            tumble = motor.IsGrounded
                ? Mathf.LerpAngle(tumble, 0f, Time.deltaTime * 9f)
                : tumble + spinRate * Time.deltaTime;

            // Landing squash gives weight without any animation data.
            bounce = motor.IsGrounded
                ? Mathf.MoveTowards(bounce, 0f, Time.deltaTime * 4.5f)
                : Mathf.Clamp01(Mathf.Abs(velocity.y) / 16f);

            float dangerWobble = danger > 0.45f
                ? Mathf.Sin(Time.unscaledTime * Mathf.Lerp(9f, 20f, danger)) * danger * 2.4f
                : 0f;

            float squash = hitPulse * 0.24f + bounce * 0.1f;
            visualRoot.localPosition = new Vector3(-recoil, 0f, 0f);
            visualRoot.localScale = new Vector3(1f + squash, 1f - squash * 0.7f, 1f);
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(-velocity.x * 1.1f + dangerWobble, -16f, 16f));

            body.localRotation = Quaternion.Euler(0f, 0f, tumble);
            body.localScale = Vector3.one * (1f + speed01 * 0.03f);

            // The eye is the only facing cue an abstract solid has, so it always leads the direction of fire.
            if (eye != null)
            {
                eye.localPosition = new Vector3(motor.Facing * 0.42f, 0.12f, -0.34f);
            }

            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 1.8f);
            hitPulse = Mathf.MoveTowards(hitPulse, 0f, Time.deltaTime * 5f);

            if (bodyRenderer != null)
            {
                PrototypeMaterials.SetColor(bodyRenderer, Color.Lerp(baseTint, Color.white, hitPulse * 0.82f));
            }
        }
    }
}
