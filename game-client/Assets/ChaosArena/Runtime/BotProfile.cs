using UnityEngine;

namespace ChaosArena
{
    public enum BotDifficulty { Easy, Normal, Hard, Expert, Master }

    /// <summary>
    /// Every tuning value that separates one bot skill tier from another, in one table.
    ///
    /// The ladder was rebuilt in 0.3.0 after playtest: the old three tiers topped out below what a competent
    /// human needs, so the whole curve moved up one step (the new EASY is the old NORMAL, the new NORMAL is
    /// the old HARD) and three genuinely harder tiers were added above it. Skill is expressed as accuracy and
    /// positioning rather than as raw stat cheating: higher tiers lead their shots better, refuse blocked or
    /// out-of-range shots, hold the range their weapon actually wants, dodge incoming rounds and recover from
    /// knockback instead of walking off the deck. None of them get extra damage or speed.
    /// </summary>
    public readonly struct BotProfile
    {
        /// <summary>Seconds between movement re-plans. Shorter means the bot corrects a bad position sooner.</summary>
        public readonly Vector2 DecisionInterval;

        /// <summary>Seconds a firing solution must hold before the bot pulls the trigger.</summary>
        public readonly float ReactionDelay;

        /// <summary>Vertical distance the target may be off the shot line and still be considered hittable.</summary>
        public readonly float AimTolerance;

        /// <summary>How much of the target's predicted movement is compensated for, 0 = shoots at where it stands.</summary>
        public readonly float LeadAccuracy;

        public readonly Vector2 BurstSeconds;
        public readonly Vector2 BurstPause;

        public readonly float PickupPursuit;
        public readonly float DropThroughChance;
        public readonly float RandomJumpChance;

        /// <summary>Chance of holding the range band its current weapon is actually good at.</summary>
        public readonly float RangeDiscipline;

        /// <summary>Chance of checking for ground before walking, which is what stops self-inflicted ring-outs.</summary>
        public readonly float EdgeSafety;

        /// <summary>Chance of jumping out of the path of an incoming round.</summary>
        public readonly float DodgeChance;

        /// <summary>Refuses shots blocked by geometry instead of firing into a platform.</summary>
        public readonly bool UsesLineOfSight;

        /// <summary>Spends the second jump to get back on the deck after being launched.</summary>
        public readonly bool RecoversFromLaunch;

        public BotProfile(Vector2 decisionInterval, float reactionDelay, float aimTolerance, float leadAccuracy,
            Vector2 burstSeconds, Vector2 burstPause, float pickupPursuit, float dropThroughChance,
            float randomJumpChance, float rangeDiscipline, float edgeSafety, float dodgeChance,
            bool usesLineOfSight, bool recoversFromLaunch)
        {
            DecisionInterval = decisionInterval;
            ReactionDelay = reactionDelay;
            AimTolerance = aimTolerance;
            LeadAccuracy = leadAccuracy;
            BurstSeconds = burstSeconds;
            BurstPause = burstPause;
            PickupPursuit = pickupPursuit;
            DropThroughChance = dropThroughChance;
            RandomJumpChance = randomJumpChance;
            RangeDiscipline = rangeDiscipline;
            EdgeSafety = edgeSafety;
            DodgeChance = dodgeChance;
            UsesLineOfSight = usesLineOfSight;
            RecoversFromLaunch = recoversFromLaunch;
        }

        /// <summary>Menu and HUD names, indexed by <see cref="BotDifficulty"/>.</summary>
        public static readonly string[] Labels = { "EASY", "NORMAL", "HARD", "EXPERT", "MASTER" };

        public static BotProfile Get(BotDifficulty difficulty) => difficulty switch
        {
            BotDifficulty.Normal => Normal,
            BotDifficulty.Hard => Hard,
            BotDifficulty.Expert => Expert,
            BotDifficulty.Master => Master,
            _ => Easy
        };

        /// <summary>Was NORMAL before the ladder moved up. Aims loosely and gives ground readily.</summary>
        public static BotProfile Easy => new(
            new Vector2(0.30f, 0.52f), 0.30f, 1.45f, 0.35f,
            new Vector2(0.70f, 1.02f), new Vector2(0.45f, 0.80f),
            0.72f, 0.55f, 0.14f, 0f, 0.45f, 0f, false, false);

        /// <summary>Was HARD. Now the default: keeps line of sight, holds range and climbs back after a launch.</summary>
        public static BotProfile Normal => new(
            new Vector2(0.16f, 0.32f), 0.20f, 1.25f, 0.60f,
            new Vector2(0.90f, 1.25f), new Vector2(0.20f, 0.42f),
            0.82f, 0.70f, 0.16f, 0.45f, 0.70f, 0.20f, true, true);

        public static BotProfile Hard => new(
            new Vector2(0.12f, 0.24f), 0.13f, 1.05f, 0.80f,
            new Vector2(1.05f, 1.45f), new Vector2(0.14f, 0.30f),
            0.88f, 0.80f, 0.18f, 0.70f, 0.85f, 0.42f, true, true);

        public static BotProfile Expert => new(
            new Vector2(0.09f, 0.17f), 0.07f, 0.90f, 0.95f,
            new Vector2(1.25f, 1.75f), new Vector2(0.09f, 0.20f),
            0.93f, 0.90f, 0.20f, 0.88f, 0.95f, 0.65f, true, true);

        /// <summary>Near-instant reaction, full lead prediction and disciplined spacing. Meant to be unfair.</summary>
        public static BotProfile Master => new(
            new Vector2(0.06f, 0.12f), 0.02f, 0.78f, 1f,
            new Vector2(1.50f, 2.10f), new Vector2(0.05f, 0.12f),
            0.97f, 0.95f, 0.22f, 1f, 1f, 0.85f, true, true);

        /// <summary>Distance band a weapon actually wants to fight at, used by range-disciplined tiers.</summary>
        public static Vector2 PreferredBand(PrototypeWeaponId id) => id switch
        {
            PrototypeWeaponId.Sniper => new Vector2(7.5f, 13.5f),
            PrototypeWeaponId.ScatterBlaster => new Vector2(1.6f, 4.2f),
            PrototypeWeaponId.PulseSmg => new Vector2(3.2f, 7.5f),
            _ => new Vector2(3.6f, 8.5f)
        };

        /// <summary>Past this the shot is wasted, so no tier takes it. Scatter's spread makes it the shortest.</summary>
        public static float EffectiveRange(PrototypeWeaponId id) => id switch
        {
            PrototypeWeaponId.Sniper => 17.5f,
            PrototypeWeaponId.ScatterBlaster => 6f,
            PrototypeWeaponId.PulseSmg => 11f,
            _ => 13f
        };
    }
}
