using UnityEngine;

namespace ChaosArena
{
    public sealed class Fighter : MonoBehaviour
    {
        public const float MaxHealth = 100f;
        public const int StartingLives = 3;

        // Time a fighter is immune after a ring-out. Long enough to reorient before re-entering the fight.
        public const float RespawnProtectionSeconds = 1.2f;
        public const float RoundStartProtectionSeconds = 0.9f;

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
            if (IsEliminated || IsProtected) return;
            Health = Mathf.Max(0f, Health - damage);
            body.AddForce(baseKnockback * KnockbackMultiplier, ForceMode.VelocityChange);
            GetComponent<FighterVisual>()?.OnHit(Danger01);
        }

        public bool LoseLife()
        {
            Lives = Mathf.Max(0, Lives - 1);
            if (Lives <= 0)
            {
                Health = 0f;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                gameObject.SetActive(false);
                return true;
            }

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
