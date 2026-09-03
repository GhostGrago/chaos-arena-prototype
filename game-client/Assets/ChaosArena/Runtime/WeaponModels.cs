using System.Collections.Generic;
using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// Loads the shared weapon meshes. Kenney Blaster Kit (CC0, registered as TP-001 in ASSET_POLICY).
    ///
    /// Muzzle direction is measured per model rather than assumed: the pack is not consistent, so each entry
    /// records which way its barrel points and the base rotation is derived from that.
    /// </summary>
    public static class WeaponModels
    {
        /// <summary>
        /// Per-model data. MuzzleSign records which way the barrel points along the model's local Z, measured
        /// from the meshes: most of the pack aims down -Z but not all of it, so a single shared rotation
        /// would leave some weapons pointing backwards.
        /// </summary>
        private readonly struct WeaponModel
        {
            public readonly string Path;
            public readonly float Scale;
            public readonly int MuzzleSign;

            public WeaponModel(string path, float scale, int muzzleSign)
            {
                Path = path;
                Scale = scale;
                MuzzleSign = muzzleSign;
            }
        }

        // Silhouettes form a deliberate length ladder so weapons are told apart at a glance:
        // pistol 0.42 -> SMG 0.62 -> scatter 0.80 -> sniper 1.39 model units.
        private static readonly Dictionary<PrototypeWeaponId, WeaponModel> Catalogue = new()
        {
            { PrototypeWeaponId.Carbine, new WeaponModel("Weapons/blaster-b", 2.4f, -1) },
            { PrototypeWeaponId.PulseSmg, new WeaponModel("Weapons/blaster-m", 1.8f, 1) },
            { PrototypeWeaponId.ScatterBlaster, new WeaponModel("Weapons/blaster-a", 1.6f, -1) },
            { PrototypeWeaponId.Sniper, new WeaponModel("Weapons/blaster-e", 1.35f, -1) }
        };

        private static Texture2D colormap;

        public static float GetHeldScale(PrototypeWeaponId weapon) =>
            Catalogue.TryGetValue(weapon, out WeaponModel model) ? model.Scale : 1f;

        /// <summary>
        /// Instantiates a weapon mesh under <paramref name="parent"/>, or returns null when the asset is
        /// missing so callers can fall back to primitives rather than showing nothing.
        /// </summary>
        public static GameObject TryCreate(PrototypeWeaponId weapon, Transform parent, float scale)
        {
            if (!Catalogue.TryGetValue(weapon, out WeaponModel model)) return null;

            GameObject source = Resources.Load<GameObject>(model.Path);
            if (source == null) return null;

            GameObject instance = Object.Instantiate(source, parent, false);
            instance.name = "Weapon Model";
            instance.transform.localPosition = Vector3.zero;
            // Turn the barrel onto +X, which is the direction fighters aim along.
            instance.transform.localRotation = Quaternion.Euler(0f, model.MuzzleSign < 0 ? -90f : 90f, 0f);
            instance.transform.localScale = Vector3.one * scale;

            // The pack paints every part from one palette atlas, so keeping that texture is what preserves
            // the colour blocks; flattening it to a project colour turned each gun into a single shade.
            colormap ??= Resources.Load<Texture2D>("Weapons/colormap");

            foreach (Renderer part in instance.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Collider partCollider in part.GetComponents<Collider>())
                {
                    partCollider.enabled = false;
                    Object.Destroy(partCollider);
                }

                if (colormap != null) PrototypeMaterials.AssignTextured(part, colormap, 0.35f, 0.45f);
                else PrototypeMaterials.AssignSurface(part, new Color(0.5f, 0.53f, 0.6f), 0.5f, 0.45f);
            }

            return instance;
        }
    }
}
