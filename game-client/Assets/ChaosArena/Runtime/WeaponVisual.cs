using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// Builds the held weapon out of primitives and rebuilds it whenever the equipped weapon changes, so a
    /// player can tell at a glance what every fighter on screen is carrying. Each weapon has a deliberately
    /// different silhouette: a long single barrel, a stubby twin, a wide multi-barrel, or a fat finned tube.
    /// </summary>
    public sealed class WeaponVisual : MonoBehaviour
    {
        private FighterMotor motor;
        private Transform mount;
        private PrototypeWeaponId built = (PrototypeWeaponId)(-1);
        private bool hasBuilt;

        public void Bind(Transform weaponMount)
        {
            mount = weaponMount;
        }

        private void Awake()
        {
            motor = GetComponent<FighterMotor>();
        }

        private void LateUpdate()
        {
            if (mount == null || motor == null) return;

            if (!hasBuilt || built != motor.WeaponId)
            {
                Rebuild(motor.WeaponId);
                built = motor.WeaponId;
                hasBuilt = true;
            }

            // The weapon always leads the facing direction, mirroring with the fighter.
            mount.localPosition = new Vector3(motor.Facing * 0.5f, -0.06f, -0.32f);
            mount.localScale = new Vector3(motor.Facing, 1f, 1f);
        }

        private void Rebuild(PrototypeWeaponId weapon)
        {
            for (int i = mount.childCount - 1; i >= 0; i--) Destroy(mount.GetChild(i).gameObject);

            PrototypeWeaponProfile profile = PrototypeWeaponProfile.Get(weapon);
            Color casing = new(0.14f, 0.16f, 0.22f);
            Color accent = profile.ProjectileColor;

            switch (weapon)
            {
                case PrototypeWeaponId.PulseSmg:
                    Part("Body", new Vector3(0.16f, 0f, 0f), new Vector3(0.44f, 0.19f, 0.17f), casing);
                    Part("Barrel Upper", new Vector3(0.48f, 0.05f, 0f), new Vector3(0.34f, 0.06f, 0.06f), casing);
                    Part("Barrel Lower", new Vector3(0.48f, -0.05f, 0f), new Vector3(0.34f, 0.06f, 0.06f), casing);
                    Neon("Coil", new Vector3(0.2f, 0.13f, 0f), new Vector3(0.3f, 0.05f, 0.12f), accent);
                    Neon("Magazine", new Vector3(0.1f, -0.17f, 0f), new Vector3(0.12f, 0.22f, 0.1f), accent);
                    break;

                case PrototypeWeaponId.ScatterBlaster:
                    Part("Body", new Vector3(0.14f, 0f, 0f), new Vector3(0.42f, 0.24f, 0.24f), casing);
                    for (int i = -1; i <= 1; i++)
                    {
                        Part($"Barrel {i}", new Vector3(0.44f, i * 0.09f, 0f), new Vector3(0.26f, 0.07f, 0.07f), casing);
                    }
                    Neon("Choke", new Vector3(0.58f, 0f, 0f), new Vector3(0.06f, 0.26f, 0.22f), accent);
                    break;

                case PrototypeWeaponId.RocketLauncher:
                    Part("Tube", new Vector3(0.22f, 0.02f, 0f), new Vector3(0.72f, 0.26f, 0.26f), casing);
                    Part("Fin Top", new Vector3(0.06f, 0.2f, 0f), new Vector3(0.24f, 0.16f, 0.05f), casing);
                    Part("Fin Bottom", new Vector3(0.06f, -0.17f, 0f), new Vector3(0.24f, 0.13f, 0.05f), casing);
                    Neon("Warhead", new Vector3(0.62f, 0.02f, 0f), new Vector3(0.14f, 0.2f, 0.2f), accent);
                    Neon("Sight", new Vector3(0.16f, 0.19f, 0f), new Vector3(0.2f, 0.05f, 0.05f), accent);
                    break;

                default:
                    Part("Body", new Vector3(0.16f, 0f, 0f), new Vector3(0.46f, 0.17f, 0.15f), casing);
                    Part("Barrel", new Vector3(0.56f, 0.01f, 0f), new Vector3(0.36f, 0.08f, 0.08f), casing);
                    Neon("Sight", new Vector3(0.2f, 0.12f, 0f), new Vector3(0.22f, 0.05f, 0.05f), accent);
                    break;
            }
        }

        private void Part(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject piece = Build(name, position, scale);
            PrototypeMaterials.AssignSurface(piece.GetComponent<Renderer>(), color, 0.7f, 0.55f);
        }

        private void Neon(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject piece = Build(name, position, scale);
            PrototypeMaterials.AssignNeon(piece.GetComponent<Renderer>(), color, 1.6f);
        }

        private GameObject Build(string name, Vector3 position, Vector3 scale)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(mount, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            Collider pieceCollider = piece.GetComponent<Collider>();
            pieceCollider.enabled = false;
            Destroy(pieceCollider);
            return piece;
        }
    }
}
