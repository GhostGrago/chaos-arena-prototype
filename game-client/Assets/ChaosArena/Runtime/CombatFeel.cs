using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// Impact feedback that is not tied to any one object: a very short time freeze on hit plus a camera shake
    /// impulse. Runs on unscaled time so it can restore itself even while it is slowing the game down.
    /// </summary>
    public sealed class CombatFeel : MonoBehaviour
    {
        private const float FreezeScale = 0.12f;
        private const float MaxFreezeSeconds = 0.06f;
        private const float MaxShake = 0.42f;
        private const float ShakeDecay = 3.6f;

        private static CombatFeel instance;
        private static bool paused;

        /// <summary>
        /// While the game is paused the pause owner controls Time.timeScale. Without this the hitstop timer
        /// would happily restore timeScale to 1 mid-pause and unfreeze the game behind the menu.
        /// </summary>
        public static void SetPaused(bool value)
        {
            paused = value;
            if (!value || instance == null) return;
            instance.freezeUntilUnscaled = -1f;
            instance.shake = 0f;
        }

        private float freezeUntilUnscaled = -1f;
        private float shake;

        /// <summary>Current shake amplitude; the camera reads this each frame.</summary>
        public static float ShakeAmount => instance != null ? instance.shake : 0f;

        public static void Ensure()
        {
            if (instance != null) return;
            GameObject host = new("Combat Feel");
            instance = host.AddComponent<CombatFeel>();
        }

        /// <summary>Called on every landed hit. <paramref name="strength01"/> scales freeze length and shake.</summary>
        public static void Impact(float strength01)
        {
            if (instance == null) return;
            instance.ApplyImpact(Mathf.Clamp01(strength01));
        }

        private void ApplyImpact(float strength01)
        {
            if (paused) return;

            // Batch runs have no display and must not have their fixed timestep disturbed by a time freeze.
            if (!Application.isBatchMode)
            {
                float freeze = Mathf.Lerp(0.02f, MaxFreezeSeconds, strength01);
                freezeUntilUnscaled = Mathf.Max(freezeUntilUnscaled, Time.unscaledTime + freeze);
                Time.timeScale = FreezeScale;
            }

            shake = Mathf.Min(MaxShake, shake + Mathf.Lerp(0.08f, MaxShake, strength01));
        }

        private void Update()
        {
            if (paused) return;

            if (freezeUntilUnscaled > 0f && Time.unscaledTime >= freezeUntilUnscaled)
            {
                freezeUntilUnscaled = -1f;
                Time.timeScale = 1f;
            }

            shake = Mathf.MoveTowards(shake, 0f, ShakeDecay * Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            // Never leave the game frozen if this object goes away mid-freeze.
            if (freezeUntilUnscaled > 0f) Time.timeScale = 1f;
            freezeUntilUnscaled = -1f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
