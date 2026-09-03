using UnityEngine;

namespace ChaosArena
{
    public enum PrototypeWeaponId { Carbine, PulseSmg, ScatterBlaster, Sniper }

    public readonly struct PrototypeWeaponProfile
    {
        public readonly PrototypeWeaponId Id;
        public readonly string Name;
        public readonly float FireCooldown;
        public readonly float ProjectileSpeed;
        public readonly float Damage;
        public readonly Vector3 Knockback;
        public readonly float ShooterRecoil;
        public readonly float VisualRecoil;
        public readonly Color ProjectileColor;
        public readonly int PickupAmmo;
        public readonly int ProjectilesPerShot;
        public readonly float SpreadDegrees;
        public readonly float ExplosionRadius;
        public readonly float ProjectileScale;

        public bool IsBaseWeapon => Id == PrototypeWeaponId.Carbine;

        public PrototypeWeaponProfile(PrototypeWeaponId id, string name, float fireCooldown, float projectileSpeed,
            float damage, Vector3 knockback, float shooterRecoil, float visualRecoil, Color projectileColor,
            int pickupAmmo, int projectilesPerShot = 1, float spreadDegrees = 0f, float explosionRadius = 0f,
            float projectileScale = 1f)
        {
            Id = id;
            Name = name;
            FireCooldown = fireCooldown;
            ProjectileSpeed = projectileSpeed;
            Damage = damage;
            Knockback = knockback;
            ShooterRecoil = shooterRecoil;
            VisualRecoil = visualRecoil;
            ProjectileColor = projectileColor;
            PickupAmmo = pickupAmmo;
            ProjectilesPerShot = projectilesPerShot;
            SpreadDegrees = spreadDegrees;
            ExplosionRadius = explosionRadius;
            ProjectileScale = projectileScale;
        }

        public static PrototypeWeaponProfile Carbine => new(
            PrototypeWeaponId.Carbine, "CARBINE", 0.32f, 18f, 9f, new Vector3(3.25f, 1.38f, 0f),
            0.22f, 0.16f, new Color(1f, 0.58f, 0.14f), -1);

        public static PrototypeWeaponProfile PulseSmg => new(
            PrototypeWeaponId.PulseSmg, "PULSE SMG", 0.11f, 21f, 3.2f, new Vector3(1.15f, 0.42f, 0f),
            0.07f, 0.07f, new Color(0.2f, 1f, 0.82f), 32, projectileScale: 0.72f);

        public static PrototypeWeaponProfile ScatterBlaster => new(
            PrototypeWeaponId.ScatterBlaster, "SCATTER", 0.72f, 15f, 3.5f, new Vector3(1.25f, 0.5f, 0f),
            0.48f, 0.28f, new Color(1f, 0.2f, 0.68f), 8, 5, 16f, projectileScale: 0.68f);

        /// <summary>
        /// Slow, precise and violent. The round travels almost instantly and the launch is the point: this is
        /// the weapon that makes knockback unmistakable, at the cost of a long cooldown and heavy self-recoil.
        ///
        /// Toned down after playtest. Damage was the real problem rather than the launch itself: internal
        /// damage drives the knockback multiplier up to 3.5x, so 16 per shot ramped a target into one-hit
        /// launch range almost immediately. Damage and shot count come down while the hit stays heavy.
        /// </summary>
        public static PrototypeWeaponProfile Sniper => new(
            PrototypeWeaponId.Sniper, "SNIPER", 1.7f, 46f, 10f, new Vector3(7f, 2.4f, 0f),
            1.25f, 0.5f, new Color(1f, 0.86f, 0.16f), 3, projectileScale: 1.1f);

        public static PrototypeWeaponProfile Get(PrototypeWeaponId id) => id switch
        {
            PrototypeWeaponId.PulseSmg => PulseSmg,
            PrototypeWeaponId.ScatterBlaster => ScatterBlaster,
            PrototypeWeaponId.Sniper => Sniper,
            _ => Carbine
        };
    }
}
