using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// Shows a bright ring around a fighter while respawn protection is active, so a blocked hit reads as
    /// protection instead of a missed shot. The ring holds steady for most of the window and only blinks in
    /// the final moments to telegraph that protection is about to end. Intentionally silent.
    /// </summary>
    [RequireComponent(typeof(Fighter))]
    public sealed class ProtectionShield : MonoBehaviour
    {
        private const int SegmentCount = 18;
        private const float Radius = 0.95f;
        private const float BlinkWindow = 0.35f;
        private const float BlinkRate = 11f;
        private const float SpinDegreesPerSecond = 85f;

        private Fighter fighter;
        private Transform ring;
        private Renderer[] segments;

        private void Awake()
        {
            fighter = GetComponent<Fighter>();
            BuildRing();
            SetVisible(false);
        }

        private void BuildRing()
        {
            GameObject ringObject = new("Protection Shield");
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ring = ringObject.transform;

            Color shieldColor = new(0.6f, 0.96f, 1f);
            segments = new Renderer[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
            {
                float angle = i / (float)SegmentCount * Mathf.PI * 2f;
                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = "Shield Segment";
                segment.transform.SetParent(ring, false);
                segment.transform.localPosition = new Vector3(Mathf.Cos(angle) * Radius, Mathf.Sin(angle) * Radius, 0f);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                segment.transform.localScale = new Vector3(0.09f, 0.26f, 0.09f);

                Collider segmentCollider = segment.GetComponent<Collider>();
                segmentCollider.enabled = false;
                Destroy(segmentCollider);

                PrototypeMaterials.Assign(segment.GetComponent<Renderer>(), shieldColor, true);
                segments[i] = segment.GetComponent<Renderer>();
            }
        }

        private void LateUpdate()
        {
            if (fighter == null || !fighter.IsDamped)
            {
                SetVisible(false);
                return;
            }

            float remaining = fighter.DampedRemaining;
            bool visible = remaining > BlinkWindow || Mathf.Repeat(remaining * BlinkRate, 1f) > 0.5f;
            SetVisible(visible);
            ring.localRotation = Quaternion.Euler(0f, 0f, Time.time * SpinDegreesPerSecond);
        }

        private void SetVisible(bool visible)
        {
            if (segments == null) return;
            foreach (Renderer segment in segments)
            {
                if (segment != null) segment.enabled = visible;
            }
        }
    }
}
