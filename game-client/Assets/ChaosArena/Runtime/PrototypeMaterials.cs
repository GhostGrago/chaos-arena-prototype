using System;
using UnityEngine;

namespace ChaosArena
{
    public static class PrototypeMaterials
    {
        private const string LitResourcePath = "ChaosArenaMaterials/PrototypeLit";
        private const string UnlitResourcePath = "ChaosArenaMaterials/PrototypeUnlit";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        private static Material litTemplate;
        private static Material unlitTemplate;

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
        /// Translucent, glossy "jelly" surface for fighter bodies. The template material is opaque, so the
        /// blend state is switched at runtime. If a URP variant ever refuses the switch the body simply stays
        /// opaque, which still reads correctly — the neon edge frame carries the silhouette either way.
        /// </summary>
        public static void AssignJelly(Renderer renderer, Color color, float alpha = 0.78f)
        {
            Material instance = new(GetTemplate(false))
            {
                name = "Prototype Jelly (Runtime)",
                enableInstancing = true
            };

            Color body = color;
            body.a = alpha;
            SetMaterialColor(instance, body);
            if (instance.HasProperty(MetallicId)) instance.SetFloat(MetallicId, 0f);
            if (instance.HasProperty(SmoothnessId)) instance.SetFloat(SmoothnessId, 0.92f);

            instance.SetFloat(SurfaceId, 1f);
            instance.SetFloat(BlendId, 0f);
            instance.SetInt(SrcBlendId, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            instance.SetInt(DstBlendId, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            instance.SetInt(ZWriteId, 0);
            instance.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            instance.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            instance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            renderer.sharedMaterial = instance;
        }

        public static void SetColor(Renderer renderer, Color color)
        {
            SetMaterialColor(renderer.material, color);
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
