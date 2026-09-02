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
        private Transform localPlayer;
        private Vector3 velocity;

        public void SetTarget(Transform target)
        {
            localPlayer = target;
        }

        private void LateUpdate()
        {
            Vector3 focus = arenaFocus;
            float pullBack = 0f;

            if (localPlayer != null && localPlayer.gameObject.activeInHierarchy)
            {
                Vector3 position = localPlayer.position;
                float verticalOffset = position.y - arenaFocus.y;

                focus.x = Mathf.Clamp(position.x * FollowWeightX, -FocusLimitX, FocusLimitX);
                focus.y = arenaFocus.y + Mathf.Clamp(verticalOffset * FollowWeightY, FocusLimitDown, FocusLimitUp);

                float edge01 = Mathf.InverseLerp(EdgeZoomStart, EdgeZoomFull, Mathf.Abs(position.x));
                float air01 = Mathf.InverseLerp(AirZoomStart, AirZoomFull, verticalOffset);
                pullBack = Mathf.Max(edge01, air01) * MaxPullBack;
            }

            Vector3 desired = new(focus.x, focus.y + CameraHeightAboveFocus, BaseDistance - pullBack);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.28f, 40f);
            transform.rotation = Quaternion.LookRotation(focus - transform.position);
        }
    }
}
