using System.Collections.Generic;
using UnityEngine;

namespace ChaosArena
{
    public sealed class WeaponPickup : MonoBehaviour
    {
        private static readonly List<WeaponPickup> all = new();

        /// <summary>
        /// Clients never decide pickups. Their fighters are moved by transform, so triggers still fire and a
        /// client would otherwise grab a weapon the host never awarded and hide a pickup that is still there.
        /// </summary>
        public static bool LocalPickupsEnabled = true;
        private PrototypeWeaponProfile profile;
        private Renderer[] visuals;
        private Collider trigger;
        private Vector3 basePosition;
        private float respawnAt;

        public static IReadOnlyList<WeaponPickup> All => all;
        public bool IsAvailable => trigger != null && trigger.enabled;
        public Vector3 Position => transform.position;

        public static WeaponPickup Spawn(PrototypeWeaponProfile profile, Vector3 position)
        {
            GameObject root = new($"Pickup_{profile.Name}");
            root.transform.position = position;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.15f, 1.15f, 1.8f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = profile.Name;
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = profile.Id switch
            {
                PrototypeWeaponId.PulseSmg => new Vector3(0.8f, 0.24f, 0.28f),
                PrototypeWeaponId.ScatterBlaster => new Vector3(1f, 0.34f, 0.34f),
                PrototypeWeaponId.Sniper => new Vector3(1.35f, 0.16f, 0.16f),
                _ => new Vector3(0.8f, 0.22f, 0.22f)
            };
            DisableAndDestroyCollider(body);
            PrototypeMaterials.Assign(body.GetComponent<Renderer>(), profile.ProjectileColor, true);

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Pickup Halo";
            halo.transform.SetParent(root.transform, false);
            halo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            halo.transform.localScale = Vector3.one * 0.2f;
            DisableAndDestroyCollider(halo);
            Color haloColor = profile.ProjectileColor;
            haloColor.a = 0.22f;
            PrototypeMaterials.Assign(halo.GetComponent<Renderer>(), haloColor, true);

            WeaponPickup pickup = root.AddComponent<WeaponPickup>();
            pickup.profile = profile;
            pickup.basePosition = position;
            return pickup;
        }

        // Decorative parts must lose their collider immediately; Destroy() alone leaves it live for the rest
        // of the frame, which is long enough for a passing projectile to trigger against it and disappear.
        private static void DisableAndDestroyCollider(GameObject target)
        {
            Collider decorationCollider = target.GetComponent<Collider>();
            if (decorationCollider == null) return;
            decorationCollider.enabled = false;
            Destroy(decorationCollider);
        }

        public static WeaponPickup FindNearestAvailable(Vector3 position, float maxDistance)
        {
            WeaponPickup best = null;
            float bestDistance = maxDistance;
            foreach (WeaponPickup pickup in all)
            {
                if (pickup == null || !pickup.IsAvailable) continue;
                float distance = Vector3.Distance(position, pickup.Position);
                if (distance < bestDistance)
                {
                    best = pickup;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public static void ResetAll()
        {
            foreach (WeaponPickup pickup in all)
            {
                if (pickup != null) pickup.SetAvailable(true);
            }
        }

        private void Awake()
        {
            trigger = GetComponent<Collider>();
            visuals = GetComponentsInChildren<Renderer>(true);
            all.Add(this);
        }

        private void OnDestroy()
        {
            all.Remove(this);
        }

        /// <summary>Host-driven availability, so a client's pickups match what the host actually has.</summary>
        public void SetRemoteAvailable(bool available)
        {
            if (IsAvailable != available) SetAvailable(available);
        }

        private void Update()
        {
            if (!IsAvailable)
            {
                if (LocalPickupsEnabled && Time.time >= respawnAt) SetAvailable(true);
                return;
            }

            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2.4f) * 0.16f);
            transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!LocalPickupsEnabled || !IsAvailable) return;
            Fighter fighter = other.GetComponent<Fighter>();
            FighterMotor motor = other.GetComponent<FighterMotor>();
            if (fighter == null || fighter.IsEliminated || motor == null) return;
            motor.Equip(profile);
            SetAvailable(false);
            respawnAt = Time.time + 10f;
            CombatVfx.Impact(transform.position, 1, profile.ProjectileColor, true);
        }

        private void SetAvailable(bool available)
        {
            if (trigger != null) trigger.enabled = available;
            if (visuals != null)
            {
                foreach (Renderer item in visuals) item.enabled = available;
            }
            if (available) transform.position = basePosition;
        }
    }
}
