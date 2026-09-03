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

        // What this slot is currently offering. Weapons use their own enum values and power-ups continue
        // past them, so the one replicated byte covers both without widening the network state.
        private byte contentId;
        private int built = -1;

        public static IReadOnlyList<WeaponPickup> All => all;
        public bool IsAvailable => trigger != null && trigger.enabled;
        public Vector3 Position => transform.position;

        public byte ContentId => contentId;
        public bool IsPowerUp => PowerUp.IsPowerUpId(contentId);

        /// <summary>Creates an empty, inactive slot. Weapon and position are assigned when it is used.</summary>
        public static WeaponPickup CreateSlot()
        {
            GameObject root = new("Pickup Slot");

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.15f, 1.15f, 1.8f);

            WeaponPickup slot = root.AddComponent<WeaponPickup>();
            slot.ConfigureContent((byte)PrototypeWeaponId.Carbine, Vector3.zero);
            slot.SetAvailable(false);
            return slot;
        }

        public void Configure(PrototypeWeaponProfile newProfile, Vector3 position)
        {
            ConfigureContent((byte)newProfile.Id, position);
        }

        public void ConfigurePowerUp(PowerUpKind kind, Vector3 position)
        {
            ConfigureContent(PowerUp.ToContentId(kind), position);
        }

        /// <summary>
        /// Points this slot at a weapon or power-up and a place. The visual is only rebuilt when the content
        /// actually changes, so drops that reuse the same type do not churn meshes.
        /// </summary>
        public void ConfigureContent(byte newContentId, Vector3 position)
        {
            basePosition = position;
            transform.position = position;

            if (built == newContentId && visuals != null) return;
            built = newContentId;
            contentId = newContentId;
            if (!PowerUp.IsPowerUpId(newContentId))
            {
                profile = PrototypeWeaponProfile.Get((PrototypeWeaponId)newContentId);
            }

            for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
            if (PowerUp.IsPowerUpId(newContentId))
            {
                PowerUp.BuildVisual(transform, PowerUp.FromContentId(newContentId));
                BuildHalo(transform, PowerUp.Tint(PowerUp.FromContentId(newContentId)));
            }
            else
            {
                BuildVisual(transform, profile);
            }

            visuals = GetComponentsInChildren<Renderer>(true);
        }

        public PrototypeWeaponId Weapon => profile.Id;

        private static void BuildVisual(Transform root, PrototypeWeaponProfile profile)
        {
            // Show the actual weapon so a drop is identifiable at a glance instead of being a coloured block.
            // Enlarged from 0.62 on playtest feedback: at the pulled-back arena camera the drop was too small
            // to identify without walking up to it, which is the whole point of showing the real model.
            GameObject model = WeaponModels.TryCreate(profile.Id, root, WeaponModels.GetHeldScale(profile.Id) * 0.95f);
            if (model == null)
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.name = profile.Name;
                fallback.transform.SetParent(root, false);
                fallback.transform.localScale = new Vector3(0.8f, 0.22f, 0.22f);
                DisableAndDestroyCollider(fallback);
                PrototypeMaterials.Assign(fallback.GetComponent<Renderer>(), profile.ProjectileColor, true);
            }

            BuildHalo(root, profile.ProjectileColor);
        }

        /// <summary>A glowing base keeps a drop readable against the arena even though the model is small.</summary>
        private static void BuildHalo(Transform root, Color tint)
        {
            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Pickup Halo";
            halo.transform.SetParent(root, false);
            halo.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            halo.transform.localScale = new Vector3(1f, 0.09f, 1f);
            DisableAndDestroyCollider(halo);
            PrototypeMaterials.AssignNeon(halo.GetComponent<Renderer>(), tint, 1.8f);
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
                if (pickup != null) pickup.SetAvailable(false);
            }
        }

        public void SetAvailableState(bool available) => SetAvailable(available);

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
            if (!IsAvailable) return;

            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2.4f) * 0.16f);
            transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!LocalPickupsEnabled || !IsAvailable) return;
            Fighter fighter = other.GetComponent<Fighter>();
            FighterMotor motor = other.GetComponent<FighterMotor>();
            if (fighter == null || fighter.IsEliminated || motor == null) return;

            Color flash;
            if (IsPowerUp)
            {
                PowerUpKind kind = PowerUp.FromContentId(contentId);
                fighter.GrantPowerUp(kind);
                flash = PowerUp.Tint(kind);
            }
            else
            {
                motor.Equip(profile);
                flash = profile.ProjectileColor;
            }

            // Collected drops are consumed outright; the director decides where the next one appears.
            SetAvailable(false);
            CombatVfx.Impact(transform.position, 1, flash, true);
            CombatVfx.Shockwave(transform.position, flash, 0.45f);
        }

        private void SetAvailable(bool available)
        {
            if (trigger != null) trigger.enabled = available;
            if (visuals != null)
            {
                foreach (Renderer item in visuals) item.enabled = available;
            }
            if (available) transform.position = basePosition;
            if (trigger != null) trigger.enabled = available;
        }
    }
}
