using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChaosArena
{
    /// <summary>
    /// Builds the arena, its background and its lighting. Split out of PrototypeBootstrap in 0.1.6 so level
    /// content can grow without further inflating the bootstrap's match/HUD responsibilities.
    /// </summary>
    public static class ArenaBuilder
    {
        // ambientCG MetalPlates006 (CC0, TP-002 in ASSET_POLICY). Only colour and normal are used; metallic
        // and smoothness stay uniform so platforms keep a consistent look under the neon lighting.
        private static Texture2D platformAlbedo;
        private static Texture2D platformNormal;

        public readonly struct PlatformDefinition
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Scale;
            public readonly bool OneWay;

            public PlatformDefinition(string name, Vector3 position, Vector3 scale, bool oneWay)
            {
                Name = name;
                Position = position;
                Scale = scale;
                OneWay = oneWay;
            }

            public float Top => Position.y + Scale.y * 0.5f;
        }

        /// <summary>
        /// Asymmetric multi-level layout. Vertical gaps stay near 2.0-3.2 units, which a 10.8 jump under the
        /// staged air gravity clears comfortably, so every platform is reachable by more than one route.
        /// </summary>
        public static readonly PlatformDefinition[] Layout =
        {
            new("Main Platform", new Vector3(0f, -0.25f, 0f), new Vector3(19f, 1f, 3f), false),
            new("Left Low Platform", new Vector3(-7f, 2.4f, 0f), new Vector3(4.6f, 0.6f, 3f), true),
            new("Right Low Platform", new Vector3(5.8f, 2f, 0f), new Vector3(5.2f, 0.6f, 3f), true),
            new("Center Mid Platform", new Vector3(-1.2f, 4.5f, 0f), new Vector3(3.8f, 0.6f, 3f), true),
            new("Right High Platform", new Vector3(6.6f, 5.2f, 0f), new Vector3(3.4f, 0.6f, 3f), true),
            new("Left High Platform", new Vector3(-6.2f, 6.4f, 0f), new Vector3(3f, 0.6f, 3f), true),
            new("Top Platform", new Vector3(1f, 7.6f, 0f), new Vector3(3.6f, 0.6f, 3f), true)
        };

        public static void Build()
        {
            Physics.gravity = new Vector3(0f, -22f, 0f);
            Camera existingCamera = Object.FindAnyObjectByType<Camera>();
            if (existingCamera != null) Object.Destroy(existingCamera.gameObject);

            foreach (PlatformDefinition definition in Layout) CreatePlatform(definition);

            BuildBackground();
            BuildArenaDetails();
            ConfigureEnvironment();
            BuildLighting();
            BuildPostProcessing();
        }

        private static void CreatePlatform(PlatformDefinition definition)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = definition.Name;
            platform.transform.SetPositionAndRotation(definition.Position, Quaternion.identity);
            platform.transform.localScale = definition.Scale;

            // Platforms read as plated metal so they separate from the matte fighters and the flat background.
            platformAlbedo ??= Resources.Load<Texture2D>("Surfaces/platform_color");
            platformNormal ??= Resources.Load<Texture2D>("Surfaces/platform_normal");

            Color deck = definition.OneWay ? new Color(0.42f, 0.5f, 0.66f) : new Color(0.32f, 0.38f, 0.5f);
            Renderer deckRenderer = platform.GetComponent<Renderer>();
            if (platformAlbedo != null)
            {
                // Tiling follows the platform's real size, otherwise one plate stretches across a 19-unit deck.
                Vector2 tiling = new(Mathf.Max(1f, definition.Scale.x * 0.45f), Mathf.Max(1f, definition.Scale.z * 0.45f));
                PrototypeMaterials.AssignPanel(deckRenderer, platformAlbedo, platformNormal, deck, 0.75f, 0.55f, tiling);
            }
            else
            {
                PrototypeMaterials.AssignSurface(deckRenderer, deck, 0.65f, 0.58f);
            }
            if (definition.OneWay) platform.AddComponent<OneWayPlatform>();

            // Ring-outs are the main way to lose, so every walkable edge gets a bright lip.
            float halfWidth = definition.Scale.x * 0.5f;
            float top = definition.Top;
            Color edgeColor = new(0.28f, 0.85f, 1f);
            foreach (int side in new[] { -1, 1 })
            {
                GameObject lip = CreateVisualPrimitive($"{definition.Name} Edge {side}", PrimitiveType.Cube,
                    new Vector3(definition.Position.x + side * (halfWidth - 0.18f), top + 0.03f, -0.2f),
                    new Vector3(0.36f, 0.09f, definition.Scale.z * 0.92f), edgeColor, true);
                PrototypeMaterials.AssignNeon(lip.GetComponent<Renderer>(), edgeColor, 1.15f);
            }

            GameObject trim = CreateVisualPrimitive(definition.Name + " Front Trim", PrimitiveType.Cube,
                definition.Position + new Vector3(0f, definition.Scale.y * 0.32f, -definition.Scale.z * 0.52f),
                new Vector3(definition.Scale.x * 0.96f, 0.06f, 0.12f), new Color(0.2f, 0.7f, 1f), true);
            PrototypeMaterials.AssignNeon(trim.GetComponent<Renderer>(), new Color(0.2f, 0.7f, 1f), 1f);

            // Under-glow strip so platforms read as lit hardware rather than grey slabs.
            GameObject underGlow = CreateVisualPrimitive(definition.Name + " Under Glow", PrimitiveType.Cube,
                definition.Position + new Vector3(0f, -definition.Scale.y * 0.52f, -definition.Scale.z * 0.3f),
                new Vector3(definition.Scale.x * 0.8f, 0.05f, 0.1f), new Color(1f, 0.35f, 0.75f), true);
            PrototypeMaterials.AssignNeon(underGlow.GetComponent<Renderer>(), new Color(1f, 0.35f, 0.75f), 0.85f);
        }

        private static void BuildArenaDetails()
        {
            Color support = new(0.08f, 0.11f, 0.16f);
            CreateSurfacePrimitive("Left Support", new Vector3(-6.5f, -2.6f, 0.8f), new Vector3(1.1f, 4f, 1.1f), support, 0.5f, 0.35f);
            CreateSurfacePrimitive("Right Support", new Vector3(6.5f, -2.6f, 0.8f), new Vector3(1.1f, 4f, 1.1f), support, 0.5f, 0.35f);
            CreateSurfacePrimitive("Underdeck", new Vector3(0f, -1.4f, 0.9f), new Vector3(14f, 1.25f, 1.1f), new Color(0.1f, 0.13f, 0.18f), 0.5f, 0.3f);

            for (int i = -4; i <= 4; i++)
            {
                GameObject deckLight = CreateVisualPrimitive("Deck Light " + i, PrimitiveType.Cube,
                    new Vector3(i * 2f, -0.62f, -1.58f), new Vector3(0.5f, 0.12f, 0.08f), new Color(1f, 0.48f, 0.12f), true);
                PrototypeMaterials.AssignNeon(deckLight.GetComponent<Renderer>(), new Color(1f, 0.5f, 0.14f), 1f);
            }
        }

        // Kenney City Kit Commercial (CC0, TP-003 in ASSET_POLICY). The pack ships purpose-built low-detail
        // buildings, which is exactly the right level for scenery this far from the camera.
        private static readonly string[] FarBuildings =
        {
            "City/low-detail-building-a", "City/low-detail-building-b", "City/low-detail-building-c",
            "City/low-detail-building-d", "City/low-detail-building-e", "City/low-detail-building-f",
            "City/low-detail-building-h", "City/low-detail-building-wide-a"
        };

        private static readonly string[] TallBuildings =
        {
            "City/building-skyscraper-a", "City/building-skyscraper-b", "City/building-skyscraper-c"
        };

        /// <summary>
        /// Calm dusk skyline built from real building models.
        ///
        /// The previous procedural version put hundreds of individually lit windows on screen, which read as
        /// visual noise and glare rather than a city. Scenery this far back only ever reads as silhouette, so
        /// the buildings are flat-tinted and unlit, and all the emissive detail is gone.
        /// </summary>
        private static void BuildBackground()
        {
            BuildDuskSky();

            BuildCityLayer(28f, 3.1f, new Color(0.34f, 0.28f, 0.36f), 0.03f, 13, true, 11);
            BuildCityLayer(21f, 2.5f, new Color(0.19f, 0.16f, 0.23f), 0.07f, 15, false, 27);
            BuildCityLayer(14.5f, 2.0f, new Color(0.09f, 0.08f, 0.13f), 0.12f, 13, false, 43);
        }

        /// <summary>Soft dusk gradient. Deliberately low contrast so the arena stays the brightest thing.</summary>
        private static void BuildDuskSky()
        {
            (Color Tint, float Height, float Y)[] bands =
            {
                (new Color(0.10f, 0.10f, 0.20f), 32f, 30f),
                (new Color(0.20f, 0.15f, 0.26f), 16f, 13f),
                (new Color(0.34f, 0.20f, 0.28f), 10f, 4f),
                (new Color(0.52f, 0.28f, 0.24f), 8f, -3f),
                (new Color(0.66f, 0.40f, 0.26f), 6f, -8f)
            };

            for (int i = 0; i < bands.Length; i++)
            {
                GameObject band = CreateVisualPrimitive("Sky Band " + i, PrimitiveType.Cube,
                    new Vector3(0f, bands[i].Y, 36f - i * 0.35f),
                    new Vector3(160f, bands[i].Height, 1f), bands[i].Tint, true);
                band.AddComponent<ParallaxLayer>().Configure(0.012f);
            }

            // A single soft sun, dim enough that it does not bloom into a white blob.
            GameObject sun = CreateVisualPrimitive("Sun", PrimitiveType.Sphere,
                new Vector3(-11f, 0.5f, 34f), Vector3.one * 9f, new Color(0.95f, 0.7f, 0.45f), true);
            PrototypeMaterials.AssignNeon(sun.GetComponent<Renderer>(), new Color(0.95f, 0.68f, 0.44f), 1.05f);
            sun.AddComponent<ParallaxLayer>().Configure(0.014f);
        }

        /// <summary>
        /// One depth layer of buildings. Every building in a layer shares one flat material, so a whole
        /// skyline costs three materials rather than one per object.
        /// </summary>
        private static void BuildCityLayer(float depth, float scale, Color tint, float parallax, int count,
            bool allowTall, int seed)
        {
            Random.InitState(seed);
            Material layerMaterial = PrototypeMaterials.CreateSurfaceMaterial(tint, 0f, 0.12f);

            float spacing = 104f / count;
            for (int i = 0; i < count; i++)
            {
                string[] set = allowTall && Random.value < 0.35f ? TallBuildings : FarBuildings;
                GameObject source = Resources.Load<GameObject>(set[Random.Range(0, set.Length)]);
                if (source == null) continue;

                float x = -52f + i * spacing + Random.Range(-spacing * 0.2f, spacing * 0.2f);
                GameObject building = Object.Instantiate(source);
                building.name = "Building " + i;
                building.transform.position = new Vector3(x, -7.5f, depth);
                building.transform.rotation = Quaternion.Euler(0f, Random.value < 0.5f ? 0f : 180f, 0f);
                building.transform.localScale = new Vector3(scale, scale * Random.Range(0.75f, 1.6f), scale);

                foreach (Renderer part in building.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Collider partCollider in part.GetComponents<Collider>())
                    {
                        partCollider.enabled = false;
                        Object.Destroy(partCollider);
                    }

                    PrototypeMaterials.AssignShared(part, layerMaterial, false);
                }

                building.AddComponent<ParallaxLayer>().Configure(parallax);
            }
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            // Sunset ambience: warm above the horizon, magenta through the middle, near black below.
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.34f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.16f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.04f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.24f, 0.17f, 0.24f);
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 76f;
        }

        private static void BuildLighting()
        {
            GameObject keyObject = new("Key Light");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            // Low and warm, as if cast by the sun sitting on the horizon behind the city.
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.76f, 0.58f);
            keyObject.transform.rotation = Quaternion.Euler(12f, -28f, 0f);

            GameObject fillObject = new("Cool Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.color = new Color(0.35f, 0.58f, 1f);
            fillObject.transform.rotation = Quaternion.Euler(25f, 145f, 0f);

            // A magenta kicker from below picks out platform undersides and keeps the palette from going
            // uniformly blue now that the sky carries more colour.
            GameObject kickerObject = new("Under Kicker");
            Light kicker = kickerObject.AddComponent<Light>();
            kicker.type = LightType.Directional;
            kicker.intensity = 0.22f;
            kicker.color = new Color(1f, 0.4f, 0.8f);
            kickerObject.transform.rotation = Quaternion.Euler(-40f, 60f, 0f);

            // Aimed back toward the camera so fighters catch a bright edge and separate from the background.
            GameObject rimObject = new("Rim Light");
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.8f;
            rim.color = new Color(0.55f, 0.85f, 1f);
            rimObject.transform.rotation = Quaternion.Euler(-12f, 18f, 0f);
        }

        /// <summary>
        /// Global bloom volume. Neon materials are pushed above 1.0 on purpose; without this pass they would
        /// just look like flat bright paint instead of emitting light.
        /// </summary>
        private static void BuildPostProcessing()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.4f);
            bloom.intensity.Override(0.28f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(0.75f, 0.88f, 1f));

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);

            ColorAdjustments grade = profile.Add<ColorAdjustments>(true);
            grade.contrast.Override(6f);
            grade.saturation.Override(4f);

            GameObject volumeObject = new("Global Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        public static GameObject CreateVisualPrimitive(string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Color color, bool unlit = false)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.position = position;
            visual.transform.localScale = scale;
            StripCollider(visual);
            PrototypeMaterials.Assign(visual.GetComponent<Renderer>(), color, unlit);
            return visual;
        }

        private static GameObject CreateSurfacePrimitive(string name, Vector3 position, Vector3 scale, Color color,
            float metallic, float smoothness)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name;
            visual.transform.position = position;
            visual.transform.localScale = scale;
            StripCollider(visual);
            PrototypeMaterials.AssignSurface(visual.GetComponent<Renderer>(), color, metallic, smoothness);
            return visual;
        }

        private static void StripCollider(GameObject target)
        {
            Collider visualCollider = target.GetComponent<Collider>();
            if (visualCollider == null) return;
            visualCollider.enabled = false;
            Object.Destroy(visualCollider);
        }
    }
}
