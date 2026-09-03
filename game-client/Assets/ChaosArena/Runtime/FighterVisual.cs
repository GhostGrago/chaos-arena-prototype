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

        // Jelly jiggle. A spring driven by changes in velocity, so landings, hits and hard turns all set the
        // body wobbling and it settles on its own. This sells "soft" far more than translucency does.
        private const float JiggleStiffness = 110f;
        private const float JiggleDamping = 7.5f;
        private const float JiggleLimit = 0.3f;
        private float jiggle;
        private float jiggleVelocity;
        private Vector3 previousVelocity;

        // Speed streak. Only starts above normal running speed (the motor tops out at 7) so ordinary movement
        // stays clean and a streak always means "this fighter is moving unusually fast" — launched, dodging,
        // or falling hard. Below the floor the trail stops emitting and the tail fades out on its own.
        //
        // Built as one continuous ribbon rather than a row of separate afterimages: discrete ghosts read as a
        // dotted line at arena distance, while an unbroken streak both looks like motion glare and gives a
        // single clean line to follow when several fighters are launched at once.
        private const float TrailSpeedFloor = 9.5f;
        private const float TrailSpeedCeiling = 24f;
        private TrailRenderer streak;
        private float trailBoostUntil;
        private float sizeScale = 1f;

        /// <summary>Whether this fighter can actually leave a speed streak, i.e. the trail is built.</summary>
        public bool CanTrail => streak != null && streak.sharedMaterial != null;

        public void Bind(Transform root, Transform bodyTransform, Transform eyeTransform, Renderer tintRenderer)
        {
            visualRoot = root;
            body = bodyTransform;
            eye = eyeTransform;
            bodyRenderer = tintRenderer;
            if (bodyRenderer != null) baseTint = bodyRenderer.sharedMaterial.color;

            BuildStreak();
        }

        /// <summary>
        /// One ribbon that follows the fighter itself, not the squashing visual root, so the streak traces the
        /// actual flight path instead of wobbling with the jelly animation. Same construction the projectile
        /// tracer already uses: unlit and driven bright so the bloom pass turns it into a glare.
        /// </summary>
        private void BuildStreak()
        {
            GameObject streakObject = new("Speed Streak");
            streakObject.transform.SetParent(transform, false);

            streak = streakObject.AddComponent<TrailRenderer>();
            streak.time = 0.3f;
            streak.minVertexDistance = 0.05f;
            streak.startWidth = 0.9f;
            streak.endWidth = 0f;
            streak.numCapVertices = 4;
            streak.autodestruct = false;
            streak.emitting = false;
            streak.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            streak.receiveShadows = false;
            streak.sharedMaterial = PrototypeMaterials.CreateMaterial(baseTint * 2.6f, true);
        }

        /// <summary>
        /// Forces the trail on briefly regardless of speed. A launch is most readable in the first moments
        /// after contact, before the fighter has built up enough speed to cross the normal threshold.
        /// </summary>
        public void BoostTrail(float seconds)
        {
            trailBoostUntil = Mathf.Max(trailBoostUntil, Time.time + seconds);
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

            Vector3 velocity = motor.PresentationVelocity;
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

            UpdateJiggle(velocity);

            // The shrink power-up scales the whole visual root, so the body a player sees matches the hit
            // volume the collider actually presents. Eased rather than snapped so the change reads as an
            // effect rather than as a rendering glitch.
            Fighter fighter = GetComponent<Fighter>();
            float targetSize = fighter != null ? fighter.SizeScale : 1f;
            sizeScale = Mathf.MoveTowards(sizeScale, targetSize, Time.deltaTime * 2.4f);

            float squash = hitPulse * 0.24f + bounce * 0.1f + jiggle;
            visualRoot.localPosition = new Vector3(-recoil, 0f, 0f);
            visualRoot.localScale = new Vector3(1f + squash, 1f - squash * 0.72f, 1f + squash * 0.45f) * sizeScale;
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(-velocity.x * 1.1f + dangerWobble, -16f, 16f));

            body.localRotation = Quaternion.Euler(0f, 0f, tumble);
            body.localScale = Vector3.one * (1f + speed01 * 0.03f);

            // Spawned after the pose is final so the ghost is an exact copy of what is on screen this frame,
            // squash and tumble included, rather than a frame behind.
            UpdateSpeedTrail(velocity);

            // The eye is the only facing cue an abstract solid has, so it always leads the direction of fire.
            if (eye != null)
            {
                eye.localPosition = new Vector3(motor.Facing * 0.42f, 0.12f, -0.34f);
            }

            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 1.8f);
            hitPulse = Mathf.MoveTowards(hitPulse, 0f, Time.deltaTime * 5f);

            if (bodyRenderer != null)
            {
                Color flash = Color.Lerp(baseTint, Color.white, hitPulse * 0.82f);
                flash.a = baseTint.a;
                PrototypeMaterials.SetColor(bodyRenderer, flash);
            }
        }

        /// <summary>
        /// Drives the streak behind a fast-moving fighter. Length and width both scale with speed, so a light
        /// hop draws a short thin line while a sniper launch draws a long bright one — the speed itself is
        /// what the player reads, with no extra HUD.
        /// </summary>
        private void UpdateSpeedTrail(Vector3 velocity)
        {
            if (!CanTrail) return;

            // A hit is worth showing before the fighter has picked up speed, so the boost lowers the bar
            // rather than bypassing the system.
            bool boosted = Time.time < trailBoostUntil;
            float floor = boosted ? TrailSpeedFloor * 0.5f : TrailSpeedFloor;
            float speed = velocity.magnitude;

            // Turning emission off rather than clearing lets the existing tail fade out behind the fighter,
            // which is what makes the streak taper away instead of vanishing all at once.
            streak.emitting = speed >= floor;
            if (!streak.emitting) return;

            float intensity = Mathf.Clamp01((speed - floor) / Mathf.Max(1f, TrailSpeedCeiling - floor));
            if (boosted) intensity = Mathf.Max(intensity, 0.65f);

            streak.time = Mathf.Lerp(0.18f, 0.42f, intensity);
            streak.widthMultiplier = Mathf.Lerp(0.55f, 1.2f, intensity);
        }

        /// <summary>Spring-damper on the body scale, kicked by any sudden change in velocity.</summary>
        private void UpdateJiggle(Vector3 velocity)
        {
            float delta = Time.deltaTime;
            if (delta <= 0f) return;

            float impulse = (velocity - previousVelocity).magnitude;
            previousVelocity = velocity;

            jiggleVelocity += impulse * 0.055f;
            jiggleVelocity -= jiggle * JiggleStiffness * delta;
            jiggleVelocity *= Mathf.Exp(-JiggleDamping * delta);
            jiggle = Mathf.Clamp(jiggle + jiggleVelocity * delta, -JiggleLimit, JiggleLimit);
        }
    }
}
