using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// Shifts a background object against camera movement. Layers further back use a smaller factor, so the
    /// skyline slides less than the near towers and the arena reads as genuinely deep rather than painted on.
    /// </summary>
    public sealed class ParallaxLayer : MonoBehaviour
    {
        private Vector3 basePosition;
        private float factor;
        private Transform view;

        public void Configure(float parallaxFactor)
        {
            factor = parallaxFactor;
            basePosition = transform.position;
        }

        private void LateUpdate()
        {
            if (view == null)
            {
                Camera main = Camera.main;
                if (main == null) return;
                view = main.transform;
            }

            Vector3 offset = view.position;
            transform.position = new Vector3(
                basePosition.x + offset.x * factor,
                basePosition.y + (offset.y - 4.6f) * factor * 0.6f,
                basePosition.z);
        }
    }
}
