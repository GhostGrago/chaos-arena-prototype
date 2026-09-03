using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
            AssertBotCountRoster();
            AssertFreeForAllElimination();
            Debug.Log("CHAOS_ARENA_0110_ASSERTIONS_PASS: translucent jelly bodies, weapon mounts, pickups, platforms, bot roster, elimination, winner, and rematch reset verified.");
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

                Transform frame = fighter.transform.Find("Visual Root/Edge Frame");
                if (frame == null || frame.childCount < 4)
                {
                    throw new System.InvalidOperationException("Fighter is missing its neon edge frame.");
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

        /// <summary>Bot count must drive who actually takes part in the match.</summary>
        private void AssertBotCountRoster()
        {
            for (int count = 1; count <= MaxBots; count++)
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

            if (!matchEnded)
            {
                CheckRingOuts();
                RetargetBots();
            }

            if (matchEnded && autoRematchAt > 0f && Time.time >= autoRematchAt) StartMatch();

            if (Input.GetKeyDown(KeyCode.R)) StartMatch();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetBotCount(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetBotCount(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetBotCount(3);
            if (Input.GetKeyDown(KeyCode.F1)) SetDifficulty(BotDifficulty.Easy);
            if (Input.GetKeyDown(KeyCode.F2)) SetDifficulty(BotDifficulty.Normal);
            if (Input.GetKeyDown(KeyCode.F3)) SetDifficulty(BotDifficulty.Hard);
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
                if (brain == null || fighter.IsEliminated) continue;

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
            botCount = Mathf.Clamp(count, 1, MaxBots);
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
            for (int i = 0; i <= botCount; i++) roster.Add(allFighters[i]);

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
            foreach (Fighter fighter in roster)
            {
                FighterMotor motor = fighter.GetComponent<FighterMotor>();
                HumanController human = fighter.GetComponent<HumanController>();
                BotController brain = fighter.GetComponent<BotController>();
                if (motor != null) motor.enabled = active;
                if (human != null) human.enabled = active;
                if (brain != null) brain.enabled = active;
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

            // Glowing wireframe traced over the solid so the shape reads as lit hardware, not a grey block.
            GameObject frameObject = new("Edge Frame");
            frameObject.transform.SetParent(root, false);
            frameObject.transform.localScale = bodyObject.transform.localScale * 1.03f;
            ProceduralShapes.CreateEdgeFrame(shape, frameObject.transform, Color.Lerp(tint, Color.white, 0.12f));

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

            GUI.Label(new Rect(0f, 12f, Screen.width, 30f), "PROTOTYPE 0.1.10 — TRANSLUCENT JELLY", titleStyle);

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

            GUI.Label(new Rect(24f, Screen.height - 84f, 900f, 26f), "More hits make fighters easier to launch — ring-outs cost lives.");
            GUI.Label(new Rect(24f, Screen.height - 60f, 1100f, 26f), "A/D move  •  Space double-jump  •  S drop through  •  J fire  •  R restart now");
            GUI.Label(new Rect(24f, Screen.height - 36f, 1100f, 26f),
                $"1/2/3 bot count (now {botCount})  •  F1/F2/F3 difficulty (now {difficulty.ToString().ToUpperInvariant()})");
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
