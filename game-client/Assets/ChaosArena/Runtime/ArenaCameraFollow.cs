using UnityEngine;

namespace ChaosArena
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraFollow : MonoBehaviour
    {
        private readonly Vector3 arenaAnchor = new(0f, 4.6f, -27f);
        private Transform localPlayer;
        private Vector3 velocity;

        public void SetTarget(Transform target)
        {
            localPlayer = target;
        }

        private void LateUpdate()
        {
            Vector3 desired = arenaAnchor;
            if (localPlayer != null && localPlayer.gameObject.activeInHierarchy)
            {
                desired.x += Mathf.Clamp(localPlayer.position.x * 0.13f, -1.4f, 1.4f);
                desired.y += Mathf.Clamp((localPlayer.position.y - 2.2f) * 0.09f, -0.45f, 0.65f);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.38f, 5f);
            transform.rotation = Quaternion.LookRotation(new Vector3(0f, 2.25f, 0.5f) - transform.position);
        }
    }
}
