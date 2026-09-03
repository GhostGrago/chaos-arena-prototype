using UnityEngine;

namespace ChaosArena
{
    public sealed class Fighter : MonoBehaviour
    {
        public const float MaxHealth = 100f;
        public const int StartingLives = 3;

        // Grace window after a ring-out, long enough to reorient before re-entering the fight. Protection
        // weakens incoming hits rather than voiding them: full immunity made attacks look like they had
        // failed to register, which reads as a bug even when the shield ring is visible.
        public const float RespawnProtectionSeconds = 1.2f;
        public const float RoundStartProtectionSeconds = 0.9f;
        public const float ProtectedDamageScale = 0.35f;
        public const float ProtectedKnockbackScale = 0.4f;

        // Collected power-ups. Both are defensive on purpose: this is a knock-out game, so the interesting
        // pickup is one that changes how hard you are to remove rather than one that adds more damage on top
        // of the weapon you are already carrying.
        public const float ShieldPowerUpSeconds = 7f;
        public const float ShrinkPowerUpSeconds = 9f;
        public const float ShrunkScale = 0.6f;

        [SerializeField] private string displayName = "Fighter";
        [SerializeField] private Color fighterColor = Color.white;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private float baseCapsuleHeight;
        private float baseCapsuleRadius;
        private Vector3 spawnPoint;
        private float protectedUntil;
        private float shieldUntil;
        private float shrunkUntil;
        private bool shrinkApplied;

        public string DisplayName => displayName;

        /// <summary>Seats change owner as players join, so the label is not fixed at creation.</summary>
        public void SetDisplayName(string newName) => displayName = newName;

        /// <summary>
        /// Applies host-authoritative vitals on a client. Without this the client HUD kept showing the local
        /// starting values and the protection shield never appeared, because both derive from private state
        /// that only the host mutates.
        /// </summary>
        public void ApplyRemoteVitals(float health, int lives, float protectionRemaining)
        {
            Health = health;
            Lives = lives;
            protectedUntil = protectionRemaining > 0f ? Time.time + protectionRemaining : 0f;
        }
        public float Health { get; private set; } = MaxHealth;
        public int Lives { get; private set; } = StartingLives;
        public float KnockbackMultiplier => Mathf.Lerp(3.5f, 1f, Health / MaxHealth);
        public float Danger01 => 1f - Health / MaxHealth;
        public bool IsEliminated => Lives <= 0;
        public bool IsProtected => Time.time < protectedUntil;
        public Color TintColor => fighterColor;
        public float ProtectionRemaining => Mathf.Max(0f, protectedUntil - Time.time);

        public bool HasShield => Time.time < shieldUntil;
        public bool IsShrunk => Time.time < shrunkUntil;

        /// <summary>Respawn grace and the shield power-up soften hits the same way, so they share one test.</summary>
        public bool IsDamped => IsProtected || HasShield;

        /// <summary>How large the fighter currently is; the shrink power-up drives this below one.</summary>
        public float SizeScale => IsShrunk ? ShrunkScale : 1f;

        /// <summary>Seconds left on whichever defensive effect is running, for the shield ring to display.</summary>
        public float DampedRemaining => Mathf.Max(ProtectionRemaining, Mathf.Max(0f, shieldUntil - Time.time));

        public void GrantPowerUp(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Shield:
                    shieldUntil = Mathf.Max(shieldUntil, Time.time + ShieldPowerUpSeconds);
                    break;
                case PowerUpKind.Shrink:
                    shrunkUntil = Mathf.Max(shrunkUntil, Time.time + ShrinkPowerUpSeconds);
                    break;
            }
        }

        public void Initialize(string newName, Color newColor, Vector3 newSpawnPoint)
        {
            displayName = newName;
            fighterColor = newColor;
            spawnPoint = newSpawnPoint;
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                baseCapsuleHeight = capsule.height;
                baseCapsuleRadius = capsule.radius;
            }

            ApplyColor();
            Respawn(false);
        }

        /// <summary>
        /// Keeps the hit volume in step with the shrink power-up. Only applied on the transition: writing the
        /// capsule every frame churns the physics shape for no reason.
        /// </summary>
        private void Update()
        {
            if (capsule == null) return;

            bool shouldBeShrunk = IsShrunk;
            if (shouldBeShrunk == shrinkApplied) return;

            shrinkApplied = shouldBeShrunk;
            float scale = shouldBeShrunk ? ShrunkScale : 1f;
            capsule.height = baseCapsuleHeight * scale;
            capsule.radius = baseCapsuleRadius * scale;
        }

        /// <summary>Moves this fighter's home position, used when a map swap changes where the deck is.</summary>
        public void MoveSpawn(Vector3 newSpawnPoint)
        {
            spawnPoint = newSpawnPoint;
        }

        public void TakeHit(float damage, Vector3 baseKnockback)
        {
            if (IsEliminated) return;

            float damageScale = IsDamped ? ProtectedDamageScale : 1f;
            float knockbackScale = IsDamped ? ProtectedKnockbackScale : 1f;

            Health = Mathf.Max(0f, Health - damage * damageScale);
            Vector3 impulse = baseKnockback * (KnockbackMultiplier * knockbackScale);
            body.AddForce(impulse, ForceMode.VelocityChange);
            GetComponent<FighterVisual>()?.OnHit(Danger01);

            // Heavier launches freeze and shake harder, so a fight-ending hit feels different from chip damage.
            float weight = Mathf.InverseLerp(1.5f, 12f, impulse.magnitude);
            CombatFeel.Impact(weight);

            // Without this the motor cancels the impulse within ~0.09s and the hit looks like it did nothing.
            GetComponent<FighterMotor>()?.ApplyKnockbackStun(Mathf.Lerp(0.15f, 0.45f, weight));

            PlayHitReaction(impulse, weight);
        }

        /// <summary>
        /// Layered impact feedback. Every hit gets a ring and a directional spray so even chip damage reads
        /// as contact; jelly and a launch streak are reserved for hits that actually send someone flying, so
        /// the heavy feedback keeps its meaning.
        /// </summary>
        private void PlayHitReaction(Vector3 impulse, float weight)
        {
            Vector3 direction = impulse.sqrMagnitude > 0.0001f ? impulse.normalized : Vector3.right;
            Vector3 origin = transform.position;

            CombatVfx.Shockwave(origin, fighterColor, weight);
            CombatVfx.ImpactDirectional(origin, direction, fighterColor, true, weight);

            if (weight > 0.45f)
            {
                CombatVfx.JellyBurst(origin, fighterColor,
                    Mathf.RoundToInt(Mathf.Lerp(4f, 12f, weight)), Mathf.Lerp(0.5f, 0.85f, weight), 5.5f);
            }

            // Threshold lowered from 0.6 after playtest: the streak was reserved for so few hits that it
            // barely appeared. The separate row of ghost blobs that used to fire here was dropped in favour
            // of boosting the fighter's own continuous streak, so a launch draws one unbroken line rather
            // than a dotted trail that did not connect to the body.
            if (weight > 0.35f)
            {
                GetComponent<FighterVisual>()?.BoostTrail(Mathf.Lerp(0.25f, 0.7f, weight));
            }
        }

        /// <summary>Replays the same reaction on a client, which never runs TakeHit itself.</summary>
        public void PlayRemoteHitReaction(Vector3 impulseDirection, float weight)
        {
            PlayHitReaction(impulseDirection * Mathf.Max(0.01f, weight), Mathf.Clamp01(weight));
        }

        public bool LoseLife()
        {
            Lives = Mathf.Max(0, Lives - 1);
            if (Lives <= 0)
            {
                Health = 0f;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                // The jelly comes apart instead of the model simply blinking out.
                CombatVfx.JellyBurst(transform.position, fighterColor, 30, 1.15f, 9f);
                CombatFeel.Impact(1f);
                gameObject.SetActive(false);
                return true;
            }

            // Losing a stock also bursts, just smaller than a final elimination.
            CombatVfx.JellyBurst(transform.position, fighterColor, 18, 0.9f, 7f);
            CombatFeel.Impact(0.8f);
            Respawn(true);
            protectedUntil = Time.time + RespawnProtectionSeconds;
            return false;
        }

        public void ResetRound()
        {
            gameObject.SetActive(true);
            Lives = StartingLives;
            protectedUntil = Time.time + RoundStartProtectionSeconds;
            // Power-ups are per-match, so a rematch never starts with someone still shielded or shrunk.
            shieldUntil = 0f;
            shrunkUntil = 0f;
            Respawn(true);
        }

        private void Respawn(bool clearVelocity)
        {
            Health = MaxHealth;
            transform.position = spawnPoint;
            if (body != null && clearVelocity)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void ApplyColor()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer fighterRenderer in renderers)
            {
                if (fighterRenderer.gameObject.name.StartsWith("Tint_"))
                {
                    PrototypeMaterials.SetColor(fighterRenderer, fighterColor);
                }
            }
        }
    }
}
