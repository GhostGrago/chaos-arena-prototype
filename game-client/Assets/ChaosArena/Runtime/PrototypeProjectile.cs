using UnityEngine;

namespace ChaosArena
{
    public sealed class PrototypeProjectile : MonoBehaviour
    {
        private Fighter owner;
        private Vector3 direction;
        private float expiresAt;
        private PrototypeWeaponProfile weapon;

        public static void Spawn(Fighter newOwner, Vector3 position, Vector3 newDirection, PrototypeWeaponProfile profile)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = $"Projectile_{newOwner.DisplayName}";
            projectile.transform.position = position;
            projectile.transform.localScale = new Vector3(0.34f, 0.16f, 0.16f) * profile.ProjectileScale;
            PrototypeMaterials.Assign(projectile.GetComponent<Renderer>(), profile.ProjectileColor, true);

            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.16f;
            trail.minVertexDistance = 0.03f;
            trail.startWidth = 0.18f;
            trail.endWidth = 0f;
            trail.material = PrototypeMaterials.CreateMaterial(profile.ProjectileColor, true);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Collider collider = projectile.GetComponent<Collider>();
            collider.isTrigger = true;

            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            PrototypeProjectile behavior = projectile.AddComponent<PrototypeProjectile>();
            behavior.owner = newOwner;
            behavior.direction = newDirection.normalized;
            behavior.weapon = profile;
            behavior.expiresAt = Time.time + 2f;
        }

        private void Update()
        {
            transform.position += direction * (weapon.ProjectileSpeed * Time.deltaTime);
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Fighter target = other.GetComponent<Fighter>();
            if (other.GetComponent<PrototypeProjectile>() != null || other.GetComponent<WeaponPickup>() != null ||
                other.GetComponent<CombatVfx>() != null) return;
            if (target == owner)
            {
                return;
            }

            int horizontalDirection = direction.x >= 0f ? 1 : -1;
            if (weapon.ExplosionRadius > 0f)
            {
                Explode(horizontalDirection);
            }
            else if (target != null)
            {
                target.TakeHit(weapon.Damage, new Vector3(horizontalDirection * weapon.Knockback.x, weapon.Knockback.y, 0f));
                CombatVfx.Impact(transform.position, horizontalDirection, weapon.ProjectileColor, true);
                PrototypeAudio.PlayHit(transform.position);
            }
            else
            {
                CombatVfx.Impact(transform.position, horizontalDirection, new Color(0.7f, 0.8f, 0.95f), false);
            }

            Destroy(gameObject);
        }

        private void Explode(int fallbackDirection)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, weapon.ExplosionRadius, ~0, QueryTriggerInteraction.Collide);
            System.Collections.Generic.HashSet<Fighter> affected = new();
            foreach (Collider hit in hits)
            {
                Fighter target = hit.GetComponent<Fighter>();
                if (target == null || target == owner || !affected.Add(target)) continue;
                Vector3 offset = target.transform.position - transform.position;
                float falloff = 1f - Mathf.Clamp01(offset.magnitude / weapon.ExplosionRadius) * 0.45f;
                int pushDirection = Mathf.Abs(offset.x) > 0.08f ? (offset.x > 0f ? 1 : -1) : fallbackDirection;
                target.TakeHit(weapon.Damage * falloff,
                    new Vector3(pushDirection * weapon.Knockback.x * falloff, weapon.Knockback.y * falloff, 0f));
            }

            for (int i = 0; i < 3; i++)
            {
                CombatVfx.Impact(transform.position + Random.insideUnitSphere * 0.35f, i % 2 == 0 ? 1 : -1,
                    weapon.ProjectileColor, true);
            }
            PrototypeAudio.PlayHit(transform.position);
        }
    }
}
