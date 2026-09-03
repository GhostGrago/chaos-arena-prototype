using UnityEngine;

namespace ChaosArena
{
    public enum ArenaThemeId { NeonCity, Desert, Starship, Ocean }

    /// <summary>
    /// One arena's layout and look. Everything that differs between maps lives here as data, so a new map is
    /// a table entry rather than a new branch through the builder.
    ///
    /// Layouts share the same reachability rules the original arena was tuned to: vertical gaps stay near
    /// 2.0-3.2 units, which a 10.8 jump clears under the staged air gravity, and no platform is reachable by
    /// only one route. Maps differ in silhouette and in how much of the floor moves, not in whether they are
    /// playable.
    /// </summary>
    public readonly struct ArenaTheme
    {
        public readonly string DisplayName;
        public readonly ArenaBuilder.PlatformDefinition[] Layout;

        public readonly Color DeckSolid;
        public readonly Color DeckOneWay;
        public readonly Color EdgeAccent;
        public readonly Color TrimAccent;

        /// <summary>Sky gradient from top to bottom, as (tint, band height, band centre Y).</summary>
        public readonly (Color Tint, float Height, float Y)[] SkyBands;
        public readonly Color SunTint;
        public readonly Vector3 SunPosition;
        public readonly float SunScale;

        public readonly Color AmbientSky;
        public readonly Color AmbientEquator;
        public readonly Color AmbientGround;
        public readonly Color FogTint;

        public readonly Color KeyLight;
        public readonly float KeyIntensity;
        public readonly Vector3 KeyAngles;
        public readonly Color FillLight;
        public readonly Color RimLight;

        public readonly BackdropStyle Backdrop;
        public readonly bool UsePlatformTexture;

        public enum BackdropStyle { City, Dunes, Starfield, Sea }

        public ArenaTheme(string displayName, ArenaBuilder.PlatformDefinition[] layout, Color deckSolid,
            Color deckOneWay, Color edgeAccent, Color trimAccent, (Color, float, float)[] skyBands, Color sunTint,
            Vector3 sunPosition, float sunScale, Color ambientSky, Color ambientEquator, Color ambientGround,
            Color fogTint, Color keyLight, float keyIntensity, Vector3 keyAngles, Color fillLight, Color rimLight,
            BackdropStyle backdrop, bool usePlatformTexture)
        {
            DisplayName = displayName;
            Layout = layout;
            DeckSolid = deckSolid;
            DeckOneWay = deckOneWay;
            EdgeAccent = edgeAccent;
            TrimAccent = trimAccent;
            SkyBands = skyBands;
            SunTint = sunTint;
            SunPosition = sunPosition;
            SunScale = sunScale;
            AmbientSky = ambientSky;
            AmbientEquator = ambientEquator;
            AmbientGround = ambientGround;
            FogTint = fogTint;
            KeyLight = keyLight;
            KeyIntensity = keyIntensity;
            KeyAngles = keyAngles;
            FillLight = fillLight;
            RimLight = rimLight;
            Backdrop = backdrop;
            UsePlatformTexture = usePlatformTexture;
        }

        public static readonly string[] Labels = { "NEON CITY", "DESERT", "STARSHIP", "OCEAN" };

        public static ArenaTheme Get(ArenaThemeId id) => id switch
        {
            ArenaThemeId.Desert => Desert,
            ArenaThemeId.Starship => Starship,
            ArenaThemeId.Ocean => Ocean,
            _ => NeonCity
        };

        private const float W = 1.1f;

        /// <summary>The original dusk-city arena. Everything is fixed ground; this is the baseline map.</summary>
        public static ArenaTheme NeonCity => new(
            "NEON CITY",
            new ArenaBuilder.PlatformDefinition[]
            {
                new("Main Platform", new Vector3(0f, -0.25f, 0f), new Vector3(19f * W, 1f, 3f), false),
                new("Left Low Platform", new Vector3(-7f, 2.4f, 0f), new Vector3(4.6f * W, 0.6f, 3f), true),
                new("Right Low Platform", new Vector3(5.8f, 2f, 0f), new Vector3(5.2f * W, 0.6f, 3f), true),
                new("Center Mid Platform", new Vector3(-1.2f, 4.5f, 0f), new Vector3(3.8f * W, 0.6f, 3f), true),
                new("Right High Platform", new Vector3(6.6f, 5.2f, 0f), new Vector3(3.4f * W, 0.6f, 3f), true),
                new("Left High Platform", new Vector3(-6.2f, 6.4f, 0f), new Vector3(3f * W, 0.6f, 3f), true),
                new("Top Platform", new Vector3(1f, 7.6f, 0f), new Vector3(3.6f * W, 0.6f, 3f), true)
            },
            new Color(0.32f, 0.38f, 0.5f), new Color(0.42f, 0.5f, 0.66f),
            new Color(0.28f, 0.85f, 1f), new Color(0.2f, 0.7f, 1f),
            new (Color, float, float)[]
            {
                (new Color(0.10f, 0.10f, 0.20f), 32f, 30f),
                (new Color(0.20f, 0.15f, 0.26f), 16f, 13f),
                (new Color(0.34f, 0.20f, 0.28f), 10f, 4f),
                (new Color(0.52f, 0.28f, 0.24f), 8f, -3f),
                (new Color(0.66f, 0.40f, 0.26f), 6f, -8f)
            },
            new Color(0.95f, 0.68f, 0.44f), new Vector3(-11f, 0.5f, 34f), 9f,
            new Color(0.42f, 0.34f, 0.34f), new Color(0.2f, 0.16f, 0.22f), new Color(0.05f, 0.04f, 0.07f),
            new Color(0.24f, 0.17f, 0.24f),
            new Color(1f, 0.76f, 0.58f), 1.15f, new Vector3(12f, -28f, 0f),
            new Color(0.35f, 0.58f, 1f), new Color(0.55f, 0.85f, 1f),
            BackdropStyle.City, true);

        /// <summary>
        /// Wide open sandstone shelves under a hard noon sun. Two slabs slide horizontally, which keeps the
        /// long sight lines from settling into a static sniper duel.
        /// </summary>
        public static ArenaTheme Desert => new(
            "DESERT",
            new ArenaBuilder.PlatformDefinition[]
            {
                new("Main Platform", new Vector3(0f, -0.25f, 0f), new Vector3(18f * W, 1f, 3f), false),
                new("Left Shelf", new Vector3(-7.4f, 2.6f, 0f), new Vector3(4.8f * W, 0.6f, 3f), true),
                new("Right Shelf", new Vector3(7.4f, 2.6f, 0f), new Vector3(4.8f * W, 0.6f, 3f), true),
                new("Sliding Ledge", new Vector3(-2.5f, 4.9f, 0f), new Vector3(3.4f * W, 0.55f, 3f), true,
                    new Vector3(7f, 0f, 0f), 8.5f, 0f),
                new("Drifting Ledge", new Vector3(3.2f, 6.9f, 0f), new Vector3(3f * W, 0.55f, 3f), true,
                    new Vector3(6f, 0f, 0f), 7f, 0.5f),
                new("Mesa Top", new Vector3(-0.4f, 8.8f, 0f), new Vector3(3.2f * W, 0.55f, 3f), true)
            },
            new Color(0.55f, 0.40f, 0.26f), new Color(0.68f, 0.52f, 0.34f),
            new Color(1f, 0.72f, 0.28f), new Color(0.95f, 0.55f, 0.2f),
            new (Color, float, float)[]
            {
                (new Color(0.38f, 0.55f, 0.78f), 32f, 30f),
                (new Color(0.58f, 0.68f, 0.78f), 16f, 13f),
                (new Color(0.80f, 0.76f, 0.66f), 10f, 4f),
                (new Color(0.90f, 0.78f, 0.56f), 8f, -3f),
                (new Color(0.86f, 0.70f, 0.46f), 6f, -8f)
            },
            new Color(1f, 0.94f, 0.76f), new Vector3(9f, 12f, 34f), 6f,
            new Color(0.72f, 0.66f, 0.56f), new Color(0.52f, 0.46f, 0.4f), new Color(0.24f, 0.18f, 0.13f),
            new Color(0.78f, 0.68f, 0.52f),
            new Color(1f, 0.93f, 0.78f), 1.5f, new Vector3(52f, -20f, 0f),
            new Color(0.8f, 0.72f, 0.6f), new Color(1f, 0.86f, 0.62f),
            BackdropStyle.Dunes, true);

        /// <summary>
        /// Tight interior deck of a ship in open space. Compact and vertical, with a lift that runs the full
        /// height of the room, so fights stay close and the whole map is in play at once.
        /// </summary>
        public static ArenaTheme Starship => new(
            "STARSHIP",
            new ArenaBuilder.PlatformDefinition[]
            {
                new("Main Platform", new Vector3(0f, -0.25f, 0f), new Vector3(16f * W, 1f, 3f), false),
                new("Port Deck", new Vector3(-6.4f, 2.5f, 0f), new Vector3(4.2f * W, 0.55f, 3f), true),
                new("Starboard Deck", new Vector3(6.4f, 2.5f, 0f), new Vector3(4.2f * W, 0.55f, 3f), true),
                new("Cargo Lift", new Vector3(0f, 3.4f, 0f), new Vector3(3f * W, 0.55f, 3f), true,
                    new Vector3(0f, 5.2f, 0f), 6.5f, 0f),
                new("Port Catwalk", new Vector3(-4.6f, 6.6f, 0f), new Vector3(3.2f * W, 0.55f, 3f), true),
                new("Starboard Catwalk", new Vector3(4.6f, 6.6f, 0f), new Vector3(3.2f * W, 0.55f, 3f), true),
                new("Bridge", new Vector3(0f, 8.8f, 0f), new Vector3(3.4f * W, 0.55f, 3f), true)
            },
            new Color(0.24f, 0.27f, 0.34f), new Color(0.3f, 0.35f, 0.44f),
            new Color(0.4f, 1f, 0.78f), new Color(0.3f, 0.9f, 0.85f),
            new (Color, float, float)[]
            {
                (new Color(0.02f, 0.02f, 0.06f), 32f, 30f),
                (new Color(0.04f, 0.03f, 0.10f), 16f, 13f),
                (new Color(0.06f, 0.05f, 0.14f), 10f, 4f),
                (new Color(0.05f, 0.04f, 0.11f), 8f, -3f),
                (new Color(0.03f, 0.03f, 0.08f), 6f, -8f)
            },
            new Color(0.55f, 0.75f, 1f), new Vector3(13f, 9f, 34f), 5f,
            new Color(0.20f, 0.24f, 0.34f), new Color(0.12f, 0.14f, 0.22f), new Color(0.03f, 0.03f, 0.06f),
            new Color(0.05f, 0.06f, 0.12f),
            new Color(0.75f, 0.85f, 1f), 0.95f, new Vector3(38f, -34f, 0f),
            new Color(0.3f, 0.9f, 0.85f), new Color(0.5f, 0.95f, 1f),
            BackdropStyle.Starfield, true);

        /// <summary>
        /// Boat decks on open water at first light. The two outer hulls rise and fall out of phase, so the
        /// footing is never level for long and edge play is the point of the map.
        /// </summary>
        public static ArenaTheme Ocean => new(
            "OCEAN",
            new ArenaBuilder.PlatformDefinition[]
            {
                new("Main Platform", new Vector3(0f, -0.25f, 0f), new Vector3(17f * W, 1f, 3f), false),
                new("Port Hull", new Vector3(-7.8f, 2.5f, 0f), new Vector3(4.4f * W, 0.55f, 3f), true,
                    new Vector3(0f, 1.9f, 0f), 5f, 0f),
                new("Starboard Hull", new Vector3(7.8f, 2.5f, 0f), new Vector3(4.4f * W, 0.55f, 3f), true,
                    new Vector3(0f, 1.9f, 0f), 5f, 0.5f),
                new("Midship Deck", new Vector3(0f, 3.9f, 0f), new Vector3(4f * W, 0.55f, 3f), true),
                new("Port Mast", new Vector3(-4.2f, 6.4f, 0f), new Vector3(2.8f * W, 0.55f, 3f), true),
                new("Starboard Mast", new Vector3(4.2f, 6.4f, 0f), new Vector3(2.8f * W, 0.55f, 3f), true),
                new("Crow Nest", new Vector3(0f, 8.4f, 0f), new Vector3(3f * W, 0.55f, 3f), true)
            },
            new Color(0.28f, 0.34f, 0.40f), new Color(0.46f, 0.44f, 0.38f),
            new Color(1f, 0.85f, 0.35f), new Color(0.4f, 0.85f, 0.9f),
            new (Color, float, float)[]
            {
                (new Color(0.14f, 0.22f, 0.40f), 32f, 30f),
                (new Color(0.28f, 0.38f, 0.55f), 16f, 13f),
                (new Color(0.55f, 0.56f, 0.62f), 10f, 4f),
                (new Color(0.80f, 0.62f, 0.48f), 8f, -3f),
                (new Color(0.30f, 0.44f, 0.58f), 6f, -8f)
            },
            new Color(1f, 0.82f, 0.58f), new Vector3(-8f, 2f, 34f), 7f,
            new Color(0.46f, 0.52f, 0.60f), new Color(0.26f, 0.32f, 0.40f), new Color(0.06f, 0.10f, 0.14f),
            new Color(0.42f, 0.50f, 0.60f),
            new Color(1f, 0.84f, 0.66f), 1.2f, new Vector3(16f, -22f, 0f),
            new Color(0.42f, 0.62f, 0.85f), new Color(0.7f, 0.9f, 1f),
            BackdropStyle.Sea, true);
    }
}
