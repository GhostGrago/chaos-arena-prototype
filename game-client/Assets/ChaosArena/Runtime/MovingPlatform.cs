using System.Collections.Generic;
using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// A platform that slides back and forth along a fixed path.
    ///
    /// Motion is derived from the clock rather than integrated, so every peer in an online match computes the
    /// same position from the same match time without any of it needing to be replicated.
    ///
    /// The whole platform object moves, decorations included, which is why the builder wraps each platform in
    /// an unscaled root: moving a scaled deck directly would drag its trim out of alignment.
    /// </summary>
    public sealed class MovingPlatform : MonoBehaviour
    {
        private static readonly List<MovingPlatform> active = new();
        public static IReadOnlyList<MovingPlatform> Active => active;

        private Vector3 origin;
        private Vector3 travel;
        private float period = 1f;
        private float phase;
        private Rigidbody body;

        /// <summary>World-space velocity this frame, so anything standing on it can be carried along.</summary>
        public Vector3 Velocity { get; private set; }

        public void Configure(Vector3 pathTravel, float cyclePeriod, float cyclePhase)
        {
            origin = transform.position;
            travel = pathTravel;
            period = Mathf.Max(0.5f, cyclePeriod);
            phase = cyclePhase;

            // A collider that moves every frame must belong to a kinematic body, or PhysX rebuilds the static
            // collision tree on every step and contacts against it resolve badly.
            body = gameObject.GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            transform.position = Evaluate(Time.time);
        }

        private Vector3 Evaluate(float time)
        {
            float t = (time / period + phase) * Mathf.PI * 2f;
            return origin + travel * (Mathf.Sin(t) * 0.5f);
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        private void FixedUpdate()
        {
            if (period <= 0f) return;

            Vector3 next = Evaluate(Time.time + Time.fixedDeltaTime);
            Velocity = (next - transform.position) / Time.fixedDeltaTime;
            if (body != null) body.MovePosition(next);
            else transform.position = next;
        }

        /// <summary>Velocity of the moving platform a collider belongs to, or zero for solid ground.</summary>
        public static Vector3 VelocityUnder(Collider ground)
        {
            if (ground == null) return Vector3.zero;
            MovingPlatform platform = ground.GetComponentInParent<MovingPlatform>();
            return platform != null ? platform.Velocity : Vector3.zero;
        }
    }
}
