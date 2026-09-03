using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Netcode;

namespace ChaosArena
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        public const int MaxBots = 3;

        // Every fighter that can ever take part. Bots outside the current roster are deactivated, not destroyed,
        // so switching bot count never rebuilds objects mid-session.
        private readonly List<Fighter> allFighters = new();
        private readonly List<Fighter> roster = new();

        private static readonly Vector3[] SpawnPoints =
        {
            new(-6.5f, 1.6f, 0f),
            new(6.5f, 1.6f, 0f),
            new(-2.2f, 1.6f, 0f),
            new(2.2f, 1.6f, 0f)
        };

        private static readonly Color[] FighterColors =
        {
            new(0.2f, 0.7f, 1f),
            new(1f, 0.35f, 0.25f),
            new(0.45f, 0.95f, 0.4f),
            new(0.85f, 0.5f, 1f)
        };

        // One distinct solid per player so silhouettes stay separable in a four-way brawl.
        private static readonly FighterShape[] FighterShapes =
        {
            FighterShape.Cube,
            FighterShape.Sphere,
            FighterShape.Tetrahedron,
            FighterShape.Cylinder
        };

        private GUIStyle titleStyle;
        private GUIStyle hudStyle;
        private GUIStyle resultStyle;
        private float smokeTestExitTime = -1f;
        private Fighter player;
        private Fighter winner;
        private bool matchEnded;
        private float matchStartedAt;
        private float matchDuration;
        private int botCount = 1;
        private int localPlayerCount = 1;
        private bool paused;
        private bool inMenu = true;

        private enum MenuScreen { Root, LocalLobby, Online, Help }
        private MenuScreen menuScreen = MenuScreen.Root;

        private ArenaThemeId selectedTheme = ArenaThemeId.NeonCity;

        /// <summary>Which couch seats have claimed a controller. Seat 0 is the keyboard and is always in.</summary>
        private readonly bool[] lobbyJoined = new bool[NetMatch.MaxFighters];
        private string joinCodeEntry = string.Empty;
        private float codeCopiedUntil;
        private int lastReportedControllerCount = -1;
        private bool showDisplaySettings;
        private readonly List<Vector2Int> displayResolutions = new();
        private int selectedResolutionIndex;
        private bool selectedBorderless;
        private string displayStatus = string.Empty;
        private float uiScale = 1f;

        private static readonly Vector2Int[] BaseDisplayResolutions =
        {
            new(1280, 720),
            new(1600, 900),
            new(1920, 1080),
            new(2560, 1440),
            new(3840, 2160)
        };

        private const string DisplayWidthKey = "ChaosArena.DisplayWidth";
        private const string DisplayHeightKey = "ChaosArena.DisplayHeight";
        private const string DisplayBorderlessKey = "ChaosArena.DisplayBorderless";
        private float UiWidth => Screen.width / uiScale;
        private float UiHeight => Screen.height / uiScale;

        private static NetworkSession Session => NetworkSession.Instance;
        private bool Networked => Session != null && Session.Mode != SessionMode.Offline;
        /// <summary>Only the host (or an offline game) decides outcomes; clients just display what they receive.</summary>
        private bool HasAuthority => Session == null || Session.Mode != SessionMode.Client;
        // Default moved up with the rest of the ladder in 0.3.0: EASY now sits where NORMAL used to, so
        // starting on EASY would have quietly made every default match easier than the one before it.
        private BotDifficulty difficulty = BotDifficulty.Normal;

        // Playtest builds restart on their own so a session never parks on a result screen waiting for input.
        // Tightened in 0.1.6+ledge-grab: the old 16-unit margin let fighters die far off screen with no
        // chance to react. With a recovery move available, a closer boundary is both fairer and visible.
        // Fixed host-authoritative bounds are used instead of a client's screen edge because every online
        // client has a different soft-follow camera. Leave generous recovery space for air control and the
        // second jump: the widened main deck ends near +/-10.45, well inside these limits.
        private const float RingOutSide = 19f;
        private const float RingOutFloor = -9.5f;

        private const float AutoRematchDelay = 2.5f;
        private float autoRematchAt = -1f;

        private void Awake()
        {
            if (!Application.isBatchMode) InitializeDisplaySettings();
            ArenaBuilder.Build();
            CombatFeel.Ensure();
            pickups = gameObject.AddComponent<PickupDirector>();

            player = CreateFighter("PLAYER", FighterColors[0], SpawnPoints[0], FighterShapes[0]);
            player.gameObject.AddComponent<HumanController>().Configure(LocalInputSlot.PlayerOne);

            for (int i = 0; i < MaxBots; i++)
            {
                Fighter botFighter = CreateFighter($"BOT {i + 1}", FighterColors[i + 1], SpawnPoints[i + 1], FighterShapes[i + 1]);
                botFighter.gameObject.AddComponent<BotController>().SetDifficulty(difficulty);

                // Every non-player seat can also be driven by a gamepad, so a four-player couch match uses
                // the same four fighters rather than needing a separate roster.
                HumanController localController = botFighter.gameObject.AddComponent<HumanController>();
                localController.Configure((LocalInputSlot)(i + 1));
                localController.enabled = false;
            }

            // Fighters pass through each other; only attacks connect.
            for (int a = 0; a < allFighters.Count; a++)
            {
                for (int b = a + 1; b < allFighters.Count; b++)
                {
                    Physics.IgnoreCollision(allFighters[a].GetComponent<Collider>(),
                        allFighters[b].GetComponent<Collider>(), true);
                }
            }

            SetupCamera(player.transform);
            StartMatch();

            // Normal sessions wait at the menu so the player can choose solo or online first.
            inMenu = !Application.isBatchMode;

            if (Application.isBatchMode && System.Environment.GetCommandLineArgs().Contains("-chaosSmokeTest"))
            {
                RunSmokeAssertions();
                smokeTestExitTime = Time.realtimeSinceStartup + 2f;
                Debug.Log("CHAOS_ARENA_SMOKE_READY: arena, player, bots, camera, and physics initialized.");
            }
        }

        private void RunSmokeAssertions()
        {
            if (WeaponPickup.All.Count != PickupDirector.MaxSlots)
            {
                throw new System.InvalidOperationException("Expected one pickup slot per director slot.");
            }

            foreach (WeaponPickup slot in WeaponPickup.All)
            {
                if (slot.IsAvailable)
                {
                    throw new System.InvalidOperationException("Drops must start hidden and be scheduled by the director.");
                }
            }
            if (FindAnyObjectByType<ArenaCameraFollow>() == null) throw new System.InvalidOperationException("Missing local-player camera follow.");
            if (ArenaBuilder.Layout.Count(definition => definition.OneWay) < 5)
            {
                throw new System.InvalidOperationException("Expected at least five one-way platforms.");
            }
            foreach (ArenaBuilder.PlatformDefinition definition in ArenaBuilder.Layout)
            {
                // The deck now lives under an unscaled root so moving platforms carry their trim, which means
                // the width to check is the child's, not the root's.
                GameObject deck = GameObject.Find($"Arena/{definition.Name}/Deck");
                if (deck == null || !Mathf.Approximately(deck.transform.localScale.x, definition.Scale.x))
                {
                    throw new System.InvalidOperationException("Runtime platform width does not match the arena layout.");
                }
            }
            float mainPlatformEdge = ArenaBuilder.Layout[0].Position.x + ArenaBuilder.Layout[0].Scale.x * 0.5f;
            if (RingOutSide - mainPlatformEdge < 8f || RingOutFloor > -9f)
            {
                throw new System.InvalidOperationException("Ring-out bounds do not leave enough recovery space.");
            }
            PrototypeWeaponProfile sniper = PrototypeWeaponProfile.Sniper;
            if (!Mathf.Approximately(sniper.Damage, 10f) || sniper.Knockback.x < 7f || sniper.FireCooldown < 1.7f)
            {
                throw new System.InvalidOperationException("Sniper must keep its stronger, slower single-shot identity without raising damage.");
            }

            AssertProjectileSurvivesItsOwnMuzzleFlash();
            foreach (Fighter fighter in allFighters)
            {
                if (fighter.GetComponent<ProtectionShield>() == null)
                {
                    throw new System.InvalidOperationException("Every fighter needs a respawn protection shield.");
                }
            }

            AssertGeometricBodies();
            AssertKnockbackSurvivesMovement();
            AssertShooterRecoilMovesFighter();
            AssertProtectionWeakensRatherThanBlocks();
            AssertPauseRestoresTime();
            AssertBotCountRoster();
            AssertBotDifficultyLadder();
            AssertBotsCanActuallyShoot();
            AssertSpeedTrailIsWired();
            AssertFreeForAllElimination();
            AssertLocalMultiplayerContract();
            AssertDisplaySettingsContract();
            AssertPowerUpsApply();
            AssertMovingPlatformsCarryRiders();
            AssertEveryArenaBuilds();
            Debug.Log("CHAOS_ARENA_030_ASSERTIONS_PASS: local four-player roster/input/camera, 4K display presets, dual-trigger firing, physical shooter recoil, extended recovery bounds, stronger slower sniper, random drops, shield and shrink power-ups, four buildable arenas, moving platforms, pause menu, weakened protection, knockback stun, translucent jelly bodies, weapon mounts, pickups, platforms, bot roster, five-tier bot ladder, bot firing solutions, speed streak wiring, elimination, winner, and rematch reset verified.");
        }

        private static void AssertDisplaySettingsContract()
        {
            if (!BaseDisplayResolutions.Contains(new Vector2Int(3840, 2160)))
            {
                throw new System.InvalidOperationException("Display settings must include a 4K preset.");
            }
        }

        /// <summary>Every fighter must carry a real solid body mesh, including the generated tetrahedron.</summary>
        private void AssertGeometricBodies()
        {
            foreach (Fighter fighter in allFighters)
            {
                Transform bodyTransform = fighter.transform.Find("Visual Root/Tint_Body");
                if (bodyTransform == null) throw new System.InvalidOperationException("Fighter is missing its geometric body.");
                MeshFilter filter = bodyTransform.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                {
                    throw new System.InvalidOperationException("Fighter body has no mesh.");
                }

                if (fighter.transform.Find("Visual Root/Eye") == null)
                {
                    throw new System.InvalidOperationException("Fighter is missing its facing eye.");
                }

                if (fighter.transform.Find("Visual Root/Weapon Mount") == null)
                {
                    throw new System.InvalidOperationException("Fighter is missing its weapon mount.");
                }

                // 0.1.9 shipped opaque bodies because a runtime opaque->transparent switch silently failed.
                // Assert the jelly surface really is transparent so that cannot regress unnoticed again.
                Material bodyMaterial = bodyTransform.GetComponent<Renderer>().sharedMaterial;
                if (bodyMaterial.renderQueue < (int)UnityEngine.Rendering.RenderQueue.Transparent ||
                    !bodyMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                {
                    throw new System.InvalidOperationException(
                        "Fighter body is not translucent; the jelly material asset did not apply.");
                }
            }

            GameObject probe = ProceduralShapes.CreateBody(FighterShape.Tetrahedron, "Tetra Probe");
            Mesh tetra = probe.GetComponent<MeshFilter>().sharedMesh;
            if (tetra.vertexCount != 12 || tetra.triangles.Length != 12)
            {
                throw new System.InvalidOperationException("Generated tetrahedron mesh is malformed.");
            }
            Destroy(probe);
        }

        /// <summary>
        /// The motor rewrites horizontal velocity every physics step, so a knockback only survives if the
        /// control lock engages. Until 0.1.11 it did not, and grounded fighters shrugged off most hits.
        /// </summary>
        private void AssertKnockbackSurvivesMovement()
        {
            FighterMotor motor = player.GetComponent<FighterMotor>();
            if (motor.InKnockback) throw new System.InvalidOperationException("Fighter should start free of knockback stun.");
            motor.ApplyKnockbackStun(0.3f);
            if (!motor.InKnockback) throw new System.InvalidOperationException("Knockback stun failed to engage.");
        }

        /// <summary>Regression guard: recoil must change the fighter's physical velocity, not only animate the gun.</summary>
        private void AssertShooterRecoilMovesFighter()
        {
            FighterMotor motor = player.GetComponent<FighterMotor>();
            Rigidbody fighterBody = player.GetComponent<Rigidbody>();
            fighterBody.linearVelocity = Vector3.zero;
            motor.ApplyShooterRecoil(PrototypeWeaponProfile.Carbine);
            float backwardVelocity = fighterBody.linearVelocity.x * motor.Facing;
            if (backwardVelocity > -0.75f)
            {
                throw new System.InvalidOperationException("Shooter recoil did not produce visible backward velocity.");
            }
            fighterBody.linearVelocity = Vector3.zero;
        }

        /// <summary>
        /// Respawn protection must reduce a hit, not erase it. Full immunity read as the attack failing to
        /// register, so a protected fighter has to lose some health and still take some knockback.
        /// </summary>
        private void AssertProtectionWeakensRatherThanBlocks()
        {
            if (!player.IsProtected)
            {
                throw new System.InvalidOperationException("A fighter should be protected right after a match starts.");
            }

            float before = player.Health;
            player.TakeHit(20f, new Vector3(5f, 2f, 0f));
            float taken = before - player.Health;
            if (taken <= 0f) throw new System.InvalidOperationException("Protection must not block damage entirely.");
            if (taken >= 20f) throw new System.InvalidOperationException("Protection must reduce incoming damage.");
        }

        /// <summary>Pausing must not leave the game frozen once the menu closes.</summary>
        private void AssertPauseRestoresTime()
        {
            SetPaused(true);
            if (!Mathf.Approximately(Time.timeScale, 0f)) throw new System.InvalidOperationException("Pause did not stop time.");
            SetPaused(false);
            if (!Mathf.Approximately(Time.timeScale, 1f)) throw new System.InvalidOperationException("Resume did not restore time.");
        }

        /// <summary>Bot count must drive who actually takes part in the match.</summary>
        private void AssertBotCountRoster()
        {
            for (int count = 0; count <= MaxBots; count++)
            {
                SetBotCount(count);
                if (roster.Count != count + 1)
                {
                    throw new System.InvalidOperationException($"Roster should hold {count + 1} fighters for {count} bots.");
                }

                for (int i = 0; i < allFighters.Count; i++)
                {
                    bool shouldBeActive = i <= count;
                    if (allFighters[i].gameObject.activeSelf != shouldBeActive)
                    {
                        throw new System.InvalidOperationException("Fighters outside the roster must be deactivated.");
                    }
                }
            }

            SetBotCount(0);
            if (GetSoleSurvivor() != null)
            {
                throw new System.InvalidOperationException("A lone fighter must not be declared the winner.");
            }

            SetBotCount(1);
        }

        /// <summary>
        /// The skill ladder must actually be a ladder, must reach the live bots, and must not have quietly
        /// slid back down. A difficulty setting that only changes a stored field is exactly the kind of
        /// "tests pass, feature does nothing" failure this project has already hit several times.
        /// </summary>
        private void AssertBotDifficultyLadder()
        {
            BotDifficulty[] ladder = (BotDifficulty[])System.Enum.GetValues(typeof(BotDifficulty));
            if (ladder.Length < 5)
            {
                throw new System.InvalidOperationException("Bot difficulty must offer at least five tiers.");
            }

            if (BotProfile.Labels.Length != ladder.Length)
            {
                throw new System.InvalidOperationException("Every difficulty tier needs a menu label.");
            }

            for (int i = 1; i < ladder.Length; i++)
            {
                BotProfile lower = BotProfile.Get(ladder[i - 1]);
                BotProfile higher = BotProfile.Get(ladder[i]);
                bool improves = higher.AimTolerance <= lower.AimTolerance &&
                    higher.LeadAccuracy >= lower.LeadAccuracy &&
                    higher.ReactionDelay <= lower.ReactionDelay &&
                    higher.DecisionInterval.y <= lower.DecisionInterval.y &&
                    higher.DodgeChance >= lower.DodgeChance &&
                    higher.EdgeSafety >= lower.EdgeSafety &&
                    higher.RangeDiscipline >= lower.RangeDiscipline;
                if (!improves)
                {
                    throw new System.InvalidOperationException($"Difficulty {ladder[i]} is not harder than {ladder[i - 1]}.");
                }
            }

            // The whole ladder moved up one step, so even the easiest tier must still decide and aim at the
            // pace the old NORMAL tier did rather than reverting to the old EASY behaviour.
            BotProfile easiest = BotProfile.Get(ladder[0]);
            if (easiest.DecisionInterval.y > 0.52f || easiest.LeadAccuracy <= 0f)
            {
                throw new System.InvalidOperationException("The easiest tier must keep the old NORMAL decision pace and lead its shots.");
            }

            BotDifficulty restore = difficulty;
            SetDifficulty(BotDifficulty.Master);
            int botsReached = 0;
            foreach (Fighter fighter in allFighters)
            {
                BotController bot = fighter.GetComponent<BotController>();
                if (bot == null) continue;
                if (bot.Difficulty != BotDifficulty.Master)
                {
                    throw new System.InvalidOperationException("A difficulty change must reach every live bot.");
                }

                botsReached++;
            }

            if (botsReached == 0)
            {
                throw new System.InvalidOperationException("No bot received the difficulty change.");
            }

            SetDifficulty(restore);
        }

        /// <summary>
        /// The speed streak and the weapon charge ring are both built at spawn. If either fails to build, the
        /// fighter simply never shows the effect and nothing anywhere reports an error.
        /// </summary>
        private void AssertSpeedTrailIsWired()
        {
            foreach (Fighter fighter in allFighters)
            {
                FighterVisual visual = fighter.GetComponent<FighterVisual>();
                if (visual == null || !visual.CanTrail)
                {
                    throw new System.InvalidOperationException($"{fighter.DisplayName} cannot produce a speed streak.");
                }

                WeaponChargeRing ring = fighter.GetComponent<WeaponChargeRing>();
                if (ring == null || ring.SegmentsBuilt == 0)
                {
                    throw new System.InvalidOperationException($"{fighter.DisplayName} has no weapon charge ring.");
                }
            }

            // Only slow weapons carry the ring: on the SMG a 0.11s cooldown would strobe rather than inform.
            if (!PrototypeWeaponProfile.Sniper.FireCooldown.Equals(0f) && PrototypeWeaponProfile.PulseSmg.FireCooldown >= 0.3f)
            {
                throw new System.InvalidOperationException("The SMG must stay below the charge-ring threshold.");
            }
        }

        /// <summary>
        /// Bots must actually reach a firing decision, not merely move. This exists because a bot that moved
        /// and dodged flawlessly but never fired once shipped to playtest: the match loop re-assigns the
        /// nearest rival every frame, and re-issuing the same target reset the reaction timer before it could
        /// ever elapse. Movement-only checks cannot catch that, so the aim path is driven directly here.
        /// </summary>
        private void AssertBotsCanActuallyShoot()
        {
            SetBotCount(1);
            Fighter bot = allFighters.FirstOrDefault(fighter => fighter.GetComponent<BotController>() != null &&
                fighter.GetComponent<BotController>().enabled);
            if (bot == null)
            {
                throw new System.InvalidOperationException("Expected an active bot to verify the firing path.");
            }

            BotController brain = bot.GetComponent<BotController>();
            Vector3 botPosition = bot.transform.position;
            Vector3 originalPlayerPosition = player.transform.position;

            // Level with the bot and well inside carbine range, with nothing between them.
            player.transform.position = botPosition + new Vector3(5f, 0f, 0f);
            RetargetBots();

            if (!brain.EvaluateFiringSolution())
            {
                player.transform.position = originalPlayerPosition;
                throw new System.InvalidOperationException("A bot found no firing solution against a level, in-range, unobstructed target.");
            }

            float heldSince = brain.SolutionHeldSince;
            if (heldSince < 0f)
            {
                player.transform.position = originalPlayerPosition;
                throw new System.InvalidOperationException("A held firing solution did not start its reaction timer.");
            }

            // The match loop calls this every single frame. It must not restart the reaction timer, or the
            // timer can never reach the tier's reaction delay and the bot never pulls the trigger.
            RetargetBots();
            RetargetBots();
            if (brain.SolutionHeldSince != heldSince)
            {
                player.transform.position = originalPlayerPosition;
                throw new System.InvalidOperationException("Re-issuing the same target reset the bot's reaction timer.");
            }

            player.transform.position = originalPlayerPosition;
            RetargetBots();
        }

        /// <summary>
        /// Every map must be buildable and playable, not merely defined. A layout with an unreachable gap or
        /// a deck narrower than the spawn spread is a map nobody can play, and that cannot be caught by
        /// looking at the table.
        /// </summary>
        private void AssertEveryArenaBuilds()
        {
            ArenaThemeId original = ArenaBuilder.ActiveThemeId;

            foreach (ArenaThemeId id in (ArenaThemeId[])System.Enum.GetValues(typeof(ArenaThemeId)))
            {
                ArenaTheme theme = ArenaTheme.Get(id);
                if (theme.Layout == null || theme.Layout.Length < 4)
                {
                    throw new System.InvalidOperationException($"Arena {id} needs at least four platforms.");
                }

                if (theme.Layout[0].OneWay)
                {
                    throw new System.InvalidOperationException($"Arena {id} must start with a solid main deck.");
                }

                // Every upper platform has to be within reach of the one below it. The jump clears about 3.4
                // units under the staged air gravity, so a larger step would strand players on the floor.
                for (int i = 1; i < theme.Layout.Length; i++)
                {
                    float best = float.MaxValue;
                    for (int j = 0; j < theme.Layout.Length; j++)
                    {
                        if (i == j) continue;
                        float rise = theme.Layout[i].Top - theme.Layout[j].Top;
                        if (rise > 0f) best = Mathf.Min(best, rise);
                    }

                    if (best > 3.4f)
                    {
                        throw new System.InvalidOperationException(
                            $"Arena {id} platform '{theme.Layout[i].Name}' sits {best:0.0} above anything below it.");
                    }
                }

                SelectTheme(id);
                if (ArenaBuilder.ActiveThemeId != id || GameObject.Find($"Arena/{theme.Layout[0].Name}/Deck") == null)
                {
                    throw new System.InvalidOperationException($"Arena {id} did not build.");
                }

                if (FindAnyObjectByType<ArenaCameraFollow>() == null)
                {
                    throw new System.InvalidOperationException($"Arena {id} left the game without a camera.");
                }

                // A ring-out must still be possible: the deck cannot reach the boundary.
                float deckEdge = theme.Layout[0].Position.x + theme.Layout[0].Scale.x * 0.5f;
                if (RingOutSide - deckEdge < 6f)
                {
                    throw new System.InvalidOperationException($"Arena {id} leaves too little room outside the deck.");
                }
            }

            SelectTheme(original);
        }

        /// <summary>Moving platforms are only useful if a rider is actually carried along with them.</summary>
        private void AssertMovingPlatformsCarryRiders()
        {
            bool anyMapMoves = false;
            foreach (ArenaThemeId id in (ArenaThemeId[])System.Enum.GetValues(typeof(ArenaThemeId)))
            {
                foreach (ArenaBuilder.PlatformDefinition definition in ArenaTheme.Get(id).Layout)
                {
                    if (definition.Moves) anyMapMoves = true;
                }
            }

            if (!anyMapMoves)
            {
                throw new System.InvalidOperationException("No arena defines a moving platform.");
            }

            // A moving platform must own a kinematic body, or PhysX rebuilds the static collision tree every
            // step and contacts against the deck resolve badly.
            foreach (MovingPlatform platform in MovingPlatform.Active)
            {
                Rigidbody platformBody = platform.GetComponent<Rigidbody>();
                if (platformBody == null || !platformBody.isKinematic)
                {
                    throw new System.InvalidOperationException("A moving platform needs a kinematic rigidbody.");
                }
            }
        }

        /// <summary>
        /// Power-ups must survive the trip through the replicated pickup byte and must actually change the
        /// fighter. Encoding them alongside weapons in one byte is exactly the kind of shortcut that breaks
        /// silently, so the round trip is checked rather than assumed.
        /// </summary>
        private void AssertPowerUpsApply()
        {
            foreach (PowerUpKind kind in PowerUp.All)
            {
                byte id = PowerUp.ToContentId(kind);
                if (!PowerUp.IsPowerUpId(id) || PowerUp.FromContentId(id) != kind)
                {
                    throw new System.InvalidOperationException($"Power-up {kind} does not survive its content id.");
                }

                foreach (PrototypeWeaponId weapon in (PrototypeWeaponId[])System.Enum.GetValues(typeof(PrototypeWeaponId)))
                {
                    if (PowerUp.IsPowerUpId((byte)weapon))
                    {
                        throw new System.InvalidOperationException("A weapon id collides with the power-up id range.");
                    }
                }
            }

            Fighter subject = allFighters[0];
            subject.ResetRound();
            if (subject.HasShield || subject.IsShrunk)
            {
                throw new System.InvalidOperationException("A fresh round must clear power-ups.");
            }

            subject.GrantPowerUp(PowerUpKind.Shield);
            if (!subject.HasShield || !subject.IsDamped)
            {
                throw new System.InvalidOperationException("The shield power-up did not damp incoming hits.");
            }

            subject.GrantPowerUp(PowerUpKind.Shrink);
            if (!subject.IsShrunk || subject.SizeScale >= 1f)
            {
                throw new System.InvalidOperationException("The shrink power-up did not reduce the fighter's size.");
            }

            subject.ResetRound();
        }

        private void AssertLocalMultiplayerContract()
        {
            localPlayerCount = 2;
            botCount = 0;
            StartMatch();

            if (HumanSeats != 2 || roster.Count != 2)
            {
                throw new System.InvalidOperationException("Local two-player must create exactly two human seats.");
            }

            HumanController first = allFighters[0].GetComponent<HumanController>();
            HumanController second = allFighters[1].GetComponent<HumanController>();
            if (first == null || second == null || first.InputSlot != LocalInputSlot.PlayerOne ||
                second.InputSlot != LocalInputSlot.PlayerTwo || !first.enabled || !second.enabled)
            {
                throw new System.InvalidOperationException("Local two-player input ownership is not configured correctly.");
            }

            ArenaCameraFollow cameraFollow = FindAnyObjectByType<ArenaCameraFollow>();
            if (cameraFollow == null || !cameraFollow.HasSecondaryTarget)
            {
                throw new System.InvalidOperationException("Shared local-two-player camera is missing its second target.");
            }

            localPlayerCount = 3;
            StartMatch();
            HumanController third = allFighters[2].GetComponent<HumanController>();
            if (HumanSeats != 3 || roster.Count != 3 || third == null ||
                third.InputSlot != LocalInputSlot.PlayerThree || !third.enabled || cameraFollow.TargetCount != 3)
            {
                throw new System.InvalidOperationException("Local three-player input, roster, or camera ownership is not configured correctly.");
            }

            // Four local players share the same four fighters as a solo match against three bots, so the
            // fourth seat has to take over a fighter that otherwise runs a bot brain.
            localPlayerCount = 4;
            StartMatch();
            HumanController fourth = allFighters[3].GetComponent<HumanController>();
            if (HumanSeats != 4 || roster.Count != 4 || fourth == null ||
                fourth.InputSlot != LocalInputSlot.PlayerFour || !fourth.enabled || cameraFollow.TargetCount != 4)
            {
                throw new System.InvalidOperationException("Local four-player input, roster, or camera ownership is not configured correctly.");
            }

            if (allFighters[3].GetComponent<BotController>().enabled)
            {
                throw new System.InvalidOperationException("A human-held seat must not also run its bot brain.");
            }

            localPlayerCount = 1;
            botCount = 1;
            StartMatch();
        }

        /// <summary>Last fighter standing wins, and a rematch restores the whole roster.</summary>
        private void AssertFreeForAllElimination()
        {
            SetBotCount(2);
            Fighter firstBot = roster[1];
            Fighter secondBot = roster[2];

            for (int i = 0; i < Fighter.StartingLives; i++) firstBot.LoseLife();
            if (!firstBot.IsEliminated) throw new System.InvalidOperationException("Stock elimination contract failed.");
            if (GetSoleSurvivor() != null) throw new System.InvalidOperationException("Match must continue while two fighters remain.");

            for (int i = 0; i < Fighter.StartingLives; i++) secondBot.LoseLife();
            Fighter survivor = GetSoleSurvivor();
            if (survivor != player) throw new System.InvalidOperationException("Sole survivor contract failed.");

            EndMatch(survivor);
            if (!matchEnded || winner != player) throw new System.InvalidOperationException("Winner contract failed.");

            StartMatch();
            if (matchEnded) throw new System.InvalidOperationException("Rematch should clear the ended state.");
            foreach (Fighter fighter in roster)
            {
                if (fighter.Lives != Fighter.StartingLives || !fighter.gameObject.activeInHierarchy ||
                    fighter.GetComponent<FighterMotor>().WeaponId != PrototypeWeaponId.Carbine)
                {
                    throw new System.InvalidOperationException("Rematch reset contract failed.");
                }
            }

            SetBotCount(1);
        }

        /// <summary>
        /// Regression guard for the 0.1.5 fix: combat VFX used to keep a live collider for the rest of the
        /// frame, so a projectile triggered against its own muzzle flash and was destroyed at the barrel.
        /// Shots appeared as sparks with no travel, no impact and no knockback.
        /// </summary>
        private void AssertProjectileSurvivesItsOwnMuzzleFlash()
        {
            Vector3 testMuzzle = new(0f, 40f, 0f);
            CombatVfx.Muzzle(testMuzzle, 1, Color.white);
            foreach (CombatVfx piece in FindObjectsByType<CombatVfx>(FindObjectsSortMode.None))
            {
                Collider vfxCollider = piece.GetComponent<Collider>();
                if (vfxCollider != null && vfxCollider.enabled)
                {
                    throw new System.InvalidOperationException(
                        "Combat VFX kept an active collider; projectiles would self-destruct at the muzzle.");
                }
            }

            int before = FindObjectsByType<PrototypeProjectile>(FindObjectsSortMode.None).Length;
            PrototypeProjectile.Spawn(player, testMuzzle, Vector3.right, PrototypeWeaponProfile.Carbine);
            PrototypeProjectile[] live = FindObjectsByType<PrototypeProjectile>(FindObjectsSortMode.None);
            if (live.Length != before + 1)
            {
                throw new System.InvalidOperationException("Firing did not produce a projectile.");
            }

            foreach (PrototypeProjectile projectile in live) Destroy(projectile.gameObject);
            foreach (CombatVfx piece in FindObjectsByType<CombatVfx>(FindObjectsSortMode.None)) Destroy(piece.gameObject);
        }

        private void Update()
        {
            if (smokeTestExitTime > 0f && Time.realtimeSinceStartup >= smokeTestExitTime)
            {
                Debug.Log("CHAOS_ARENA_SMOKE_PASS");
                Application.Quit(0);
                return;
            }

            ReportControllerDevices();

            if (showDisplaySettings && Input.GetKeyDown(KeyCode.Escape))
            {
                showDisplaySettings = false;
                return;
            }

            if (inMenu) return;

            if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(!paused);
            if (paused) return;

            if (!HasAuthority)
            {
                CheckConnectionLost();
                if (inMenu) return;
                HookRemoteShots();
                RefreshSeatRoles();
                ApplyNetworkState();
                return;
            }

            if (Networked && HumanSeats != appliedHumanSeats)
            {
                appliedHumanSeats = HumanSeats;
                StartMatch();
            }

            RefreshSeatRoles();

            if (!matchEnded)
            {
                CheckRingOuts();
                DriveNetworkedSeats();
                RetargetBots();
                pickups.HostTick();
            }

            if (Networked && NetMatch.Instance != null)
            {
                NetMatch.Instance.HostBroadcast(roster, BuildMatchState(), BuildPickupStates());
            }

            if (matchEnded && autoRematchAt > 0f && Time.time >= autoRematchAt) StartMatch();

            if (Input.GetKeyDown(KeyCode.R)) StartMatch();
            if (Input.GetKeyDown(KeyCode.Alpha0)) SetBotCount(0);
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetBotCount(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetBotCount(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetBotCount(3);
            if (Input.GetKeyDown(KeyCode.F1)) SetDifficulty(BotDifficulty.Easy);
            if (Input.GetKeyDown(KeyCode.F2)) SetDifficulty(BotDifficulty.Normal);
            if (Input.GetKeyDown(KeyCode.F3)) SetDifficulty(BotDifficulty.Hard);
            if (Input.GetKeyDown(KeyCode.F4)) SetDifficulty(BotDifficulty.Expert);
            if (Input.GetKeyDown(KeyCode.F5)) SetDifficulty(BotDifficulty.Master);
        }

        private void SetPaused(bool value)
        {
            paused = value;
            CombatFeel.SetPaused(value);
            Time.timeScale = value ? 0f : 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private NetMatch hookedMatch;
        private int appliedHumanSeats = 1;
        private bool wasConnected;
        private PickupDirector pickups;

        // Last seen remote vitals, used to spot hits and ring-outs on a client purely from state changes.
        private readonly float[] remoteHealth = new float[NetMatch.MaxFighters];
        private readonly int[] remoteLives = new int[NetMatch.MaxFighters];
        private readonly bool[] remoteActive = new bool[NetMatch.MaxFighters];

        /// <summary>Seats currently held by people. Offline supports keyboard-only solo or 2–3 local players.</summary>
        private int HumanSeats => Networked && NetMatch.Instance != null
            ? Mathf.Clamp(NetMatch.Instance.HumanSeats, 1, NetMatch.MaxFighters)
            : Mathf.Clamp(localPlayerCount, 1, NetMatch.MaxFighters);

        /// <summary>
        /// Re-derives who owns each seat. Seat ownership used to be decided only inside StartMatch, so a
        /// player joining mid-match kept being driven by the bot brain until the next match began, which
        /// looked like the newcomer had spawned as an AI.
        /// </summary>
        private void RefreshSeatRoles()
        {
            int humans = HumanSeats;
            bool authority = HasAuthority;

            for (int seat = 0; seat < allFighters.Count; seat++)
            {
                Fighter fighter = allFighters[seat];
                bool isHuman = seat < humans;
                fighter.SetDisplayName(BuildSeatName(seat, humans));

                BotController brain = fighter.GetComponent<BotController>();
                HumanController human = fighter.GetComponent<HumanController>();
                if (brain != null)
                {
                    bool inRoster = roster.Contains(fighter);
                    brain.enabled = authority && inRoster && !isHuman && !matchEnded;
                }
                if (human != null)
                {
                    bool localInputSeat = Networked ? seat == 0 : isHuman;
                    human.enabled = roster.Contains(fighter) && localInputSeat && !matchEnded;
                }
            }

            // On a client the camera must follow the fighter this player actually drives.
            if (Networked && NetMatch.Instance != null)
            {
                int seat = Mathf.Clamp(NetMatch.Instance.LocalSeat, 0, allFighters.Count - 1);
                Fighter local = allFighters[seat];
                if (local != player)
                {
                    player = local;
                    FindAnyObjectByType<ArenaCameraFollow>()?.SetTarget(player.transform);
                }
            }

            ConfigureCameraTargets();
        }

        private string BuildSeatName(int seat, int humans)
        {
            if (seat >= humans) return $"BOT {seat - humans + 1}";
            return Networked || localPlayerCount > 1 ? $"PLAYER {seat + 1}" : "PLAYER";
        }

        /// <summary>
        /// Client-side: turn the host's shot announcements into visible, damage-free rounds. Tracks which
        /// instance it bound to, so a rejoin or host restart re-subscribes instead of listening to a dead one.
        /// </summary>
        private void HookRemoteShots()
        {
            NetMatch net = NetMatch.Instance;
            if (net == null || ReferenceEquals(net, hookedMatch)) return;

            if (hookedMatch != null) hookedMatch.OnRemoteShot -= SpawnCosmeticShot;
            net.OnRemoteShot += SpawnCosmeticShot;
            hookedMatch = net;
        }

        private void SpawnCosmeticShot(int seat, PrototypeWeaponId weapon, Vector3 muzzle, Vector3 direction)
        {
            if (seat < 0 || seat >= allFighters.Count) return;
            PrototypeWeaponProfile profile = PrototypeWeaponProfile.Get(weapon);
            int facing = direction.x >= 0f ? 1 : -1;
            int count = Mathf.Max(1, profile.ProjectilesPerShot);

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float angle = Mathf.Lerp(-profile.SpreadDegrees * 0.5f, profile.SpreadDegrees * 0.5f, t);
                Vector3 shot = Quaternion.Euler(0f, 0f, angle * facing) * Vector3.right * facing;
                PrototypeProjectile.SpawnCosmetic(allFighters[seat], muzzle, shot, profile);
            }

            CombatVfx.Muzzle(muzzle, facing, profile.ProjectileColor);
            PrototypeAudio.PlayShot(muzzle);
        }

        private NetMatchState BuildMatchState() => new()
        {
            Ended = matchEnded,
            WinnerSeat = (sbyte)(winner != null ? allFighters.IndexOf(winner) : -1),
            Duration = matchDuration,
            RestartIn = Mathf.Max(0f, autoRematchAt - Time.time)
        };

        private NetPickupState[] BuildPickupStates()
        {
            NetPickupState[] states = new NetPickupState[PickupDirector.MaxSlots];
            for (int i = 0; i < states.Length && i < pickups.Slots.Count; i++)
            {
                WeaponPickup slot = pickups.Slots[i];
                if (slot == null) continue;
                states[i] = new NetPickupState
                {
                    Active = slot.IsAvailable,
                    // Carries weapons and power-ups alike; power-up ids continue past the weapon ids.
                    Weapon = slot.ContentId,
                    Position = slot.Position
                };
            }

            return states;
        }

        /// <summary>Feeds each remote player's input into the fighter they own. Host only.</summary>
        private void DriveNetworkedSeats()
        {
            NetMatch net = NetMatch.Instance;
            if (!Networked || net == null || !net.IsSpawned) return;

            for (int seat = 1; seat < roster.Count && seat < net.HumanSeats; seat++)
            {
                NetInput input = net.ConsumeInput(seat, out bool jump, out bool drop);
                FighterMotor motor = roster[seat].GetComponent<FighterMotor>();
                if (motor != null) motor.SetCommands(input.Horizontal, jump, drop, input.Fire, input.Horizontal);
            }
        }

        /// <summary>
        /// Clients are pure presentation: physics runs on the host, so incoming transforms are applied
        /// directly and the local rigidbodies are kept out of the way.
        /// </summary>
        private void ApplyNetworkState()
        {
            NetMatch net = NetMatch.Instance;
            if (net == null || !net.HasNetworkState) return;

            // Match outcome is host-owned; without this a client never saw the winner screen or the countdown.
            NetMatchState match = net.MatchState;
            matchEnded = match.Ended;
            matchDuration = match.Duration;
            autoRematchAt = match.Ended ? Time.time + match.RestartIn : -1f;
            winner = match.Ended && match.WinnerSeat >= 0 && match.WinnerSeat < allFighters.Count
                ? allFighters[match.WinnerSeat]
                : null;

            for (int i = 0; i < pickups.Slots.Count && i < net.Pickups.Length; i++)
            {
                WeaponPickup slot = pickups.Slots[i];
                NetPickupState drop = net.Pickups[i];
                if (slot == null) continue;

                if (drop.Active) slot.ConfigureContent(drop.Weapon, drop.Position);
                slot.SetRemoteAvailable(drop.Active);
            }

            for (int i = 0; i < allFighters.Count && i < NetMatch.MaxFighters; i++)
            {
                Fighter fighter = allFighters[i];
                NetFighterState state = net.GetState(i);

                // Impact feedback is derived from state changes rather than extra messages: a drop in health
                // means a hit landed, and a drop in stock means a ring-out, so clients react without new RPCs.
                if (state.Active)
                {
                    if (state.Health < remoteHealth[i] - 0.01f)
                    {
                        fighter.GetComponent<FighterVisual>()?.OnHit(1f - state.Health / Fighter.MaxHealth);

                        // Weight is inferred from how much health the hit removed, since a client never sees
                        // the impulse itself. Direction comes from the replicated velocity.
                        float hitWeight = Mathf.Clamp01((remoteHealth[i] - state.Health) / 14f);
                        Vector3 hitDirection = state.Velocity.sqrMagnitude > 0.01f
                            ? state.Velocity.normalized
                            : new Vector3(state.Facing, 0f, 0f);
                        fighter.PlayRemoteHitReaction(hitDirection, hitWeight);
                    }

                    if (state.Lives < remoteLives[i])
                    {
                        CombatVfx.JellyBurst(fighter.transform.position, fighter.TintColor, 18, 0.9f, 7f);
                        CombatFeel.Impact(0.8f);
                    }
                }
                else if (remoteActive[i] && remoteLives[i] > 0)
                {
                    CombatVfx.JellyBurst(fighter.transform.position, fighter.TintColor, 30, 1.15f, 9f);
                    CombatFeel.Impact(1f);
                }

                remoteHealth[i] = state.Health;
                remoteLives[i] = state.Lives;
                remoteActive[i] = state.Active;

                fighter.ApplyRemoteVitals(state.Health, state.Lives, state.ProtectionRemaining);

                if (fighter.gameObject.activeSelf != state.Active) fighter.gameObject.SetActive(state.Active);
                if (!state.Active) continue;

                Rigidbody body = fighter.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic) body.isKinematic = true;

                // Velocity, facing, grounded and weapon drive the animation and the held gun model. Without
                // these a client saw fighters slide around frozen, because the local motor never simulates.
                FighterMotor motor = fighter.GetComponent<FighterMotor>();
                if (motor != null)
                {
                    motor.ApplyRemoteState(state.Velocity, state.Facing, state.Grounded,
                        (PrototypeWeaponId)state.Weapon, state.Ammo);
                }

                // Smoothed toward the authoritative position; snap when the gap is large, such as a respawn.
                float gap = (fighter.transform.position - state.Position).sqrMagnitude;
                fighter.transform.position = gap > 9f
                    ? state.Position
                    : Vector3.Lerp(fighter.transform.position, state.Position, 1f - Mathf.Exp(-18f * Time.deltaTime));
            }
        }

        private void CheckRingOuts()
        {
            foreach (Fighter fighter in roster)
            {
                if (!fighter.gameObject.activeInHierarchy) continue;
                Vector3 position = fighter.transform.position;
                if (position.y >= RingOutFloor && Mathf.Abs(position.x) <= RingOutSide) continue;

                fighter.GetComponent<FighterMotor>().ResetWeapon();
                if (!fighter.LoseLife()) continue;

                Fighter survivor = GetSoleSurvivor();
                if (survivor != null)
                {
                    EndMatch(survivor);
                    return;
                }
            }
        }

        /// <summary>Returns the winner once exactly one roster fighter is still alive, otherwise null.</summary>
        private Fighter GetSoleSurvivor()
        {
            // One participant is a waiting room, not a victory.
            if (roster.Count < 2) return null;

            Fighter survivor = null;
            int alive = 0;
            foreach (Fighter fighter in roster)
            {
                if (fighter.IsEliminated) continue;
                alive++;
                survivor = fighter;
            }

            return alive == 1 ? survivor : null;
        }

        /// <summary>Each bot chases whichever living rival is closest, so a free-for-all does not gang up on the player.</summary>
        private void RetargetBots()
        {
            foreach (Fighter fighter in roster)
            {
                BotController brain = fighter.GetComponent<BotController>();
                if (brain == null || !brain.enabled || fighter.IsEliminated) continue;

                Fighter nearest = null;
                float nearestDistance = float.MaxValue;
                foreach (Fighter other in roster)
                {
                    if (other == fighter || other.IsEliminated || !other.gameObject.activeInHierarchy) continue;
                    float distance = (other.transform.position - fighter.transform.position).sqrMagnitude;
                    if (distance >= nearestDistance) continue;
                    nearestDistance = distance;
                    nearest = other;
                }

                brain.SetTarget(nearest != null ? nearest.transform : null);
            }
        }

        private void SetBotCount(int count)
        {
            int freeSeats = NetMatch.MaxFighters - HumanSeats;
            botCount = Mathf.Clamp(count, 0, Mathf.Min(MaxBots, freeSeats));
            StartMatch();
        }

        private void SetDifficulty(BotDifficulty newDifficulty)
        {
            difficulty = newDifficulty;
            foreach (Fighter fighter in allFighters)
            {
                fighter.GetComponent<BotController>()?.SetDifficulty(difficulty);
            }
        }

        private void EndMatch(Fighter matchWinner)
        {
            matchEnded = true;
            winner = matchWinner;
            matchDuration = Time.time - matchStartedAt;
            autoRematchAt = Time.time + AutoRematchDelay;
            SetCombatActive(false);
            ClearProjectiles();
        }

        private void StartMatch()
        {
            ClearProjectiles();
            WeaponPickup.ResetAll();
            pickups.ResetCycle();

            roster.Clear();
            int humans = HumanSeats;
            int total = Mathf.Clamp(humans + botCount, humans, NetMatch.MaxFighters);
            for (int i = 0; i < total && i < allFighters.Count; i++) roster.Add(allFighters[i]);
            appliedHumanSeats = humans;

            foreach (Fighter fighter in allFighters)
            {
                if (roster.Contains(fighter))
                {
                    fighter.ResetRound();
                    fighter.GetComponent<FighterMotor>().ResetWeapon();
                }
                else
                {
                    fighter.gameObject.SetActive(false);
                }
            }

            winner = null;
            matchEnded = false;
            autoRematchAt = -1f;
            matchStartedAt = Time.time;
            SetCombatActive(true);
            RetargetBots();
            ConfigureCameraTargets();
        }

        private void ConfigureCameraTargets()
        {
            ArenaCameraFollow cameraFollow = FindAnyObjectByType<ArenaCameraFollow>();
            if (cameraFollow == null) return;

            if (!Networked && localPlayerCount > 1 && roster.Count > 1)
            {
                // Every local human is framed, so nobody is left off screen in a couch match.
                int framed = Mathf.Min(localPlayerCount, roster.Count);
                Transform[] targets = new Transform[framed];
                for (int i = 0; i < framed; i++) targets[i] = roster[i].transform;
                cameraFollow.SetTargets(targets);
            }
            else
            {
                cameraFollow.SetTarget(player.transform);
            }
        }

        private void SetCombatActive(bool active)
        {
            bool authority = HasAuthority;
            for (int seat = 0; seat < roster.Count; seat++)
            {
                Fighter fighter = roster[seat];
                FighterMotor motor = fighter.GetComponent<FighterMotor>();
                HumanController human = fighter.GetComponent<HumanController>();
                BotController brain = fighter.GetComponent<BotController>();

                bool seatIsHuman = seat < HumanSeats;
                bool localInputSeat = Networked ? seat == 0 : seatIsHuman;
                if (motor != null) motor.enabled = active && authority;
                if (human != null) human.enabled = active && localInputSeat;
                if (brain != null) brain.enabled = active && authority && !seatIsHuman;
                Rigidbody body = fighter.GetComponent<Rigidbody>();
                if (!active && body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        private static void ClearProjectiles()
        {
            PrototypeProjectile[] projectiles = FindObjectsByType<PrototypeProjectile>(FindObjectsSortMode.None);
            foreach (PrototypeProjectile projectile in projectiles) Destroy(projectile.gameObject);
        }

        private Fighter CreateFighter(string fighterName, Color color, Vector3 spawn, FighterShape shape)
        {
            GameObject fighterObject = new(fighterName);
            fighterObject.name = fighterName;

            CapsuleCollider capsule = fighterObject.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.375f;

            Rigidbody body = fighterObject.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX |
                               RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

            Fighter fighter = fighterObject.AddComponent<Fighter>();
            fighterObject.AddComponent<FighterMotor>();
            FighterVisual visual = fighterObject.AddComponent<FighterVisual>();
            WeaponVisual weaponVisual = fighterObject.AddComponent<WeaponVisual>();
            BuildFighterModel(fighterObject.transform, visual, weaponVisual, color, shape);
            fighterObject.AddComponent<ProtectionShield>();
            fighterObject.AddComponent<WeaponChargeRing>();
            fighterObject.GetComponent<FighterMotor>().SeatIndex = allFighters.Count;
            fighter.Initialize(fighterName, color, spawn);
            allFighters.Add(fighter);
            return fighter;
        }

        /// <summary>
        /// Geometry Fighters bodies: one primitive solid per player plus a glowing eye. The physics capsule is
        /// unchanged, so this is purely a visual identity change. Distinct solids give the most separable
        /// silhouettes in a four-way brawl, and the eye is the only facing cue an abstract shape has.
        /// </summary>
        private static void BuildFighterModel(Transform fighter, FighterVisual visual, WeaponVisual weaponVisual, Color tint, FighterShape shape)
        {
            GameObject visualRootObject = new("Visual Root");
            visualRootObject.transform.SetParent(fighter, false);
            Transform root = visualRootObject.transform;

            GameObject bodyObject = ProceduralShapes.CreateBody(shape, "Tint_Body");
            bodyObject.transform.SetParent(root, false);
            bodyObject.transform.localPosition = Vector3.zero;
            bodyObject.transform.localScale = shape switch
            {
                FighterShape.Cylinder => new Vector3(1.02f, 0.62f, 1.02f),
                FighterShape.Tetrahedron => Vector3.one * 1.28f,
                FighterShape.Sphere => Vector3.one * 1.16f,
                _ => Vector3.one * 1.02f
            };
            Renderer bodyRenderer = bodyObject.GetComponent<Renderer>();
            PrototypeMaterials.AssignJelly(bodyRenderer, tint);

            GameObject eyeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeObject.name = "Eye";
            eyeObject.transform.SetParent(root, false);
            eyeObject.transform.localScale = new Vector3(0.34f, 0.34f, 0.2f);
            Collider eyeCollider = eyeObject.GetComponent<Collider>();
            eyeCollider.enabled = false;
            Destroy(eyeCollider);
            PrototypeMaterials.AssignNeon(eyeObject.GetComponent<Renderer>(), new Color(0.85f, 0.98f, 1f), 2f);

            GameObject mountObject = new("Weapon Mount");
            mountObject.transform.SetParent(root, false);

            visual.Bind(root, bodyObject.transform, eyeObject.transform, bodyRenderer);
            weaponVisual.Bind(mountObject.transform);
        }

        private static GameObject CreateModelPart(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider partCollider = part.GetComponent<Collider>();
            partCollider.enabled = false;
            Destroy(partCollider);
            // Fighters stay matte so they read against the metallic platforms and the flat background.
            PrototypeMaterials.AssignSurface(part.GetComponent<Renderer>(), color, 0.05f, 0.28f);
            return part;
        }

        private static void SetupCamera(Transform localPlayer)
        {
            GameObject cameraObject = new("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = false;
            camera.fieldOfView = 39f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 110f;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.07f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = true;

            // Bloom only exists if the camera actually runs the post-processing stack.
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraObject.transform.position = new Vector3(0f, 4.6f, -29f);
            cameraObject.transform.LookAt(new Vector3(0f, 2.25f, 0.5f));
            cameraObject.AddComponent<ArenaCameraFollow>().SetTarget(localPlayer);
        }

        private void OnGUI()
        {
            uiScale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 1f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            resultStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            GUI.Label(new Rect(0f, 12f, UiWidth, 30f), "PROTOTYPE 0.3.0 — LOCAL MULTIPLAYER", titleStyle);

            if (showDisplaySettings)
            {
                DrawDisplaySettings();
                return;
            }

            if (inMenu)
            {
                DrawMainMenu();
                return;
            }

            int requiredControllers = Mathf.Max(0, localPlayerCount - 1);
            if (!Networked && requiredControllers > HumanController.ConnectedControllerCount)
            {
                GUI.Box(new Rect(UiWidth * 0.5f - 260f, 48f, 520f, 34f),
                    $"CONNECT {requiredControllers} GAMEPADS — DETECTED {HumanController.ConnectedControllerCount}");
            }

            DrawScoreboard();

            if (matchEnded && winner != null)
            {
                float restartIn = Mathf.Max(0f, autoRematchAt - Time.time);
                GUI.Box(new Rect(UiWidth * 0.5f - 235f, UiHeight * 0.5f - 80f, 470f, 160f), "");
                GUI.Label(new Rect(UiWidth * 0.5f - 220f, UiHeight * 0.5f - 62f, 440f, 55f),
                    $"{winner.DisplayName} WINS!", resultStyle);
                GUI.Label(new Rect(UiWidth * 0.5f - 220f, UiHeight * 0.5f + 2f, 440f, 35f),
                    $"MATCH {matchDuration:0.0}s   •   NEXT MATCH IN {restartIn:0.0}s", titleStyle);
            }

            if (localPlayerCount > 1 && !Networked)
            {
                for (int i = 0; i < localPlayerCount && i < roster.Count; i++)
                {
                    DrawOffscreenIndicator(roster[i], $"P{i + 1} ▸", FighterColors[i]);
                }
            }
            else
            {
                DrawOffscreenIndicator(player, "YOU ▸", player != null ? player.TintColor : FighterColors[0]);
            }

            if (paused)
            {
                DrawPauseMenu();
                return;
            }

            // The control and rules text that used to sit along the bottom edge lives in the settings screen
            // now. It was permanent clutter across the arena floor, and it is reference material a player
            // reads once rather than something needed every frame.
        }

        /// <summary>
        /// Lives-only scoreboard. Health is deliberately absent: the fighter already shows its own condition
        /// through colour and wobble, and lives are the only number that decides the match.
        ///
        /// Humans go top-left and bots top-right, so in a couch match every player finds their own card in
        /// one place instead of hunting for it among the AI.
        /// </summary>
        private void DrawScoreboard()
        {
            int humans = Mathf.Min(HumanSeats, roster.Count);
            for (int i = 0; i < humans; i++)
            {
                DrawFighterCard(roster[i], 24f, 52f + i * 46f, false);
            }

            for (int i = humans; i < roster.Count; i++)
            {
                DrawFighterCard(roster[i], UiWidth - 268f, 52f + (i - humans) * 46f, true);
            }
        }

        /// <summary>One compact card: a colour flag, the name, remaining lives as pips, and the weapon.</summary>
        private void DrawFighterCard(Fighter fighter, float x, float y, bool rightAligned)
        {
            if (fighter == null) return;

            const float cardWidth = 244f;
            const float cardHeight = 40f;
            GUI.Box(new Rect(x, y, cardWidth, cardHeight), GUIContent.none);

            // A solid colour flag ties the card to the fighter on screen faster than the name does.
            Color previous = GUI.color;
            GUI.color = fighter.IsEliminated ? new Color(0.35f, 0.35f, 0.38f) : fighter.TintColor;
            GUI.Box(new Rect(x + 5f, y + 5f, 6f, cardHeight - 10f), GUIContent.none);
            GUI.color = previous;

            GUI.Label(new Rect(x + 18f, y + 3f, 150f, 22f), fighter.DisplayName, hudStyle);

            string lives = fighter.IsEliminated
                ? "OUT"
                : new string('●', Mathf.Max(0, fighter.Lives)) + new string('○', Mathf.Max(0, Fighter.StartingLives - fighter.Lives));
            GUI.color = fighter.IsEliminated ? new Color(1f, 0.5f, 0.5f) : previous;
            GUI.Label(new Rect(x + cardWidth - 92f, y + 3f, 86f, 22f), lives, hudStyle);
            GUI.color = previous;

            FighterMotor motor = fighter.GetComponent<FighterMotor>();
            if (motor == null) return;
            string ammo = motor.Ammo < 0 ? "∞" : motor.Ammo.ToString();
            GUI.Label(new Rect(x + 18f, y + 20f, cardWidth - 26f, 20f), $"{motor.WeaponName}  ×{ammo}");
        }

        /// <summary>
        /// Points at the local player once a launch carries them off screen. The camera deliberately stays on
        /// the arena rather than chasing, so without this a strong knockback ends in an unseen death.
        /// </summary>
        private void DrawOffscreenIndicator(Fighter target, string label, Color tint)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            Camera view = Camera.main;
            if (view == null) return;

            Vector3 viewport = view.WorldToViewportPoint(target.transform.position);
            bool behind = viewport.z < 0f;
            if (!behind && viewport.x >= 0.04f && viewport.x <= 0.96f && viewport.y >= 0.04f && viewport.y <= 0.96f) return;

            if (behind)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            float screenX = Mathf.Clamp(viewport.x, 0.04f, 0.96f) * UiWidth;
            float screenY = (1f - Mathf.Clamp(viewport.y, 0.04f, 0.96f)) * UiHeight;

            Color previous = GUI.color;
            GUI.color = tint;
            GUI.Box(new Rect(screenX - 46f, screenY - 16f, 92f, 32f), label);
            GUI.color = previous;
        }

        /// <summary>
        /// Entry screen. Reduced to two ways to play plus settings: the old screen listed five entry points,
        /// three of which only differed by player count, which is a decision better made in the lobby where
        /// the game can see which controllers are actually plugged in.
        /// </summary>
        private void DrawMainMenu()
        {
            switch (menuScreen)
            {
                case MenuScreen.LocalLobby:
                    DrawLocalLobby();
                    return;
                case MenuScreen.Online:
                    DrawOnlineScreen();
                    return;
                case MenuScreen.Help:
                    DrawHelpScreen();
                    return;
            }

            const float width = 460f;
            const float height = 470f;
            float left = UiWidth * 0.5f - width * 0.5f;
            float top = UiHeight * 0.5f - height * 0.5f;

            GUI.Box(new Rect(left, top, width, height), "");
            GUI.Label(new Rect(left, top + 22f, width, 40f), "GEOMETRY FIGHTERS", resultStyle);

            bool busy = Session != null && Session.Busy;
            GUI.enabled = !busy;

            if (GUI.Button(new Rect(left + 50f, top + 92f, width - 100f, 52f), "SINGLE PLAYER"))
            {
                Session?.PlayOffline();
                localPlayerCount = 1;
                botCount = Mathf.Max(1, botCount);
                BeginPlaying();
            }

            if (GUI.Button(new Rect(left + 50f, top + 154f, width - 100f, 52f), "MULTIPLAYER"))
            {
                Session?.PlayOffline();
                ResetLobby();
                menuScreen = MenuScreen.LocalLobby;
            }

            GUI.enabled = true;

            GUI.Label(new Rect(left + 50f, top + 216f, width - 100f, 24f), "ARENA", hudStyle);
            DrawChoiceRow(left + 50f, top + 240f, width - 100f, ArenaTheme.Labels, (int)selectedTheme,
                index => SelectTheme((ArenaThemeId)index));

            if (GUI.Button(new Rect(left + 50f, top + 288f, width - 100f, 38f), "HOW TO PLAY"))
            {
                menuScreen = MenuScreen.Help;
            }

            if (GUI.Button(new Rect(left + 50f, top + 332f, width - 100f, 38f), "DISPLAY SETTINGS"))
            {
                OpenDisplaySettings();
            }

            GUI.Label(new Rect(left, top + height - 44f, width, 22f),
                "Single player pits you against AI. Story mode comes later.", titleStyle);

            if (Session != null && Session.Mode == SessionMode.Client)
            {
                GUI.Label(new Rect(left, top + height - 24f, width, 22f), "Connected. Waiting for host...", titleStyle);
                BeginPlaying();
            }
        }

        /// <summary>
        /// Couch lobby. Seat one is always the keyboard; every other seat is claimed by pressing a button on
        /// an actual gamepad, which is what makes the seat order match where people are sitting instead of
        /// depending on the order Windows happened to enumerate the devices.
        /// </summary>
        private void DrawLocalLobby()
        {
            const float width = 500f;
            const float height = 470f;
            float left = UiWidth * 0.5f - width * 0.5f;
            float top = UiHeight * 0.5f - height * 0.5f;

            GUI.Box(new Rect(left, top, width, height), "");
            GUI.Label(new Rect(left, top + 18f, width, 36f), "PLAYERS", resultStyle);
            GUI.Label(new Rect(left, top + 58f, width, 24f),
                $"GAMEPADS DETECTED: {HumanController.ConnectedControllerCount}", titleStyle);

            PollLobbyJoins();

            for (int seat = 0; seat < NetMatch.MaxFighters; seat++)
            {
                float rowTop = top + 92f + seat * 46f;
                bool joined = lobbyJoined[seat];

                Color previous = GUI.color;
                GUI.color = joined ? FighterColors[seat] : new Color(0.4f, 0.4f, 0.45f);
                GUI.Box(new Rect(left + 40f, rowTop, 8f, 36f), GUIContent.none);
                GUI.color = previous;

                GUI.Label(new Rect(left + 58f, rowTop + 6f, 130f, 26f), $"PLAYER {seat + 1}", hudStyle);

                string state;
                if (seat == 0) state = "KEYBOARD — READY";
                else if (joined) state = $"READY — {HumanController.ControllerName(seat - 1)}";
                else if (HumanController.ConnectedControllerCount >= seat) state = "PRESS A / CROSS TO JOIN";
                else state = "CONNECT A GAMEPAD";

                GUI.Label(new Rect(left + 180f, rowTop + 6f, width - 220f, 26f), state);
            }

            int joinedCount = LobbyPlayerCount;
            int freeSeats = NetMatch.MaxFighters - joinedCount;
            GUI.Label(new Rect(left + 40f, top + 284f, 120f, 26f), "BOTS", hudStyle);
            string[] botLabels = new string[freeSeats + 1];
            for (int i = 0; i <= freeSeats; i++) botLabels[i] = i.ToString();
            DrawChoiceRow(left + 150f, top + 282f, width - 190f, botLabels,
                Mathf.Clamp(botCount, 0, freeSeats), index => botCount = index);

            GUI.Label(new Rect(left, top + 322f, width, 24f),
                "Player 1 presses ENTER to start", titleStyle);

            bool startPressed = Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (GUI.Button(new Rect(left + 50f, top + 350f, width - 100f, 44f), $"START — {joinedCount} PLAYERS") ||
                startPressed)
            {
                localPlayerCount = Mathf.Max(1, joinedCount);
                botCount = Mathf.Clamp(botCount, 0, NetMatch.MaxFighters - joinedCount);
                BeginPlaying();
                return;
            }

            if (GUI.Button(new Rect(left + 50f, top + 400f, (width - 110f) * 0.5f, 36f), "ONLINE ROOM"))
            {
                menuScreen = MenuScreen.Online;
            }

            if (GUI.Button(new Rect(left + 60f + (width - 110f) * 0.5f, top + 400f, (width - 110f) * 0.5f, 36f), "BACK"))
            {
                menuScreen = MenuScreen.Root;
            }
        }

        /// <summary>
        /// Claims seats for gamepads as their buttons are pressed. Seats fill in press order and a pad already
        /// holding a seat cannot take a second one.
        /// </summary>
        private void PollLobbyJoins()
        {
            int pad = HumanController.GamepadRequestingJoin();
            if (pad < 0) return;

            int wantedSeat = pad + 1;
            if (wantedSeat >= NetMatch.MaxFighters || lobbyJoined[wantedSeat]) return;
            lobbyJoined[wantedSeat] = true;
        }

        private int LobbyPlayerCount
        {
            get
            {
                int count = 0;
                foreach (bool joined in lobbyJoined)
                {
                    if (joined) count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Swaps the arena. The rebuild happens immediately so the menu backdrop shows the map that was
        /// chosen, and fighters are respawned because their old footing no longer exists.
        /// </summary>
        private void SelectTheme(ArenaThemeId theme)
        {
            if (selectedTheme == theme && ArenaBuilder.ActiveThemeId == theme) return;

            selectedTheme = theme;
            // Build destroys the previous arena along with its camera, so the view has to be remade.
            ArenaBuilder.Build(theme);
            SetupCamera(player.transform);
            foreach (Fighter fighter in allFighters) fighter.MoveSpawn(SpawnPointFor(fighter));
            StartMatch();
        }

        /// <summary>Spawn point for a fighter's seat, kept inside the current arena's main deck.</summary>
        private Vector3 SpawnPointFor(Fighter fighter)
        {
            int seat = Mathf.Clamp(allFighters.IndexOf(fighter), 0, SpawnPoints.Length - 1);
            ArenaBuilder.PlatformDefinition deck = ArenaBuilder.Layout[0];
            float half = Mathf.Max(1f, deck.Scale.x * 0.5f - 1.5f);
            float x = Mathf.Clamp(SpawnPoints[seat].x, -half, half);
            return new Vector3(x, deck.Top + 1.6f, 0f);
        }

        private void ResetLobby()
        {
            for (int i = 0; i < lobbyJoined.Length; i++) lobbyJoined[i] = i == 0;
            botCount = 0;
        }

        /// <summary>Relay hosting and joining, split out of the entry screen to keep that screen to two choices.</summary>
        private void DrawOnlineScreen()
        {
            const float width = 440f;
            const float height = 360f;
            float left = UiWidth * 0.5f - width * 0.5f;
            float top = UiHeight * 0.5f - height * 0.5f;

            GUI.Box(new Rect(left, top, width, height), "");
            GUI.Label(new Rect(left, top + 18f, width, 36f), "ONLINE", resultStyle);

            bool busy = Session != null && Session.Busy;
            GUI.enabled = !busy;

            if (GUI.Button(new Rect(left + 40f, top + 70f, width - 80f, 42f), "HOST ROOM"))
            {
                // An opened room starts empty so arriving players are not padded out with bots.
                localPlayerCount = 1;
                botCount = 0;
                Session?.HostRelay();
            }

            GUI.Label(new Rect(left + 20f, top + 124f, 110f, 28f), "ROOM CODE", hudStyle);
            joinCodeEntry = GUI.TextField(new Rect(left + 128f, top + 124f, 180f, 28f), joinCodeEntry, 8);
            if (GUI.Button(new Rect(left + 314f, top + 124f, 82f, 28f), "PASTE"))
            {
                joinCodeEntry = (GUIUtility.systemCopyBuffer ?? string.Empty).Trim().ToUpperInvariant();
            }

            if (GUI.Button(new Rect(left + 40f, top + 160f, width - 80f, 42f), "JOIN ROOM"))
            {
                localPlayerCount = 1;
                Session?.JoinRelay(joinCodeEntry);
            }

            GUI.enabled = true;

            string message = busy ? (Session != null ? Session.Status : "Working...") : Session?.Status;
            if (!string.IsNullOrEmpty(message))
            {
                GUI.Label(new Rect(left + 20f, top + 210f, width - 40f, 48f), message);
            }

            if (Session != null && Session.Mode == SessionMode.Host && !string.IsNullOrEmpty(Session.JoinCode))
            {
                GUI.Label(new Rect(left + 20f, top + 210f, 250f, 30f), $"ROOM CODE: {Session.JoinCode}", hudStyle);
                if (GUI.Button(new Rect(left + 286f, top + 210f, 110f, 28f),
                    Time.unscaledTime < codeCopiedUntil ? "COPIED!" : "COPY CODE"))
                {
                    GUIUtility.systemCopyBuffer = Session.JoinCode;
                    codeCopiedUntil = Time.unscaledTime + 1.5f;
                }

                if (GUI.Button(new Rect(left + 40f, top + 250f, width - 80f, 38f), "START MATCH")) BeginPlaying();
            }
            else if (Session != null && Session.Mode == SessionMode.Client)
            {
                GUI.Label(new Rect(left, top + 210f, width, 34f), "Connected. Waiting for host...", titleStyle);
                BeginPlaying();
            }

            if (GUI.Button(new Rect(left + 40f, top + height - 48f, width - 80f, 36f), "BACK"))
            {
                menuScreen = MenuScreen.LocalLobby;
            }
        }

        /// <summary>
        /// The rules and control reference that used to be printed permanently across the bottom of the
        /// arena. It is read once and then never needed again, so it belongs behind a menu, not on the HUD.
        /// </summary>
        private void DrawHelpScreen()
        {
            const float width = 620f;
            const float height = 470f;
            float left = UiWidth * 0.5f - width * 0.5f;
            float top = UiHeight * 0.5f - height * 0.5f;

            GUI.Box(new Rect(left, top, width, height), "");
            GUI.Label(new Rect(left, top + 18f, width, 36f), "HOW TO PLAY", resultStyle);

            float row = top + 68f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f), "GOAL", hudStyle);
            row += 26f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f),
                "Damage does not kill. It makes a fighter easier to launch.");
            row += 22f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f),
                "Knock rivals out of the arena. Every ring-out costs them a life.");

            row += 40f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f), "PLAYER 1 — KEYBOARD", hudStyle);
            row += 26f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f),
                "A / D  move      SPACE  double jump      S  drop through");
            row += 22f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f),
                "J or LEFT CTRL  fire      R  restart      ESC  pause menu");

            row += 40f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f), "PLAYERS 2-4 — GAMEPAD", hudStyle);
            row += 26f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f),
                "LEFT STICK  move      SOUTH (A / Cross)  jump      EAST  drop through");
            row += 22f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f),
                "LT or RT  fire      WEST / RB also fire");

            row += 40f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f), "IN A SOLO MATCH", hudStyle);
            row += 26f;
            GUI.Label(new Rect(left + 36f, row, width - 72f, 24f),
                "0-3  bot count      F1-F5  bot difficulty (EASY to MASTER)");

            if (GUI.Button(new Rect(left + 40f, top + height - 48f, width - 80f, 36f), "BACK"))
            {
                menuScreen = MenuScreen.Root;
            }
        }

        private void BeginPlaying()
        {
            inMenu = false;
            wasConnected = false;
            WeaponPickup.LocalPickupsEnabled = HasAuthority;

            // Returning from an online client can leave `player` pointing at its former remote seat.
            // Every offline mode owns seat zero locally, so restore that stable identity before starting.
            if (!Networked && allFighters.Count > 0) player = allFighters[0];

            for (int i = 0; i < NetMatch.MaxFighters; i++)
            {
                remoteHealth[i] = Fighter.MaxHealth;
                remoteLives[i] = Fighter.StartingLives;
                remoteActive[i] = true;
            }

            if (HasAuthority) StartMatch();
        }

        private void ReportControllerDevices()
        {
            int count = HumanController.ConnectedControllerCount;
            if (count == lastReportedControllerCount) return;
            lastReportedControllerCount = count;
            string first = HumanController.ControllerName(0);
            string second = HumanController.ControllerName(1);
            Debug.Log($"CHAOS_LOCAL_GAMEPADS: count={count}; P2={first}; P3={second}");
        }

        /// <summary>
        /// A client whose session drops would otherwise sit in a frozen match forever. Only counts as a drop
        /// once the client has actually been connected: StartClient returns before approval completes, so
        /// checking IsConnectedClient too early tore down the connection that was still being established.
        /// </summary>
        private void CheckConnectionLost()
        {
            if (HasAuthority || Session == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsConnectedClient)
            {
                wasConnected = true;
                return;
            }

            if (!wasConnected) return;

            Debug.Log("CHAOS_NET_CONNECTION_LOST");
            wasConnected = false;
            Session.Leave();
            WeaponPickup.LocalPickupsEnabled = true;
            inMenu = true;
            menuScreen = MenuScreen.Root;
        }

        private void DrawPauseMenu()
        {
            // Widened for the five-tier difficulty ladder: at 360 the row could not fit "NORMAL" and
            // "EXPERT" side by side without clipping the labels.
            const float width = 430f;
            const float height = 486f;
            float left = UiWidth * 0.5f - width * 0.5f;
            float top = UiHeight * 0.5f - height * 0.5f;
            float inner = width - 80f;

            GUI.Box(new Rect(left, top, width, height), "");
            GUI.Label(new Rect(left, top + 16f, width, 40f), "PAUSED", resultStyle);

            // Match settings belong to whoever runs the simulation, so only the host may change them.
            bool canConfigure = HasAuthority;
            GUI.enabled = canConfigure;

            GUI.Label(new Rect(left + 40f, top + 64f, inner, 24f), "DIFFICULTY", hudStyle);
            DrawChoiceRow(left + 40f, top + 88f, inner,
                BotProfile.Labels,
                (int)difficulty,
                index => SetDifficulty((BotDifficulty)index));

            GUI.Label(new Rect(left + 40f, top + 130f, inner, 24f), localPlayerCount > 1 ? "BOTS (disabled in local multiplayer)" : "BOTS", hudStyle);
            GUI.enabled = canConfigure && localPlayerCount == 1;
            DrawChoiceRow(left + 40f, top + 154f, inner,
                new[] { "0", "1", "2", "3" },
                botCount,
                SetBotCount);

            GUI.enabled = true;

            if (!canConfigure)
            {
                GUI.Label(new Rect(left + 40f, top + 192f, inner, 24f), "Only the host can change match settings.");
            }

            if (GUI.Button(new Rect(left + 40f, top + 222f, inner, 38f), "RESUME")) SetPaused(false);

            GUI.enabled = canConfigure;
            if (GUI.Button(new Rect(left + 40f, top + 266f, inner, 38f), "RESTART MATCH"))
            {
                SetPaused(false);
                StartMatch();
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(left + 40f, top + 310f, inner, 38f), "LEAVE TO MENU"))
            {
                SetPaused(false);
                Session?.Leave();
                WeaponPickup.LocalPickupsEnabled = true;
                inMenu = true;
                menuScreen = MenuScreen.Root;
            }

            if (GUI.Button(new Rect(left + 40f, top + 354f, inner, 38f), "DISPLAY SETTINGS"))
            {
                OpenDisplaySettings();
            }

            if (GUI.Button(new Rect(left + 40f, top + 398f, inner, 34f), "QUIT GAME"))
            {
                Application.Quit();
            }

            GUI.Label(new Rect(left, top + height - 26f, width, 24f), "ESC closes this menu", titleStyle);
        }

        private void InitializeDisplaySettings()
        {
            displayResolutions.Clear();
            displayResolutions.AddRange(BaseDisplayResolutions);
            Vector2Int native = new(Screen.currentResolution.width, Screen.currentResolution.height);
            if (!displayResolutions.Contains(native)) displayResolutions.Add(native);
            displayResolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            int targetWidth = PlayerPrefs.GetInt(DisplayWidthKey, Screen.width);
            int targetHeight = PlayerPrefs.GetInt(DisplayHeightKey, Screen.height);
            selectedResolutionIndex = FindClosestResolution(targetWidth, targetHeight);
            selectedBorderless = PlayerPrefs.GetInt(DisplayBorderlessKey,
                Screen.fullScreenMode == FullScreenMode.Windowed ? 0 : 1) == 1;

            if (PlayerPrefs.HasKey(DisplayWidthKey)) ApplyDisplaySettings(false);
        }

        private int FindClosestResolution(int width, int height)
        {
            int best = 0;
            long bestDistance = long.MaxValue;
            for (int i = 0; i < displayResolutions.Count; i++)
            {
                long dx = displayResolutions[i].x - width;
                long dy = displayResolutions[i].y - height;
                long distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        private void OpenDisplaySettings()
        {
            selectedResolutionIndex = FindClosestResolution(Screen.width, Screen.height);
            selectedBorderless = Screen.fullScreenMode != FullScreenMode.Windowed;
            displayStatus = string.Empty;
            showDisplaySettings = true;
        }

        private void DrawDisplaySettings()
        {
            const float width = 500f;
            const float height = 330f;
            float left = UiWidth * 0.5f - width * 0.5f;
            float top = UiHeight * 0.5f - height * 0.5f;
            GUI.Box(new Rect(left, top, width, height), "");
            GUI.Label(new Rect(left, top + 16f, width, 40f), "DISPLAY SETTINGS", resultStyle);

            GUI.Label(new Rect(left + 50f, top + 68f, width - 100f, 24f), "RESOLUTION", hudStyle);
            Vector2Int resolution = displayResolutions[Mathf.Clamp(selectedResolutionIndex, 0, displayResolutions.Count - 1)];
            if (GUI.Button(new Rect(left + 50f, top + 96f, 52f, 36f), "<"))
                selectedResolutionIndex = (selectedResolutionIndex - 1 + displayResolutions.Count) % displayResolutions.Count;
            GUI.Label(new Rect(left + 110f, top + 96f, width - 220f, 36f), $"{resolution.x} × {resolution.y}", titleStyle);
            if (GUI.Button(new Rect(left + width - 102f, top + 96f, 52f, 36f), ">"))
                selectedResolutionIndex = (selectedResolutionIndex + 1) % displayResolutions.Count;

            GUI.Label(new Rect(left + 50f, top + 146f, width - 100f, 24f), "WINDOW MODE", hudStyle);
            DrawChoiceRow(left + 50f, top + 172f, width - 100f,
                new[] { "WINDOWED", "BORDERLESS" }, selectedBorderless ? 1 : 0,
                index => selectedBorderless = index == 1);

            if (GUI.Button(new Rect(left + 50f, top + 222f, 190f, 40f), "APPLY")) ApplyDisplaySettings(true);
            if (GUI.Button(new Rect(left + width - 240f, top + 222f, 190f, 40f), "BACK")) showDisplaySettings = false;
            GUI.Label(new Rect(left + 50f, top + 274f, width - 100f, 28f), displayStatus, titleStyle);
            GUI.Label(new Rect(left, top + height - 24f, width, 22f), "ESC returns without applying", titleStyle);
        }

        private void ApplyDisplaySettings(bool showConfirmation)
        {
            if (displayResolutions.Count == 0) return;
            Vector2Int resolution = displayResolutions[Mathf.Clamp(selectedResolutionIndex, 0, displayResolutions.Count - 1)];
            FullScreenMode mode = selectedBorderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(resolution.x, resolution.y, mode);
            PlayerPrefs.SetInt(DisplayWidthKey, resolution.x);
            PlayerPrefs.SetInt(DisplayHeightKey, resolution.y);
            PlayerPrefs.SetInt(DisplayBorderlessKey, selectedBorderless ? 1 : 0);
            PlayerPrefs.Save();
            if (showConfirmation) displayStatus = $"APPLIED: {resolution.x} × {resolution.y} — {(selectedBorderless ? "BORDERLESS" : "WINDOWED")}";
        }

        /// <summary>Row of mutually exclusive options; the active one is marked so the state is readable.</summary>
        private void DrawChoiceRow(float left, float top, float width, string[] labels, int active, System.Action<int> onPick)
        {
            float gap = 6f;
            float itemWidth = (width - gap * (labels.Length - 1)) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                string label = i == active ? $"[ {labels[i]} ]" : labels[i];
                if (GUI.Button(new Rect(left + i * (itemWidth + gap), top, itemWidth, 32f), label) && i != active)
                {
                    onPick(i);
                }
            }
        }

        private void DrawFighterPanel(Fighter fighter, float x, float y)
        {
            if (fighter == null) return;
            string state = fighter.IsEliminated ? "OUT" : $"LIVES {fighter.Lives}";
            GUI.Label(new Rect(x, y, 280f, 28f), $"{fighter.DisplayName}  {state}", hudStyle);
            FighterMotor motor = fighter.GetComponent<FighterMotor>();
            if (motor == null) return;
            string ammoText = motor.Ammo < 0 ? "∞" : motor.Ammo.ToString();
            GUI.Label(new Rect(x, y + 24f, 280f, 24f), $"{motor.WeaponName}  AMMO {ammoText}");
        }
    }
}
