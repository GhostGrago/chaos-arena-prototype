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
            int count = fighterHit ? 11 : 5;
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(-70f, 70f) + (direction > 0 ? 180f : 0f);
                Vector3 velocity = Quaternion.Euler(0f, 0f, angle) * Vector3.right * Random.Range(3.2f, 7.5f);
                SpawnPiece("Impact Spark", position, velocity, Vector3.one * Random.Range(0.11f, 0.22f),
                    i < 2 ? Color.white : color, Random.Range(0.2f, 0.36f), 2.8f);
            }
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
