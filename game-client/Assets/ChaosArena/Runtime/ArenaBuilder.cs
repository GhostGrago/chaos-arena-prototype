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
        private const float PlatformWidthScale = 1.1f;

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

            /// <summary>Peak-to-peak path offset. Zero means the platform never moves.</summary>
            public readonly Vector3 Travel;
            public readonly float Period;
            public readonly float Phase;

            public PlatformDefinition(string name, Vector3 position, Vector3 scale, bool oneWay)
                : this(name, position, scale, oneWay, Vector3.zero, 0f, 0f)
            {
            }

            public PlatformDefinition(string name, Vector3 position, Vector3 scale, bool oneWay,
                Vector3 travel, float period, float phase)
            {
                Name = name;
                Position = position;
                Scale = scale;
                OneWay = oneWay;
                Travel = travel;
                Period = period;
                Phase = phase;
            }

            public bool Moves => Travel != Vector3.zero && Period > 0f;

            /// <summary>Resting surface height. A moving platform reports the centre of its travel.</summary>
            public float Top => Position.y + Scale.y * 0.5f;
        }

        /// <summary>The map currently built. Drops and smoke assertions read the live layout from here.</summary>
        public static ArenaThemeId ActiveThemeId { get; private set; } = ArenaThemeId.NeonCity;
        public static ArenaTheme ActiveTheme { get; private set; } = ArenaTheme.NeonCity;
        public static PlatformDefinition[] Layout => ActiveTheme.Layout;

        /// <summary>Everything this builder makes hangs off one root, so a map swap is a single teardown.</summary>
        private static Transform arenaRoot;

        public static void Build() => Build(ActiveThemeId);

        public static void Build(ArenaThemeId themeId)
        {
            ActiveThemeId = themeId;
            ActiveTheme = ArenaTheme.Get(themeId);

            if (arenaRoot != null)
            {
                // Deactivated before being destroyed: Destroy only takes effect at end of frame, and the old
                // platforms would otherwise still be in OneWayPlatform.ActivePlatforms while the new map is
                // being built, leaving fighters standing on collision that is about to disappear.
                arenaRoot.gameObject.SetActive(false);
                Object.Destroy(arenaRoot.gameObject);
            }

            arenaRoot = new GameObject("Arena").transform;

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

        /// <summary>
        /// Builds one platform as an unscaled root holding a scaled deck plus its trim. The root exists so a
        /// moving platform carries its decorations: parenting them to the scaled deck instead would multiply
        /// their scale by the deck's, stretching every lip to the width of the platform.
        /// </summary>
        private static void CreatePlatform(PlatformDefinition definition)
        {
            ArenaTheme theme = ActiveTheme;

            GameObject root = Adopt(new GameObject(definition.Name));
            root.transform.position = definition.Position;

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Deck";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = Vector3.zero;
            platform.transform.localScale = definition.Scale;

            // Platforms read as plated metal so they separate from the matte fighters and the flat background.
            platformAlbedo ??= Resources.Load<Texture2D>("Surfaces/platform_color");
            platformNormal ??= Resources.Load<Texture2D>("Surfaces/platform_normal");

            Color deck = definition.OneWay ? theme.DeckOneWay : theme.DeckSolid;
            Renderer deckRenderer = platform.GetComponent<Renderer>();
            if (platformAlbedo != null && theme.UsePlatformTexture)
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
            float localTop = definition.Scale.y * 0.5f;
            foreach (int side in new[] { -1, 1 })
            {
                GameObject lip = CreateChildPrimitive($"Edge {side}", root.transform,
                    new Vector3(side * (halfWidth - 0.18f), localTop + 0.03f, -0.2f),
                    new Vector3(0.36f, 0.09f, definition.Scale.z * 0.92f));
                PrototypeMaterials.AssignNeon(lip.GetComponent<Renderer>(), theme.EdgeAccent, 1.15f);
            }

            GameObject trim = CreateChildPrimitive("Front Trim", root.transform,
                new Vector3(0f, definition.Scale.y * 0.32f, -definition.Scale.z * 0.52f),
                new Vector3(definition.Scale.x * 0.96f, 0.06f, 0.12f));
            PrototypeMaterials.AssignNeon(trim.GetComponent<Renderer>(), theme.TrimAccent, 1f);

            // A moving platform is marked along its full travel so the path is readable before stepping on it.
            if (definition.Moves)
            {
                GameObject marker = CreateChildPrimitive("Path Marker", root.transform,
                    new Vector3(0f, -definition.Scale.y * 0.6f, 0f),
                    new Vector3(definition.Scale.x * 0.5f, 0.07f, 0.07f));
                PrototypeMaterials.AssignNeon(marker.GetComponent<Renderer>(), theme.EdgeAccent, 1.6f);

                root.AddComponent<MovingPlatform>().Configure(definition.Travel, definition.Period, definition.Phase);
            }
        }

        /// <summary>Files a newly created object under the arena root so a map swap can remove it.</summary>
        private static GameObject Adopt(GameObject created)
        {
            if (arenaRoot != null) created.transform.SetParent(arenaRoot, true);
            return created;
        }

        private static GameObject CreateChildPrimitive(string name, Transform parent, Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            StripCollider(visual);
            return visual;
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

            switch (ActiveTheme.Backdrop)
            {
                case ArenaTheme.BackdropStyle.City:
                    BuildCityLayer(28f, 3.1f, new Color(0.34f, 0.28f, 0.36f), 0.03f, 13, true, 11);
                    BuildCityLayer(21f, 2.5f, new Color(0.19f, 0.16f, 0.23f), 0.07f, 15, false, 27);
                    BuildCityLayer(14.5f, 2.0f, new Color(0.09f, 0.08f, 0.13f), 0.12f, 13, false, 43);
                    break;
                case ArenaTheme.BackdropStyle.Dunes:
                    BuildDuneLayer(27f, new Color(0.72f, 0.60f, 0.44f), 0.03f, 9f, 5, 17);
                    BuildDuneLayer(20f, new Color(0.62f, 0.49f, 0.34f), 0.07f, 7f, 6, 31);
                    BuildDuneLayer(14f, new Color(0.48f, 0.36f, 0.24f), 0.12f, 5.5f, 7, 53);
                    break;
                case ArenaTheme.BackdropStyle.Starfield:
                    BuildStarfield();
                    break;
                case ArenaTheme.BackdropStyle.Sea:
                    BuildSeaLayer(26f, new Color(0.24f, 0.38f, 0.52f), 0.03f, 3.2f, 19);
                    BuildSeaLayer(19f, new Color(0.18f, 0.31f, 0.45f), 0.07f, 2.4f, 37);
                    BuildSeaLayer(13f, new Color(0.12f, 0.24f, 0.36f), 0.12f, 1.8f, 59);
                    break;
            }
        }

        /// <summary>
        /// Rolling dunes as overlapping flattened spheres. Only silhouette reads at this distance, so the
        /// shapes are crude on purpose and each depth layer costs a single shared material.
        /// </summary>
        private static void BuildDuneLayer(float depth, Color tint, float parallax, float height, int count, int seed)
        {
            Random.InitState(seed);
            Material layerMaterial = PrototypeMaterials.CreateSurfaceMaterial(tint, 0f, 0.1f);

            float spacing = 110f / count;
            for (int i = 0; i < count; i++)
            {
                float x = -55f + i * spacing + Random.Range(-spacing * 0.25f, spacing * 0.25f);
                float width = Random.Range(26f, 44f);
                GameObject dune = Adopt(GameObject.CreatePrimitive(PrimitiveType.Sphere));
                dune.name = "Dune " + i;
                dune.transform.position = new Vector3(x, -9f - height * 0.15f, depth);
                dune.transform.localScale = new Vector3(width, height * Random.Range(0.7f, 1.25f), 6f);
                StripCollider(dune);
                PrototypeMaterials.AssignShared(dune.GetComponent<Renderer>(), layerMaterial, false);
                dune.AddComponent<ParallaxLayer>().Configure(parallax);
            }
        }

        /// <summary>Scattered stars plus two distant bodies. Cheap, and it reads instantly as space.</summary>
        private static void BuildStarfield()
        {
            Random.InitState(97);
            Material starMaterial = PrototypeMaterials.CreateNeonMaterial(new Color(0.85f, 0.9f, 1f), 2.2f);
            for (int i = 0; i < 90; i++)
            {
                GameObject star = Adopt(GameObject.CreatePrimitive(PrimitiveType.Sphere));
                star.name = "Star " + i;
                star.transform.position = new Vector3(Random.Range(-60f, 60f), Random.Range(-10f, 32f), Random.Range(24f, 33f));
                star.transform.localScale = Vector3.one * Random.Range(0.16f, 0.42f);
                StripCollider(star);
                PrototypeMaterials.AssignShared(star.GetComponent<Renderer>(), starMaterial, false);
                star.AddComponent<ParallaxLayer>().Configure(0.02f);
            }

            GameObject planet = Adopt(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            planet.name = "Planet";
            planet.transform.position = new Vector3(-22f, 14f, 30f);
            planet.transform.localScale = Vector3.one * 16f;
            StripCollider(planet);
            PrototypeMaterials.AssignSurface(planet.GetComponent<Renderer>(), new Color(0.3f, 0.22f, 0.44f), 0.1f, 0.3f);
            planet.AddComponent<ParallaxLayer>().Configure(0.016f);

            GameObject moon = Adopt(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            moon.name = "Moon";
            moon.transform.position = new Vector3(19f, 21f, 28f);
            moon.transform.localScale = Vector3.one * 5f;
            StripCollider(moon);
            PrototypeMaterials.AssignSurface(moon.GetComponent<Renderer>(), new Color(0.58f, 0.6f, 0.68f), 0.05f, 0.25f);
            moon.AddComponent<ParallaxLayer>().Configure(0.018f);
        }

        /// <summary>Long low swells. Wide flattened spheres at staggered depths read as open water.</summary>
        private static void BuildSeaLayer(float depth, Color tint, float parallax, float height, int seed)
        {
            Random.InitState(seed);
            Material layerMaterial = PrototypeMaterials.CreateSurfaceMaterial(tint, 0.2f, 0.75f);

            for (int i = 0; i < 7; i++)
            {
                float x = -54f + i * 18f + Random.Range(-4f, 4f);
                GameObject swell = Adopt(GameObject.CreatePrimitive(PrimitiveType.Sphere));
                swell.name = "Swell " + i;
                swell.transform.position = new Vector3(x, -8.5f - height * 0.35f, depth);
                swell.transform.localScale = new Vector3(Random.Range(22f, 34f), height, 5f);
                StripCollider(swell);
                PrototypeMaterials.AssignShared(swell.GetComponent<Renderer>(), layerMaterial, false);
                swell.AddComponent<ParallaxLayer>().Configure(parallax);
            }
        }

        /// <summary>Soft dusk gradient. Deliberately low contrast so the arena stays the brightest thing.</summary>
        private static void BuildDuskSky()
        {
            (Color Tint, float Height, float Y)[] bands = ActiveTheme.SkyBands;

            for (int i = 0; i < bands.Length; i++)
            {
                GameObject band = CreateVisualPrimitive("Sky Band " + i, PrimitiveType.Cube,
                    new Vector3(0f, bands[i].Y, 36f - i * 0.35f),
                    new Vector3(160f, bands[i].Height, 1f), bands[i].Tint, true);
                band.AddComponent<ParallaxLayer>().Configure(0.012f);
            }

            // A single soft light source, dim enough that it does not bloom into a white blob.
            GameObject sun = CreateVisualPrimitive("Sun", PrimitiveType.Sphere,
                ActiveTheme.SunPosition, Vector3.one * ActiveTheme.SunScale, ActiveTheme.SunTint, true);
            PrototypeMaterials.AssignNeon(sun.GetComponent<Renderer>(), ActiveTheme.SunTint, 1.05f);
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
                GameObject building = Adopt(Object.Instantiate(source));
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
            RenderSettings.ambientSkyColor = ActiveTheme.AmbientSky;
            RenderSettings.ambientEquatorColor = ActiveTheme.AmbientEquator;
            RenderSettings.ambientGroundColor = ActiveTheme.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = ActiveTheme.FogTint;
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 76f;
        }

        private static void BuildLighting()
        {
            GameObject keyObject = Adopt(new GameObject("Key Light"));
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            // Low and warm, as if cast by the sun sitting on the horizon behind the city.
            key.intensity = ActiveTheme.KeyIntensity;
            key.color = ActiveTheme.KeyLight;
            keyObject.transform.rotation = Quaternion.Euler(ActiveTheme.KeyAngles);

            GameObject fillObject = Adopt(new GameObject("Cool Fill Light"));
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.color = ActiveTheme.FillLight;
            fillObject.transform.rotation = Quaternion.Euler(25f, 145f, 0f);

            // A magenta kicker from below picks out platform undersides and keeps the palette from going
            // uniformly blue now that the sky carries more colour.
            GameObject kickerObject = Adopt(new GameObject("Under Kicker"));
            Light kicker = kickerObject.AddComponent<Light>();
            kicker.type = LightType.Directional;
            kicker.intensity = 0.22f;
            kicker.color = new Color(1f, 0.4f, 0.8f);
            kickerObject.transform.rotation = Quaternion.Euler(-40f, 60f, 0f);

            // Aimed back toward the camera so fighters catch a bright edge and separate from the background.
            GameObject rimObject = Adopt(new GameObject("Rim Light"));
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.8f;
            rim.color = ActiveTheme.RimLight;
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

            GameObject volumeObject = Adopt(new GameObject("Global Volume"));
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        public static GameObject CreateVisualPrimitive(string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Color color, bool unlit = false)
        {
            GameObject visual = Adopt(GameObject.CreatePrimitive(type));
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
            GameObject visual = Adopt(GameObject.CreatePrimitive(PrimitiveType.Cube));
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
