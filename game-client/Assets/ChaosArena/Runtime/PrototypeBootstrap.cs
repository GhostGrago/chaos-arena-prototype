using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChaosArena
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        private readonly List<Fighter> fighters = new();
        private GUIStyle titleStyle;
        private GUIStyle hudStyle;
        private GUIStyle resultStyle;
        private float smokeTestExitTime = -1f;
        private BotController botBrain;
        private Fighter player;
        private Fighter bot;
        private Fighter winner;
        private bool matchEnded;
        private float matchStartedAt;
        private float matchDuration;

        // Playtest builds restart on their own so a session never parks on a result screen waiting for input.
        private const float AutoRematchDelay = 2.5f;
        private float autoRematchAt = -1f;

        private void Awake()
        {
            BuildArena();
            player = CreateFighter("PLAYER", new Color(0.2f, 0.7f, 1f), new Vector3(-4f, 1.4f, 0f));
            bot = CreateFighter("BOT", new Color(1f, 0.35f, 0.25f), new Vector3(4f, 1.4f, 0f));
            player.gameObject.AddComponent<HumanController>();
            BotController brain = bot.gameObject.AddComponent<BotController>();
            brain.SetTarget(player.transform);
            brain.SetDifficulty(BotDifficulty.Easy);
            botBrain = brain;
            Physics.IgnoreCollision(player.GetComponent<Collider>(), bot.GetComponent<Collider>(), true);
            SetupCamera(player.transform);
            matchStartedAt = Time.time;

            if (Application.isBatchMode && System.Environment.GetCommandLineArgs().Contains("-chaosSmokeTest"))
            {
                RunPrototype014SmokeAssertions();
                smokeTestExitTime = Time.realtimeSinceStartup + 2f;
                Debug.Log("CHAOS_ARENA_SMOKE_READY: arena, player, bot, camera, and physics initialized.");
            }

        }

        private void RunPrototype014SmokeAssertions()
        {
            if (WeaponPickup.All.Count != 3) throw new System.InvalidOperationException("Expected three weapon pickups.");
            if (FindAnyObjectByType<ArenaCameraFollow>() == null) throw new System.InvalidOperationException("Missing local-player camera follow.");

            AssertProjectileSurvivesItsOwnMuzzleFlash();
            if (player.GetComponent<ProtectionShield>() == null || bot.GetComponent<ProtectionShield>() == null)
            {
                throw new System.InvalidOperationException("Both fighters need a respawn protection shield.");
            }

            bool first = bot.LoseLife();
            bool second = bot.LoseLife();
            bool third = bot.LoseLife();
            if (first || second || !third || bot.Lives != 0) throw new System.InvalidOperationException("Stock elimination contract failed.");
            EndMatch(bot);
            if (!matchEnded || winner != player) throw new System.InvalidOperationException("Winner contract failed.");

            StartMatch();
            if (matchEnded || player.Lives != Fighter.StartingLives || bot.Lives != Fighter.StartingLives ||
                !player.gameObject.activeInHierarchy || !bot.gameObject.activeInHierarchy ||
                player.GetComponent<FighterMotor>().WeaponId != PrototypeWeaponId.Carbine ||
                bot.GetComponent<FighterMotor>().WeaponId != PrototypeWeaponId.Carbine)
            {
                throw new System.InvalidOperationException("Rematch reset contract failed.");
            }
            Debug.Log("CHAOS_ARENA_014_ASSERTIONS_PASS: pickups, elimination, winner, and rematch reset verified.");
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
                foreach (Fighter fighter in fighters)
                {
                    if (!fighter.gameObject.activeInHierarchy) continue;
                    Vector3 position = fighter.transform.position;
                    if (position.y < -7f || Mathf.Abs(position.x) > 16f)
                    {
                        fighter.GetComponent<FighterMotor>().ResetWeapon();
                        bool eliminated = fighter.LoseLife();
                        if (eliminated)
                        {
                            EndMatch(fighter);
                            break;
                        }
                    }
                }
            }

            if (matchEnded && autoRematchAt > 0f && Time.time >= autoRematchAt)
            {
                StartMatch();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                StartMatch();
            }

            if (Input.GetKeyDown(KeyCode.F1)) botBrain.SetDifficulty(BotDifficulty.Easy);
            if (Input.GetKeyDown(KeyCode.F2)) botBrain.SetDifficulty(BotDifficulty.Normal);
            if (Input.GetKeyDown(KeyCode.F3)) botBrain.SetDifficulty(BotDifficulty.Hard);
        }

        private void EndMatch(Fighter loser)
        {
            matchEnded = true;
            winner = loser == player ? bot : player;
            matchDuration = Time.time - matchStartedAt;
            autoRematchAt = Time.time + AutoRematchDelay;
            SetCombatActive(false);
            ClearProjectiles();
        }

        private void StartMatch()
        {
            ClearProjectiles();
            WeaponPickup.ResetAll();
            foreach (Fighter fighter in fighters)
            {
                fighter.ResetRound();
                fighter.GetComponent<FighterMotor>().ResetWeapon();
            }
            winner = null;
            matchEnded = false;
            autoRematchAt = -1f;
            matchStartedAt = Time.time;
            SetCombatActive(true);
        }

        private void SetCombatActive(bool active)
        {
            foreach (Fighter fighter in fighters)
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

        private Fighter CreateFighter(string fighterName, Color color, Vector3 spawn)
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
            BuildFighterModel(fighterObject.transform, visual, color);
            fighterObject.AddComponent<ProtectionShield>();
            fighter.Initialize(fighterName, color, spawn);
            fighters.Add(fighter);
            return fighter;
        }

        private static void BuildArena()
        {
            Physics.gravity = new Vector3(0f, -22f, 0f);
            Camera existingCamera = FindAnyObjectByType<Camera>();
            if (existingCamera != null) Destroy(existingCamera.gameObject);

            CreatePlatform("Main Platform", new Vector3(0f, -0.25f, 0f), new Vector3(19f, 1f, 3f), new Color(0.18f, 0.22f, 0.3f), false);
            CreatePlatform("Left Platform", new Vector3(-6f, 3f, 0f), new Vector3(5f, 0.6f, 3f), new Color(0.25f, 0.3f, 0.4f), true);
            CreatePlatform("Right Platform", new Vector3(6f, 3f, 0f), new Vector3(5f, 0.6f, 3f), new Color(0.25f, 0.3f, 0.4f), true);
            CreatePlatform("Top Platform", new Vector3(0f, 5.5f, 0f), new Vector3(4.5f, 0.6f, 3f), new Color(0.32f, 0.37f, 0.48f), true);

            WeaponPickup.Spawn(PrototypeWeaponProfile.PulseSmg, new Vector3(-6f, 4.05f, 0f));
            WeaponPickup.Spawn(PrototypeWeaponProfile.ScatterBlaster, new Vector3(0f, 6.55f, 0f));
            WeaponPickup.Spawn(PrototypeWeaponProfile.RocketLauncher, new Vector3(6f, 4.05f, 0f));

            BuildBackground();
            BuildArenaDetails();

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.38f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.16f, 0.23f);
            RenderSettings.ambientGroundColor = new Color(0.045f, 0.055f, 0.075f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.09f, 0.13f, 0.19f);
            // Pushed out so the arena keeps its contrast when the camera pulls back near a ring-out edge.
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 76f;

            GameObject lightObject = new("Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(1f, 0.88f, 0.72f);
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            GameObject fillObject = new("Cool Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.65f;
            fill.color = new Color(0.35f, 0.58f, 1f);
            fillObject.transform.rotation = Quaternion.Euler(25f, 145f, 0f);
        }

        private static void CreatePlatform(string platformName, Vector3 position, Vector3 scale, Color color, bool oneWay)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = platformName;
            platform.transform.SetPositionAndRotation(position, Quaternion.identity);
            platform.transform.localScale = scale;
            PrototypeMaterials.Assign(platform.GetComponent<Renderer>(), color);
            if (oneWay) platform.AddComponent<OneWayPlatform>();

            CreateVisualPrimitive(platformName + " Front Trim", PrimitiveType.Cube,
                position + new Vector3(0f, scale.y * 0.32f, -scale.z * 0.52f),
                new Vector3(scale.x * 0.96f, 0.1f, 0.12f), new Color(0.16f, 0.62f, 0.82f), true);
        }

        private static void BuildArenaDetails()
        {
            Color support = new(0.08f, 0.11f, 0.16f);
            CreateVisualPrimitive("Left Support", PrimitiveType.Cube, new Vector3(-6.5f, -2.6f, 0.8f), new Vector3(1.1f, 4f, 1.1f), support);
            CreateVisualPrimitive("Right Support", PrimitiveType.Cube, new Vector3(6.5f, -2.6f, 0.8f), new Vector3(1.1f, 4f, 1.1f), support);
            CreateVisualPrimitive("Underdeck", PrimitiveType.Cube, new Vector3(0f, -1.4f, 0.9f), new Vector3(14f, 1.25f, 1.1f), new Color(0.1f, 0.13f, 0.18f));

            for (int i = -4; i <= 4; i++)
            {
                CreateVisualPrimitive("Deck Light " + i, PrimitiveType.Cube, new Vector3(i * 2f, -0.62f, -1.58f),
                    new Vector3(0.5f, 0.12f, 0.08f), new Color(1f, 0.48f, 0.12f), true);
            }
        }

        private static void BuildBackground()
        {
            CreateVisualPrimitive("Distant Sky Wall", PrimitiveType.Cube, new Vector3(0f, 8f, 28f),
                new Vector3(75f, 30f, 1f), new Color(0.055f, 0.09f, 0.15f), true);

            Color mountain = new(0.09f, 0.15f, 0.22f);
            for (int i = -4; i <= 4; i++)
            {
                GameObject peak = CreateVisualPrimitive("Distant Peak " + i, PrimitiveType.Cube,
                    new Vector3(i * 8f, -0.5f + Mathf.Abs(i % 2) * 1.2f, 19f + Mathf.Abs(i) * 0.6f),
                    new Vector3(8f, 8f + Mathf.Abs(i % 3) * 3f, 4f), mountain);
                peak.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            }

            Color tower = new(0.075f, 0.11f, 0.16f);
            for (int i = -5; i <= 5; i++)
            {
                float height = 4f + Mathf.Repeat(i * 2.7f, 5f);
                CreateVisualPrimitive("Skyline " + i, PrimitiveType.Cube, new Vector3(i * 4.5f, height * 0.5f - 2f, 12f + Mathf.Abs(i % 2) * 2f),
                    new Vector3(3.2f, height, 2.6f), tower);
            }

            CreateVisualPrimitive("Distant Moon", PrimitiveType.Sphere, new Vector3(11f, 9.5f, 17f),
                Vector3.one * 3.4f, new Color(0.85f, 0.9f, 1f), true);
        }

        private static void BuildFighterModel(Transform fighter, FighterVisual visual, Color tint)
        {
            GameObject visualRootObject = new("Visual Root");
            visualRootObject.transform.SetParent(fighter, false);
            Transform root = visualRootObject.transform;

            CreateModelPart("Tint_Torso", PrimitiveType.Capsule, root, new Vector3(0f, 0.03f, 0f), new Vector3(0.55f, 0.58f, 0.48f), tint);
            CreateModelPart("Tint_Head", PrimitiveType.Sphere, root, new Vector3(0f, 0.62f, 0f), new Vector3(0.58f, 0.58f, 0.52f), tint);
            CreateModelPart("Face Visor", PrimitiveType.Cube, root, new Vector3(0.18f, 0.65f, -0.25f), new Vector3(0.38f, 0.17f, 0.08f), new Color(0.05f, 0.13f, 0.2f));
            CreateModelPart("Backpack", PrimitiveType.Cube, root, new Vector3(-0.31f, 0.08f, 0.1f), new Vector3(0.24f, 0.55f, 0.42f), new Color(0.12f, 0.15f, 0.2f));

            Transform leftArm = CreateLimb("Tint_Left Arm", root, new Vector3(-0.36f, 0.13f, 0f), tint);
            Transform rightArm = CreateLimb("Tint_Right Arm", root, new Vector3(0.36f, 0.15f, -0.08f), tint);
            Transform leftLeg = CreateLimb("Tint_Left Leg", root, new Vector3(-0.18f, -0.52f, 0.08f), tint);
            Transform rightLeg = CreateLimb("Tint_Right Leg", root, new Vector3(0.18f, -0.52f, -0.08f), tint);

            CreateModelPart("Weapon Body", PrimitiveType.Cube, rightArm, new Vector3(0.34f, -0.12f, -0.22f), new Vector3(0.58f, 0.2f, 0.18f), new Color(0.12f, 0.14f, 0.18f));
            CreateModelPart("Weapon Barrel", PrimitiveType.Cylinder, rightArm, new Vector3(0.67f, -0.12f, -0.22f), new Vector3(0.08f, 0.27f, 0.08f), new Color(0.38f, 0.44f, 0.5f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            visual.Bind(root, leftArm, rightArm, leftLeg, rightLeg);
        }

        private static Transform CreateLimb(string name, Transform parent, Vector3 position, Color color)
        {
            GameObject pivot = new(name + " Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = position;
            CreateModelPart(name, PrimitiveType.Capsule, pivot.transform, new Vector3(0f, -0.2f, 0f), new Vector3(0.22f, 0.32f, 0.22f), color);
            return pivot.transform;
        }

        private static GameObject CreateModelPart(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            PrototypeMaterials.Assign(part.GetComponent<Renderer>(), color);
            return part;
        }

        private static GameObject CreateVisualPrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, bool unlit = false)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.position = position;
            visual.transform.localScale = scale;
            Collider collider = visual.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            PrototypeMaterials.Assign(visual.GetComponent<Renderer>(), color, unlit);
            return visual;
        }

        private static void SetupCamera(Transform localPlayer)
        {
            GameObject cameraObject = new("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = false;
            camera.fieldOfView = 39f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 90f;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.11f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 4.6f, -27f);
            cameraObject.transform.LookAt(new Vector3(0f, 2.25f, 0.5f));
            cameraObject.AddComponent<ArenaCameraFollow>().SetTarget(localPlayer);
        }

        private void OnGUI()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            resultStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            GUI.Label(new Rect(0f, 12f, Screen.width, 30f), "PROTOTYPE 0.1.4 — MATCH & WEAPONS PASS", titleStyle);
            for (int i = 0; i < fighters.Count; i++)
            {
                Fighter fighter = fighters[i];
                float x = i == 0 ? 24f : Screen.width - 300f;
                string identity = i == 1 && botBrain != null ? $"BOT — {botBrain.Difficulty.ToString().ToUpperInvariant()}" : fighter.DisplayName;
                GUI.Label(new Rect(x, 52f, 280f, 28f), $"{identity}  LIVES {fighter.Lives}", hudStyle);
                FighterMotor motor = fighter.GetComponent<FighterMotor>();
                string ammoText = motor.Ammo < 0 ? "∞" : motor.Ammo.ToString();
                GUI.Label(new Rect(x, 78f, 280f, 24f), $"{motor.WeaponName}  AMMO {ammoText}");
            }

            if (matchEnded && winner != null)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 235f, Screen.height * 0.5f - 80f, 470f, 160f), "");
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 62f, 440f, 55f),
                    $"{winner.DisplayName} WINS!", resultStyle);
                float restartIn = Mathf.Max(0f, autoRematchAt - Time.time);
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f + 2f, 440f, 35f),
                    $"MATCH {matchDuration:0.0}s   •   NEXT MATCH IN {restartIn:0.0}s", titleStyle);
            }

            GUI.Label(new Rect(24f, Screen.height - 62f, 900f, 26f), "More hits make fighters easier to launch — ring-outs cost lives.");
            GUI.Label(new Rect(24f, Screen.height - 38f, 1100f, 26f), "A/D move  •  Space double-jump  •  S drop through  •  J fire  •  Collect weapons  •  R rematch");
        }
    }
}
