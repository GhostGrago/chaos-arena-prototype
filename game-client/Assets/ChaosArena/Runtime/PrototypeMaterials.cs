using System;
using UnityEngine;

namespace ChaosArena
{
    public static class PrototypeMaterials
    {
        private const string LitResourcePath = "ChaosArenaMaterials/PrototypeLit";
        private const string UnlitResourcePath = "ChaosArenaMaterials/PrototypeUnlit";
        private const string JellyResourcePath = "ChaosArenaMaterials/PrototypeJelly";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");

        private static Material litTemplate;
        private static Material unlitTemplate;
        private static Material jellyTemplate;

        public static void Assign(Renderer renderer, Color color, bool unlit = false)
        {
            Material template = GetTemplate(unlit);
            Material instance = new(template)
            {
                name = template.name + " (Runtime)",
                enableInstancing = true
            };
            SetMaterialColor(instance, color);
            renderer.sharedMaterial = instance;
        }

        /// <summary>
        /// Assigns a lit material with an explicit surface response. Separating metallic/smoothness per layer
        /// is what stops platforms, fighters and background from all reading as the same flat plastic.
        /// </summary>
        public static void AssignSurface(Renderer renderer, Color color, float metallic, float smoothness)
        {
            Material instance = new(GetTemplate(false))
            {
                name = "Prototype Surface (Runtime)",
                enableInstancing = true
            };
            SetMaterialColor(instance, color);
            if (instance.HasProperty(MetallicId)) instance.SetFloat(MetallicId, Mathf.Clamp01(metallic));
            if (instance.HasProperty(SmoothnessId)) instance.SetFloat(SmoothnessId, Mathf.Clamp01(smoothness));
            renderer.sharedMaterial = instance;
        }

        /// <summary>
        /// Unlit material driven past 1.0 so the bloom pass catches it. This is what makes edge strips and
        /// accents read as emitting light rather than just being brightly painted.
        /// </summary>
        public static void AssignNeon(Renderer renderer, Color color, float intensity = 1.6f)
        {
            Material instance = new(GetTemplate(true))
            {
                name = "Prototype Neon (Runtime)",
                enableInstancing = true
            };
            SetMaterialColor(instance, color * intensity);
            renderer.sharedMaterial = instance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Translucent glossy body surface. The transparent blend state lives in a real material asset rather
        /// than being switched at runtime: flipping an opaque URP material to transparent in code silently
        /// failed in 0.1.9, and PITFALLS already warns against guessing URP material state in code.
        /// </summary>
        public static void AssignJelly(Renderer renderer, Color color, float alpha = 0.72f)
        {
            jellyTemplate ??= Resources.Load<Material>(JellyResourcePath);
            if (jellyTemplate == null)
            {
                throw new InvalidOperationException($"Missing URP material resource: {JellyResourcePath}");
            }

            Material instance = new(jellyTemplate)
            {
                name = "Prototype Jelly (Runtime)",
                enableInstancing = true
            };

            Color body = color;
            body.a = alpha;
            SetMaterialColor(instance, body);
            renderer.sharedMaterial = instance;
        }

        /// <summary>
        /// Lit material that keeps an imported model's own palette texture. Kenney models colour themselves
        /// through a shared atlas rather than per-material colours, so replacing the material with a flat
        /// colour collapsed every part to one shade. Only the texture is adopted; the shader stays ours so
        /// URP never falls back to the magenta error material.
        /// </summary>
        public static void AssignTextured(Renderer renderer, Texture texture, float metallic, float smoothness)
        {
            Material instance = new(GetTemplate(false))
            {
                name = "Prototype Textured (Runtime)",
                enableInstancing = true
            };

            SetMaterialColor(instance, Color.white);
            instance.SetTexture(BaseMapId, texture);
            instance.mainTexture = texture;
            if (instance.HasProperty(MetallicId)) instance.SetFloat(MetallicId, Mathf.Clamp01(metallic));
            if (instance.HasProperty(SmoothnessId)) instance.SetFloat(SmoothnessId, Mathf.Clamp01(smoothness));
            renderer.sharedMaterial = instance;
        }

        /// <summary>
        /// Tinted, tiled surface with a normal map. Tiling is driven by the object's world size so a wide
        /// platform repeats the plates instead of stretching a single tile across its whole face.
        /// </summary>
        public static void AssignPanel(Renderer renderer, Texture albedo, Texture normal, Color tint,
            float metallic, float smoothness, Vector2 tiling)
        {
            Material instance = new(GetTemplate(false))
            {
                name = "Prototype Panel (Runtime)",
                enableInstancing = true
            };

            SetMaterialColor(instance, tint);
            if (albedo != null)
            {
                instance.SetTexture(BaseMapId, albedo);
                instance.mainTexture = albedo;
                instance.SetTextureScale(BaseMapId, tiling);
                instance.mainTextureScale = tiling;
            }

            if (normal != null)
            {
                instance.SetTexture(BumpMapId, normal);
                instance.SetTextureScale(BumpMapId, tiling);
                instance.EnableKeyword("_NORMALMAP");
            }

            if (instance.HasProperty(MetallicId)) instance.SetFloat(MetallicId, Mathf.Clamp01(metallic));
            if (instance.HasProperty(SmoothnessId)) instance.SetFloat(SmoothnessId, Mathf.Clamp01(smoothness));
            renderer.sharedMaterial = instance;
        }

        public static void SetColor(Renderer renderer, Color color)
        {
            SetMaterialColor(renderer.material, color);
        }

        /// <summary>
        /// Reusable emissive material. Background windows and signs run into the hundreds, so they share a
        /// handful of materials instead of each call allocating its own instance.
        /// </summary>
        public static Material CreateNeonMaterial(Color color, float intensity)
        {
            Material material = new(GetTemplate(true))
            {
                name = "Prototype Shared Neon",
                enableInstancing = true
            };
            SetMaterialColor(material, color * intensity);
            return material;
        }

        /// <summary>Assigns an already-created material; no per-object allocation.</summary>
        public static void AssignShared(Renderer renderer, Material material, bool castShadows = true)
        {
            renderer.sharedMaterial = material;
            if (!castShadows) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static Material CreateSurfaceMaterial(Color color, float metallic, float smoothness)
        {
            Material material = new(GetTemplate(false))
            {
                name = "Prototype Shared Surface",
                enableInstancing = true
            };
            SetMaterialColor(material, color);
            if (material.HasProperty(MetallicId)) material.SetFloat(MetallicId, Mathf.Clamp01(metallic));
            if (material.HasProperty(SmoothnessId)) material.SetFloat(SmoothnessId, Mathf.Clamp01(smoothness));
            return material;
        }

        public static Material CreateMaterial(Color color, bool unlit = false)
        {
            Material material = new(GetTemplate(unlit))
            {
                name = "Prototype VFX Material",
                enableInstancing = true
            };
            SetMaterialColor(material, color);
            return material;
        }

        private static Material GetTemplate(bool unlit)
        {
            if (unlit)
            {
                unlitTemplate ??= Resources.Load<Material>(UnlitResourcePath);
                if (unlitTemplate == null)
                {
                    throw new InvalidOperationException($"Missing URP material resource: {UnlitResourcePath}");
                }

                return unlitTemplate;
            }

            litTemplate ??= Resources.Load<Material>(LitResourcePath);
            if (litTemplate == null)
            {
                throw new InvalidOperationException($"Missing URP material resource: {LitResourcePath}");
            }

            return litTemplate;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }

            material.color = color;
        }
    }
}
