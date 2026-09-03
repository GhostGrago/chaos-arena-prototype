using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// Frames the local player without abandoning the arena. The focus point sits between the arena centre
    /// and the local fighter rather than locking onto either, and the camera pulls back automatically when
    /// the fighter is pushed toward a ring-out edge or launched high, so a strong knockback stays on screen.
    /// In multiplayer each client points this at the fighter it owns.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraFollow : MonoBehaviour
    {
        private const float BaseDistance = -27f;
        private const float CameraHeightAboveFocus = 2.35f;

        // Share of the fighter's position folded into the focus point. Below 1 the arena still anchors the shot.
        private const float FollowWeightX = 0.6f;
        private const float FollowWeightY = 0.45f;
        private const float FocusLimitX = 5.5f;
        private const float FocusLimitDown = -1.5f;
        private const float FocusLimitUp = 3f;

        // Distance from arena centre at which the automatic pull-back starts and reaches full strength.
        private const float EdgeZoomStart = 5.5f;
        private const float EdgeZoomFull = 14f;
        private const float AirZoomStart = 4.5f;
        private const float AirZoomFull = 11f;
        private const float MaxPullBack = 8f;

        private readonly Vector3 arenaFocus = new(0f, 2.25f, 0.5f);
        private readonly Transform[] players = new Transform[3];
        private Vector3 velocity;

        public bool HasSecondaryTarget => players[1] != null;
        public int TargetCount => (players[0] != null ? 1 : 0) + (players[1] != null ? 1 : 0) +
                                  (players[2] != null ? 1 : 0);

        public void SetTarget(Transform target)
        {
            SetTargets(target, null, null);
        }

        public void SetTargets(Transform primary, Transform secondary, Transform tertiary = null)
        {
            players[0] = primary;
            players[1] = secondary;
            players[2] = tertiary;
        }

        private void LateUpdate()
        {
            Vector3 focus = arenaFocus;
            float pullBack = 0f;

            Vector3 sum = Vector3.zero;
            int activeCount = 0;
            float farthestX = 0f;
            float highestY = float.MinValue;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            for (int i = 0; i < players.Length; i++)
            {
                Transform target = players[i];
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                Vector3 position = target.position;
                sum += position;
                activeCount++;
                farthestX = Mathf.Max(farthestX, Mathf.Abs(position.x));
                highestY = Mathf.Max(highestY, position.y);
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
            }

            if (activeCount > 0)
            {
                Vector3 framingPosition = sum / activeCount;
                float verticalOffset = framingPosition.y - arenaFocus.y;

                focus.x = Mathf.Clamp(framingPosition.x * FollowWeightX, -FocusLimitX, FocusLimitX);
                focus.y = arenaFocus.y + Mathf.Clamp(verticalOffset * FollowWeightY, FocusLimitDown, FocusLimitUp);

                float edge01 = Mathf.InverseLerp(EdgeZoomStart, EdgeZoomFull, farthestX);
                float air01 = Mathf.InverseLerp(AirZoomStart, AirZoomFull, highestY - arenaFocus.y);
                float separation01 = activeCount > 1 ? Mathf.InverseLerp(8f, 20f, maxX - minX) : 0f;
                pullBack = Mathf.Max(edge01, Mathf.Max(air01, separation01)) * MaxPullBack;
            }

            Vector3 desired = new(focus.x, focus.y + CameraHeightAboveFocus, BaseDistance - pullBack);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.28f, 40f);

            // Shake is applied after smoothing so the damping does not swallow the impulse.
            float shake = CombatFeel.ShakeAmount;
            if (shake > 0.0005f)
            {
                float time = Time.unscaledTime * 38f;
                transform.position += new Vector3(
                    (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f * shake,
                    (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f * shake,
                    0f);
            }

            transform.rotation = Quaternion.LookRotation(focus - transform.position);
        }
    }
}
