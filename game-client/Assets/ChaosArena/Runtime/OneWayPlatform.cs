using System.Collections.Generic;
using UnityEngine;

namespace ChaosArena
{
    [RequireComponent(typeof(Collider))]
    public sealed class OneWayPlatform : MonoBehaviour
    {
        private static readonly List<OneWayPlatform> activePlatforms = new();

        public static IReadOnlyList<OneWayPlatform> ActivePlatforms => activePlatforms;
        public Collider PlatformCollider { get; private set; }
        public float Top => PlatformCollider.bounds.max.y;

        private void Awake()
        {
            PlatformCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            if (!activePlatforms.Contains(this)) activePlatforms.Add(this);
        }

        private void OnDisable()
        {
            activePlatforms.Remove(this);
        }
    }
}
