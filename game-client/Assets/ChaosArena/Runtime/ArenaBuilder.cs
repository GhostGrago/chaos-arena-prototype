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

        // Pickups float just above the platform they belong to. Scatter sits on the highest, most contested spot.
        private static readonly (PrototypeWeaponId Weapon, int PlatformIndex)[] PickupPlacement =
        {
            (PrototypeWeaponId.PulseSmg, 1),
            (PrototypeWeaponId.Sniper, 4),
            (PrototypeWeaponId.ScatterBlaster, 6)
        };

        public static void Build()
        {
            Physics.gravity = new Vector3(0f, -22f, 0f);
            Camera existingCamera = Object.FindAnyObjectByType<Camera>();
            if (existingCamera != null) Object.Destroy(existingCamera.gameObject);

            foreach (PlatformDefinition definition in Layout) CreatePlatform(definition);

            foreach ((PrototypeWeaponId weapon, int platformIndex) in PickupPlacement)
            {
                PlatformDefinition platform = Layout[platformIndex];
                WeaponPickup.Spawn(PrototypeWeaponProfile.Get(weapon),
                    new Vector3(platform.Position.x, platform.Top + 0.75f, 0f));
            }

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

            // Platforms read as brushed metal so they separate from the matte fighters and the flat background.
            Color deck = definition.OneWay ? new Color(0.26f, 0.31f, 0.42f) : new Color(0.19f, 0.23f, 0.31f);
            PrototypeMaterials.AssignSurface(platform.GetComponent<Renderer>(), deck, 0.65f, 0.58f);
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
                PrototypeMaterials.AssignNeon(lip.GetComponent<Renderer>(), edgeColor, 1.7f);
            }

            GameObject trim = CreateVisualPrimitive(definition.Name + " Front Trim", PrimitiveType.Cube,
                definition.Position + new Vector3(0f, definition.Scale.y * 0.32f, -definition.Scale.z * 0.52f),
                new Vector3(definition.Scale.x * 0.96f, 0.06f, 0.12f), new Color(0.2f, 0.7f, 1f), true);
            PrototypeMaterials.AssignNeon(trim.GetComponent<Renderer>(), new Color(0.2f, 0.7f, 1f), 1.3f);

            // Under-glow strip so platforms read as lit hardware rather than grey slabs.
            GameObject underGlow = CreateVisualPrimitive(definition.Name + " Under Glow", PrimitiveType.Cube,
                definition.Position + new Vector3(0f, -definition.Scale.y * 0.52f, -definition.Scale.z * 0.3f),
                new Vector3(definition.Scale.x * 0.8f, 0.05f, 0.1f), new Color(1f, 0.35f, 0.75f), true);
            PrototypeMaterials.AssignNeon(underGlow.GetComponent<Renderer>(), new Color(1f, 0.35f, 0.75f), 1.15f);
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
                PrototypeMaterials.AssignNeon(deckLight.GetComponent<Renderer>(), new Color(1f, 0.5f, 0.14f), 1.4f);
            }
        }

        private static void BuildBackground()
        {
            // Layers drift at different rates against the camera, which sells the 2.5D depth without any art.
            GameObject sky = CreateVisualPrimitive("Distant Sky Wall", PrimitiveType.Cube, new Vector3(0f, 8f, 28f),
                new Vector3(90f, 40f, 1f), new Color(0.05f, 0.08f, 0.14f), true);
            sky.AddComponent<ParallaxLayer>().Configure(0.02f);

            Color mountain = new(0.085f, 0.14f, 0.21f);
            for (int i = -4; i <= 4; i++)
            {
                GameObject peak = CreateVisualPrimitive("Distant Peak " + i, PrimitiveType.Cube,
                    new Vector3(i * 8f, -0.5f + Mathf.Abs(i % 2) * 1.2f, 19f + Mathf.Abs(i) * 0.6f),
                    new Vector3(8f, 8f + Mathf.Abs(i % 3) * 3f, 4f), mountain);
                peak.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                peak.AddComponent<ParallaxLayer>().Configure(0.06f);
            }

            Color tower = new(0.07f, 0.105f, 0.155f);
            for (int i = -5; i <= 5; i++)
            {
                float height = 4f + Mathf.Repeat(i * 2.7f, 5f);
                GameObject skyline = CreateVisualPrimitive("Skyline " + i, PrimitiveType.Cube,
                    new Vector3(i * 4.5f, height * 0.5f - 2f, 12f + Mathf.Abs(i % 2) * 2f),
                    new Vector3(3.2f, height, 2.6f), tower);
                skyline.AddComponent<ParallaxLayer>().Configure(0.13f);
            }

            GameObject moon = CreateVisualPrimitive("Distant Moon", PrimitiveType.Sphere, new Vector3(11f, 9.5f, 17f),
                Vector3.one * 3.4f, new Color(0.85f, 0.9f, 1f), true);
            moon.AddComponent<ParallaxLayer>().Configure(0.03f);
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.3f, 0.4f, 0.56f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.17f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.075f, 0.11f, 0.17f);
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 76f;
        }

        private static void BuildLighting()
        {
            GameObject keyObject = new("Key Light");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.3f;
            key.color = new Color(1f, 0.89f, 0.74f);
            keyObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            GameObject fillObject = new("Cool Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.color = new Color(0.35f, 0.58f, 1f);
            fillObject.transform.rotation = Quaternion.Euler(25f, 145f, 0f);

            // Aimed back toward the camera so fighters catch a bright edge and separate from the background.
            GameObject rimObject = new("Rim Light");
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 1.15f;
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
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(0.75f, 0.88f, 1f));

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);

            ColorAdjustments grade = profile.Add<ColorAdjustments>(true);
            grade.contrast.Override(10f);
            grade.saturation.Override(8f);

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
