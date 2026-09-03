using UnityEngine;

namespace ChaosArena
{
    public enum PowerUpKind { Shield, Shrink }

    /// <summary>
    /// Look and identity of the collectable power-ups.
    ///
    /// Both are defensive. In a knock-out game the pickup that changes a fight most is one that makes you
    /// harder to remove, and stacking more offence onto whatever weapon a player already holds would just
    /// compress matches further.
    /// </summary>
    public static class PowerUp
    {
        /// <summary>
        /// Power-ups share the replicated pickup byte with weapons rather than adding a field to the network
        /// state, so their ids continue past the weapon ids instead of overlapping them.
        /// </summary>
        public const byte IdBase = 100;

        public static byte ToContentId(PowerUpKind kind) => (byte)(IdBase + (byte)kind);
        public static PowerUpKind FromContentId(byte contentId) => (PowerUpKind)(contentId - IdBase);
        public static bool IsPowerUpId(byte contentId) => contentId >= IdBase;

        public static readonly PowerUpKind[] All = { PowerUpKind.Shield, PowerUpKind.Shrink };

        public static string Name(PowerUpKind kind) => kind switch
        {
            PowerUpKind.Shield => "SHIELD",
            _ => "SHRINK"
        };

        public static Color Tint(PowerUpKind kind) => kind switch
        {
            PowerUpKind.Shield => new Color(0.45f, 0.85f, 1f),
            _ => new Color(0.7f, 1f, 0.45f)
        };

        /// <summary>
        /// Builds the floating drop. The two read apart at a glance by shape as well as colour: the shield is
        /// a ring around a core, the shrink is a stack that tapers to a point.
        /// </summary>
        public static void BuildVisual(Transform root, PowerUpKind kind)
        {
            Color tint = Tint(kind);

            if (kind == PowerUpKind.Shield)
            {
                GameObject core = CreatePart(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.44f);
                PrototypeMaterials.AssignJelly(core.GetComponent<Renderer>(), tint, 0.75f);

                const int segments = 12;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i / (float)segments * Mathf.PI * 2f;
                    GameObject shard = CreatePart(root, PrimitiveType.Cube,
                        new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f),
                        new Vector3(0.09f, 0.2f, 0.09f));
                    shard.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                    PrototypeMaterials.AssignNeon(shard.GetComponent<Renderer>(), tint, 1.7f);
                }
            }
            else
            {
                float[] sizes = { 0.56f, 0.38f, 0.22f };
                for (int i = 0; i < sizes.Length; i++)
                {
                    GameObject step = CreatePart(root, PrimitiveType.Cube,
                        new Vector3(0f, -0.22f + i * 0.24f, 0f), Vector3.one * sizes[i]);
                    PrototypeMaterials.AssignNeon(step.GetComponent<Renderer>(), tint, 1.2f + i * 0.4f);
                }
            }
        }

        private static GameObject CreatePart(Transform root, PrimitiveType type, Vector3 localPosition, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(root, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;

            // Decorative parts must lose their collider immediately; Destroy() alone leaves it live for the
            // rest of the frame, long enough for a passing projectile to trigger against it and vanish.
            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null)
            {
                partCollider.enabled = false;
                Object.Destroy(partCollider);
            }

            return part;
        }
    }
}
