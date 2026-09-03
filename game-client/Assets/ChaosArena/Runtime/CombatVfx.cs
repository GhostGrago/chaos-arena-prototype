using UnityEngine;

namespace ChaosArena
{
    public sealed class CombatVfx : MonoBehaviour
    {
        private Vector3 velocity;
        private float expiresAt;
        private float shrinkRate;
        private Vector3 spin;
        private float gravityScale = 0.12f;

        public static void Muzzle(Vector3 position, int direction, Color color)
        {
            SpawnPiece("Muzzle Flash", position + Vector3.right * direction * 0.14f,
                new Vector3(direction * 1.2f, 0f, 0f), new Vector3(0.46f, 0.22f, 0.16f), color, 0.075f, 7f);
            SpawnPiece("Muzzle Core", position, Vector3.zero, Vector3.one * 0.22f, Color.white, 0.05f, 10f);
        }

        public static void Impact(Vector3 position, int direction, Color color, bool fighterHit)
        {
            ImpactDirectional(position, new Vector3(direction, 0f, 0f), color, fighterHit, 0f);
        }

        /// <summary>
        /// Spark spray that follows the actual impulse. The old version only used a horizontal sign, so an
        /// upward launch still sprayed sideways and the hit read as weaker than it was.
        /// </summary>
        public static void ImpactDirectional(Vector3 position, Vector3 hitDirection, Color color, bool fighterHit,
            float weight01)
        {
            Vector3 forward = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector3.right;
            float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            int count = fighterHit ? Mathf.RoundToInt(Mathf.Lerp(9f, 18f, weight01)) : 5;
            float speed = Mathf.Lerp(3.2f, 9.5f, weight01);

            for (int i = 0; i < count; i++)
            {
                // Sparks fan out around the impulse and mostly travel back along it, as debris would.
                float angle = baseAngle + 180f + Random.Range(-62f, 62f);
                Vector3 velocity = Quaternion.Euler(0f, 0f, angle) * Vector3.right * Random.Range(speed * 0.45f, speed);
                SpawnPiece("Impact Spark", position, velocity,
                    Vector3.one * Random.Range(0.11f, 0.22f + weight01 * 0.12f),
                    i < 2 ? Color.white : color, Random.Range(0.2f, 0.36f), 2.8f);
            }
        }

        /// <summary>
        /// Expanding ring at the point of impact, scaled by how hard the hit was. A ring reads at a distance
        /// far better than a spark cloud, which matters most in a three-way brawl where the camera is pulled
        /// back and several fighters are on screen at once.
        /// </summary>
        public static void Shockwave(Vector3 position, Color color, float weight01)
        {
            int segments = 14;
            float startRadius = Mathf.Lerp(0.28f, 0.5f, weight01);
            float endRadius = Mathf.Lerp(0.9f, 2.6f, weight01);
            float lifetime = Mathf.Lerp(0.16f, 0.3f, weight01);
            float thickness = Mathf.Lerp(0.07f, 0.15f, weight01);

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 outward = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Shock Ring";
                shard.transform.position = position + outward * startRadius;
                shard.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                shard.transform.localScale = new Vector3(thickness, thickness * 2.4f, thickness);
                StripCollider(shard);
                PrototypeMaterials.AssignNeon(shard.GetComponent<Renderer>(), color, Mathf.Lerp(1.4f, 2.4f, weight01));

                CombatVfx behavior = shard.AddComponent<CombatVfx>();
                behavior.velocity = outward * ((endRadius - startRadius) / lifetime);
                behavior.expiresAt = Time.time + lifetime;
                behavior.shrinkRate = thickness * 2.4f / lifetime;
                behavior.gravityScale = 0f;
            }
        }

        // The row of ghost blobs that used to be dropped at the moment of impact, and the frozen mesh
        // afterimages that followed a fast fighter, were both withdrawn on playtest feedback: separate
        // ghosts read as a dotted line at arena distance. Both are replaced by the single continuous
        // TrailRenderer streak in FighterVisual, which a hit boosts rather than duplicating.

        private static void StripCollider(GameObject target)
        {
            // Destroy() alone leaves the collider live for the rest of the frame, which is long enough for a
            // projectile to trigger against it and vanish. Always disable first.
            Collider pieceCollider = target.GetComponent<Collider>();
            if (pieceCollider == null) return;
            pieceCollider.enabled = false;
            Destroy(pieceCollider);
        }

        private static void SpawnPiece(string name, Vector3 position, Vector3 velocity, Vector3 scale,
            Color color, float lifetime, float shrinkRate)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.position = position;
            piece.transform.localScale = scale;
            // Destroy() only removes the collider at the end of the frame. Muzzle pieces spawn on top of
            // the projectile that just left the barrel, so the collider must be disabled immediately or the
            // projectile triggers against its own muzzle flash and destroys itself before it can travel.
            Collider pieceCollider = piece.GetComponent<Collider>();
            pieceCollider.enabled = false;
            Destroy(pieceCollider);
            PrototypeMaterials.Assign(piece.GetComponent<Renderer>(), color, true);
            CombatVfx behavior = piece.AddComponent<CombatVfx>();
            behavior.velocity = velocity;
            behavior.expiresAt = Time.time + lifetime;
            behavior.shrinkRate = shrinkRate;
        }

        /// <summary>
        /// Bursts the fighter's body into tumbling jelly blobs. Used for ring-outs and heavy hits so a
        /// knockout reads as the jelly actually coming apart rather than the model simply vanishing.
        /// </summary>
        public static void JellyBurst(Vector3 position, Color color, int count, float sizeScale, float force)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.z *= 0.35f;
                direction = direction.normalized;

                float size = Random.Range(0.14f, 0.34f) * sizeScale;
                SpawnBlob(position + Random.insideUnitSphere * 0.28f,
                    direction * Random.Range(force * 0.35f, force),
                    size, color, Random.Range(0.5f, 1.05f));
            }
        }

        private static void SpawnBlob(Vector3 position, Vector3 velocity, float size, Color color, float lifetime)
        {
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = "Jelly Blob";
            blob.transform.position = position;
            blob.transform.localScale = new Vector3(size, size * Random.Range(0.7f, 1.1f), size);

            Collider blobCollider = blob.GetComponent<Collider>();
            blobCollider.enabled = false;
            Destroy(blobCollider);

            PrototypeMaterials.AssignJelly(blob.GetComponent<Renderer>(), color, 0.85f);

            CombatVfx behavior = blob.AddComponent<CombatVfx>();
            behavior.velocity = velocity;
            behavior.expiresAt = Time.time + lifetime;
            behavior.shrinkRate = size / Mathf.Max(0.1f, lifetime);
            behavior.spin = Random.onUnitSphere * Random.Range(180f, 620f);
            behavior.gravityScale = 0.85f;
        }

        private void Update()
        {
            transform.position += velocity * Time.deltaTime;
            velocity += Physics.gravity * (gravityScale * Time.deltaTime);
            if (spin != Vector3.zero) transform.Rotate(spin * Time.deltaTime, Space.World);
            transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.zero, shrinkRate * Time.deltaTime);
            if (Time.time >= expiresAt) Destroy(gameObject);
        }
    }
}
