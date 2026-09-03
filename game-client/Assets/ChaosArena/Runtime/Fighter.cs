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

        [SerializeField] private string displayName = "Fighter";
        [SerializeField] private Color fighterColor = Color.white;

        private Rigidbody body;
        private Vector3 spawnPoint;
        private float protectedUntil;

        public string DisplayName => displayName;
        public float Health { get; private set; } = MaxHealth;
        public int Lives { get; private set; } = StartingLives;
        public float KnockbackMultiplier => Mathf.Lerp(3.5f, 1f, Health / MaxHealth);
        public float Danger01 => 1f - Health / MaxHealth;
        public bool IsEliminated => Lives <= 0;
        public bool IsProtected => Time.time < protectedUntil;
        public Color TintColor => fighterColor;
        public float ProtectionRemaining => Mathf.Max(0f, protectedUntil - Time.time);

        public void Initialize(string newName, Color newColor, Vector3 newSpawnPoint)
        {
            displayName = newName;
            fighterColor = newColor;
            spawnPoint = newSpawnPoint;
            body = GetComponent<Rigidbody>();
            ApplyColor();
            Respawn(false);
        }

        public void TakeHit(float damage, Vector3 baseKnockback)
        {
            if (IsEliminated) return;

            float damageScale = IsProtected ? ProtectedDamageScale : 1f;
            float knockbackScale = IsProtected ? ProtectedKnockbackScale : 1f;

            Health = Mathf.Max(0f, Health - damage * damageScale);
            Vector3 impulse = baseKnockback * (KnockbackMultiplier * knockbackScale);
            body.AddForce(impulse, ForceMode.VelocityChange);
            GetComponent<FighterVisual>()?.OnHit(Danger01);

            // Heavier launches freeze and shake harder, so a fight-ending hit feels different from chip damage.
            float weight = Mathf.InverseLerp(1.5f, 12f, impulse.magnitude);
            CombatFeel.Impact(weight);

            // Without this the motor cancels the impulse within ~0.09s and the hit looks like it did nothing.
            GetComponent<FighterMotor>()?.ApplyKnockbackStun(Mathf.Lerp(0.15f, 0.45f, weight));

            // A big launch sprays jelly. Small chip damage does not, so the burst stays meaningful.
            if (weight > 0.45f)
            {
                CombatVfx.JellyBurst(transform.position, fighterColor,
                    Mathf.RoundToInt(Mathf.Lerp(4f, 12f, weight)), Mathf.Lerp(0.5f, 0.85f, weight), 5.5f);
            }
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
