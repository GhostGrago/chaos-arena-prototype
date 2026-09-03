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
        private bool paused;
        private bool inMenu = true;
        private string joinCodeEntry = string.Empty;
        private float codeCopiedUntil;

        private static NetworkSession Session => NetworkSession.Instance;
        private bool Networked => Session != null && Session.Mode != SessionMode.Offline;
        /// <summary>Only the host (or an offline game) decides outcomes; clients just display what they receive.</summary>
        private bool HasAuthority => Session == null || Session.Mode != SessionMode.Client;
        private BotDifficulty difficulty = BotDifficulty.Easy;

        // Playtest builds restart on their own so a session never parks on a result screen waiting for input.
        // Tightened in 0.1.6+ledge-grab: the old 16-unit margin let fighters die far off screen with no
        // chance to react. With a recovery move available, a closer boundary is both fairer and visible.
        private const float RingOutSide = 13f;
        private const float RingOutFloor = -6f;

        private const float AutoRematchDelay = 2.5f;
        private float autoRematchAt = -1f;

        private void Awake()
        {
            ArenaBuilder.Build();
            CombatFeel.Ensure();

            player = CreateFighter("PLAYER", FighterColors[0], SpawnPoints[0], FighterShapes[0]);
            player.gameObject.AddComponent<HumanController>();

            for (int i = 0; i < MaxBots; i++)
            {
                Fighter botFighter = CreateFighter($"BOT {i + 1}", FighterColors[i + 1], SpawnPoints[i + 1], FighterShapes[i + 1]);
                botFighter.gameObject.AddComponent<BotController>().SetDifficulty(difficulty);
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
            if (WeaponPickup.All.Count != 3) throw new System.InvalidOperationException("Expected three weapon pickups.");
            if (FindAnyObjectByType<ArenaCameraFollow>() == null) throw new System.InvalidOperationException("Missing local-player camera follow.");
            if (ArenaBuilder.Layout.Count(definition => definition.OneWay) < 5)
            {
                throw new System.InvalidOperationException("Expected at least five one-way platforms.");
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
            AssertProtectionWeakensRatherThanBlocks();
            AssertPauseRestoresTime();
            AssertBotCountRoster();
            AssertFreeForAllElimination();
            Debug.Log("CHAOS_ARENA_0111_ASSERTIONS_PASS: pause menu, weakened protection, knockback stun, translucent jelly bodies, weapon mounts, pickups, platforms, bot roster, elimination, winner, and rematch reset verified.");
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
            }

            if (Networked && NetMatch.Instance != null)
            {
                NetMatch.Instance.HostBroadcast(roster, BuildMatchState());
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

        // Last seen remote vitals, used to spot hits and ring-outs on a client purely from state changes.
        private readonly float[] remoteHealth = new float[NetMatch.MaxFighters];
        private readonly int[] remoteLives = new int[NetMatch.MaxFighters];
        private readonly bool[] remoteActive = new bool[NetMatch.MaxFighters];

        /// <summary>Seats currently held by people. Offline that is just the local player.</summary>
        private int HumanSeats => Networked && NetMatch.Instance != null
            ? Mathf.Clamp(NetMatch.Instance.HumanSeats, 1, NetMatch.MaxFighters)
            : 1;

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
                if (brain != null)
                {
                    bool inRoster = roster.Contains(fighter);
                    brain.enabled = authority && inRoster && !isHuman && !matchEnded;
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
        }

        private string BuildSeatName(int seat, int humans)
        {
            if (seat >= humans) return $"BOT {seat - humans + 1}";
            return Networked ? $"PLAYER {seat + 1}" : "PLAYER";
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

        private NetMatchState BuildMatchState()
        {
            byte mask = 0;
            for (int i = 0; i < WeaponPickup.All.Count && i < 8; i++)
            {
                if (WeaponPickup.All[i] != null && WeaponPickup.All[i].IsAvailable) mask |= (byte)(1 << i);
            }

            return new NetMatchState
            {
                Ended = matchEnded,
                WinnerSeat = (sbyte)(winner != null ? allFighters.IndexOf(winner) : -1),
                Duration = matchDuration,
                RestartIn = Mathf.Max(0f, autoRematchAt - Time.time),
                PickupMask = mask
            };
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

            for (int i = 0; i < WeaponPickup.All.Count && i < 8; i++)
            {
                WeaponPickup pickup = WeaponPickup.All[i];
                if (pickup != null) pickup.SetRemoteAvailable((match.PickupMask & (1 << i)) != 0);
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
                        CombatVfx.JellyBurst(fighter.transform.position, fighter.TintColor, 4, 0.45f, 4f);
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
            botCount = Mathf.Clamp(count, 0, MaxBots);
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

                // A seat taken by a remote player is driven by their input, never by a local brain.
                bool seatIsRemoteHuman = Networked && NetMatch.Instance != null && seat > 0 && seat < NetMatch.Instance.HumanSeats;
                if (motor != null) motor.enabled = active && authority;
                if (human != null) human.enabled = active;
                if (brain != null) brain.enabled = active && authority && !seatIsRemoteHuman;
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
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            resultStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            GUI.Label(new Rect(0f, 12f, Screen.width, 30f), "PROTOTYPE 0.2.3 — EMPTY ROOMS & HOST SETTINGS", titleStyle);

            if (inMenu)
            {
                DrawMainMenu();
                return;
            }

            DrawFighterPanel(player, 24f, 52f);
            for (int i = 1; i < roster.Count; i++)
            {
                DrawFighterPanel(roster[i], Screen.width - 300f, 52f + (i - 1) * 52f);
            }

            if (matchEnded && winner != null)
            {
                float restartIn = Mathf.Max(0f, autoRematchAt - Time.time);
                GUI.Box(new Rect(Screen.width * 0.5f - 235f, Screen.height * 0.5f - 80f, 470f, 160f), "");
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 62f, 440f, 55f),
                    $"{winner.DisplayName} WINS!", resultStyle);
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f + 2f, 440f, 35f),
                    $"MATCH {matchDuration:0.0}s   •   NEXT MATCH IN {restartIn:0.0}s", titleStyle);
            }

            DrawOffscreenIndicator();

            if (paused)
            {
                DrawPauseMenu();
                return;
            }

            GUI.Label(new Rect(24f, Screen.height - 84f, 900f, 26f), "More hits make fighters easier to launch — ring-outs cost lives.");
            GUI.Label(new Rect(24f, Screen.height - 60f, 1100f, 26f), "A/D move  •  Space double-jump  •  S drop through  •  J fire  •  R restart now");
            GUI.Label(new Rect(24f, Screen.height - 36f, 1100f, 26f),
                $"0/1/2/3 bot count (now {botCount})  •  F1/F2/F3 difficulty (now {difficulty.ToString().ToUpperInvariant()})  •  ESC menu");
        }

        /// <summary>
        /// Points at the local player once a launch carries them off screen. The camera deliberately stays on
        /// the arena rather than chasing, so without this a strong knockback ends in an unseen death.
        /// </summary>
        private void DrawOffscreenIndicator()
        {
            if (player == null || !player.gameObject.activeInHierarchy) return;
            Camera view = Camera.main;
            if (view == null) return;

            Vector3 viewport = view.WorldToViewportPoint(player.transform.position);
            bool behind = viewport.z < 0f;
            if (!behind && viewport.x >= 0.04f && viewport.x <= 0.96f && viewport.y >= 0.04f && viewport.y <= 0.96f) return;

            if (behind)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            float screenX = Mathf.Clamp(viewport.x, 0.04f, 0.96f) * Screen.width;
            float screenY = (1f - Mathf.Clamp(viewport.y, 0.04f, 0.96f)) * Screen.height;

            Color previous = GUI.color;
            GUI.color = FighterColors[0];
            GUI.Box(new Rect(screenX - 46f, screenY - 16f, 92f, 32f), "YOU ▸");
            GUI.color = previous;
        }

        /// <summary>Entry screen: play alone, host a relay room, or join one with a code.</summary>
        private void DrawMainMenu()
        {
            const float width = 420f;
            const float height = 340f;
            float left = Screen.width * 0.5f - width * 0.5f;
            float top = Screen.height * 0.5f - height * 0.5f;

            GUI.Box(new Rect(left, top, width, height), "");
            GUI.Label(new Rect(left, top + 16f, width, 40f), "GEOMETRY FIGHTERS", resultStyle);

            bool busy = Session != null && Session.Busy;
            GUI.enabled = !busy;

            if (GUI.Button(new Rect(left + 40f, top + 74f, width - 80f, 40f), "PLAY SOLO (vs BOTS)"))
            {
                Session?.PlayOffline();
                botCount = Mathf.Max(1, botCount);
                BeginPlaying();
            }

            if (GUI.Button(new Rect(left + 40f, top + 122f, width - 80f, 40f), "HOST ONLINE ROOM"))
            {
                // An opened room starts empty so arriving players are not padded out with bots.
                botCount = 0;
                Session?.HostRelay();
            }

            GUI.Label(new Rect(left + 20f, top + 174f, 110f, 28f), "ROOM CODE", hudStyle);
            joinCodeEntry = GUI.TextField(new Rect(left + 128f, top + 174f, 170f, 28f), joinCodeEntry, 8);
            if (GUI.Button(new Rect(left + 304f, top + 174f, 78f, 28f), "PASTE"))
            {
                joinCodeEntry = (GUIUtility.systemCopyBuffer ?? string.Empty).Trim().ToUpperInvariant();
            }

            if (GUI.Button(new Rect(left + 40f, top + 210f, width - 80f, 40f), "JOIN ROOM"))
            {
                Session?.JoinRelay(joinCodeEntry);
            }

            GUI.enabled = true;

            string message = busy ? (Session != null ? Session.Status : "Working...") : Session?.Status;
            if (!string.IsNullOrEmpty(message))
            {
                GUI.Label(new Rect(left + 20f, top + 258f, width - 40f, 48f), message);
            }

            if (Session != null && Session.Mode == SessionMode.Host && !string.IsNullOrEmpty(Session.JoinCode))
            {
                GUI.Label(new Rect(left + 20f, top + 256f, 250f, 30f), $"ROOM CODE: {Session.JoinCode}", hudStyle);
                if (GUI.Button(new Rect(left + 276f, top + 256f, 106f, 28f),
                    Time.unscaledTime < codeCopiedUntil ? "COPIED!" : "COPY CODE"))
                {
                    GUIUtility.systemCopyBuffer = Session.JoinCode;
                    codeCopiedUntil = Time.unscaledTime + 1.5f;
                }

                if (GUI.Button(new Rect(left + 40f, top + 292f, width - 80f, 34f), "START MATCH")) BeginPlaying();
            }
            else if (Session != null && Session.Mode == SessionMode.Client)
            {
                GUI.Label(new Rect(left, top + 258f, width, 34f), "Connected. Waiting for host...", titleStyle);
                BeginPlaying();
            }
            else
            {
                GUI.Label(new Rect(left + 20f, top + height - 30f, width - 40f, 24f),
                    "Host shares the room code; friends type it to join.");
            }
        }

        private void BeginPlaying()
        {
            inMenu = false;
            wasConnected = false;
            WeaponPickup.LocalPickupsEnabled = HasAuthority;

            for (int i = 0; i < NetMatch.MaxFighters; i++)
            {
                remoteHealth[i] = Fighter.MaxHealth;
                remoteLives[i] = Fighter.StartingLives;
                remoteActive[i] = true;
            }

            if (HasAuthority) StartMatch();
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
        }

        private void DrawPauseMenu()
        {
            const float width = 360f;
            const float height = 430f;
            float left = Screen.width * 0.5f - width * 0.5f;
            float top = Screen.height * 0.5f - height * 0.5f;
            float inner = width - 80f;

            GUI.Box(new Rect(left, top, width, height), "");
            GUI.Label(new Rect(left, top + 16f, width, 40f), "PAUSED", resultStyle);

            // Match settings belong to whoever runs the simulation, so only the host may change them.
            bool canConfigure = HasAuthority;
            GUI.enabled = canConfigure;

            GUI.Label(new Rect(left + 40f, top + 64f, inner, 24f), "DIFFICULTY", hudStyle);
            DrawChoiceRow(left + 40f, top + 88f, inner,
                new[] { "EASY", "NORMAL", "HARD" },
                (int)difficulty,
                index => SetDifficulty((BotDifficulty)index));

            GUI.Label(new Rect(left + 40f, top + 130f, inner, 24f), "BOTS", hudStyle);
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
            }

            if (GUI.Button(new Rect(left + 40f, top + 354f, inner, 34f), "QUIT GAME"))
            {
                Application.Quit();
            }

            GUI.Label(new Rect(left, top + height - 26f, width, 24f), "ESC closes this menu", titleStyle);
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
