using System;
using UnityEngine;

namespace ChaosArena
{
    public static class PrototypeMaterials
    {
        private const string LitResourcePath = "ChaosArenaMaterials/PrototypeLit";
        private const string UnlitResourcePath = "ChaosArenaMaterials/PrototypeUnlit";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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
