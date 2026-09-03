using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// A small ring around the held weapon that closes as the shot recharges. The moment it completes is the
    /// moment the weapon can fire again, so the player reads their next shot off the weapon itself instead of
    /// counting seconds or spamming the trigger.
    ///
    /// Deliberately absent on fast weapons: the SMG recharges in 0.11s, and a ring closing that fast strobes
    /// and reads as noise. Only weapons at or above <see cref="FighterMotor.ShowsChargeRing"/>'s threshold get
    /// one, which is exactly the pistol, the scatter blaster and the sniper.
    /// </summary>
    [RequireComponent(typeof(FighterMotor))]
    public sealed class WeaponChargeRing : MonoBehaviour
    {
        private const int SegmentCount = 20;
        private const float Radius = 0.42f;
        private const float ReadyFlashSeconds = 0.16f;

        private FighterMotor motor;
        private Transform ring;
        private Renderer[] segments;
        private Material chargingMaterial;
        private Material readyMaterial;
        private float completedAt = -1f;
        private bool wasCharging;

        /// <summary>Test hook: the ring is useless if it never actually built its segments.</summary>
        public int SegmentsBuilt => segments != null ? segments.Length : 0;

        private void Awake()
        {
            motor = GetComponent<FighterMotor>();
            BuildRing();
            Hide();
        }

        private void BuildRing()
        {
            GameObject ringObject = new("Weapon Charge Ring");
            ringObject.transform.SetParent(transform, false);
            ring = ringObject.transform;

            // Both materials are created once and shared by every segment: the ring redraws every frame, so
            // per-segment material instances would allocate continuously.
            chargingMaterial = PrototypeMaterials.CreateNeonMaterial(new Color(0.55f, 0.8f, 1f), 1.5f);
            readyMaterial = PrototypeMaterials.CreateNeonMaterial(new Color(0.7f, 1f, 0.85f), 3.4f);

            segments = new Renderer[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
            {
                // Starts at the top and closes clockwise, so "closed" is unambiguous at a glance.
                float angle = Mathf.PI * 0.5f - i / (float)SegmentCount * Mathf.PI * 2f;
                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = "Charge Segment";
                segment.transform.SetParent(ring, false);
                segment.transform.localPosition = new Vector3(Mathf.Cos(angle) * Radius, Mathf.Sin(angle) * Radius, 0f);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                segment.transform.localScale = new Vector3(0.05f, 0.13f, 0.05f);

                Collider segmentCollider = segment.GetComponent<Collider>();
                segmentCollider.enabled = false;
                Destroy(segmentCollider);

                segments[i] = segment.GetComponent<Renderer>();
                PrototypeMaterials.AssignShared(segments[i], chargingMaterial, false);
            }
        }

        private void LateUpdate()
        {
            if (segments == null) return;

            if (!motor.ShowsChargeRing)
            {
                Hide();
                return;
            }

            float progress = motor.ChargeProgress01;
            bool charging = progress < 1f;

            // Catch the transition rather than the state, so the completed ring flashes once and clears
            // instead of sitting on screen as permanent clutter while the weapon is simply ready.
            if (wasCharging && !charging) completedAt = Time.time;
            wasCharging = charging;

            if (!charging && Time.time - completedAt > ReadyFlashSeconds)
            {
                Hide();
                return;
            }

            // The ring rides just ahead of the fighter on the side it is aiming, so it never covers the body.
            ring.localPosition = new Vector3(motor.Facing * 0.62f, 0.05f, -0.5f);

            int filled = charging ? Mathf.CeilToInt(progress * SegmentCount) : SegmentCount;
            Material material = charging ? chargingMaterial : readyMaterial;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                segments[i].enabled = i < filled;
                if (segments[i].enabled) segments[i].sharedMaterial = material;
            }
        }

        private void Hide()
        {
            if (segments == null) return;
            foreach (Renderer segment in segments)
            {
                if (segment != null) segment.enabled = false;
            }
        }
    }
}
