using UnityEngine;

namespace ChaosArena
{
    [RequireComponent(typeof(FighterMotor))]
    public sealed class BotController : MonoBehaviour
    {
        private enum Tactic { Approach, HoldRange, Reposition, EscapeEdge }

        private FighterMotor motor;
        private Fighter self;
        private Collider ownCollider;
        private Transform target;
        private FighterMotor targetMotor;

        private float nextDecisionTime;
        private bool jumpDecision;
        private bool dropDecision;
        private float fireBurstEnds;
        private float nextFireBurst;
        private float plannedMovement;
        private Tactic tactic;
        private WeaponPickup desiredPickup;

        // Aiming runs on a slower clock than rendering: a line-of-sight raycast per bot per frame is wasted
        // work when the tier's reaction delay already governs how fast the bot is allowed to respond.
        private const float AimRefreshInterval = 0.04f;
        private float nextAimCheck;
        private bool hasFiringSolution;
        private float solutionHeldSince = -1f;
        private float nextDodgeCheck;

        // Past the deck edge or below it, holding the current plan is fatal, so recovery overrides it.
        private const float DeckEdge = 10.2f;
        private const float RecoveryFloor = -3.5f;

        public BotDifficulty Difficulty { get; private set; } = BotDifficulty.Normal;

        public void SetDifficulty(BotDifficulty difficulty)
        {
            Difficulty = difficulty;
            nextDecisionTime = 0f;
            fireBurstEnds = 0f;
            nextFireBurst = 0f;
            solutionHeldSince = -1f;
        }

        /// <summary>
        /// Assigns the rival to fight. The early-out is load-bearing, not an optimisation: the match loop
        /// re-issues the nearest rival every single frame, so clearing the aim state unconditionally reset
        /// the reaction timer before it could ever reach the tier's reaction delay, and bots moved and dodged
        /// but never once fired.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            if (target == newTarget) return;

            target = newTarget;
            targetMotor = newTarget != null ? newTarget.GetComponent<FighterMotor>() : null;
            solutionHeldSince = -1f;
        }

        private void Awake()
        {
            motor = GetComponent<FighterMotor>();
            self = GetComponent<Fighter>();
            ownCollider = GetComponent<Collider>();
        }

        private void Update()
        {
            // With 1-3 bots the roster changes between matches, and an eliminated rival is deactivated rather
            // than destroyed, so an inactive target must stop the bot instead of steering it at a corpse.
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                motor.SetCommands(0f, false, false, false);
                return;
            }

            BotProfile profile = BotProfile.Get(Difficulty);
            Vector3 offset = target.position - transform.position;
            float targetDirection = offset.x >= 0f ? 1f : -1f;

            if (Time.time >= nextDecisionTime)
            {
                PlanMovement(profile, offset, targetDirection);
            }

            if (profile.DodgeChance > 0f && Time.time >= nextDodgeCheck)
            {
                TryDodgeIncomingFire(profile);
            }

            // Checked every frame rather than on the decision clock: by the time a slower tier re-planned, a
            // launched fighter had already fallen past the point where a second jump could still save it.
            if (profile.RecoversFromLaunch)
            {
                ApplyLaunchRecovery();
            }

            if (Time.time >= nextAimCheck)
            {
                nextAimCheck = Time.time + AimRefreshInterval;
                EvaluateFiringSolution();
            }

            bool reacted = hasFiringSolution && solutionHeldSince >= 0f &&
                Time.time - solutionHeldSince >= profile.ReactionDelay;

            if (reacted && Time.time >= nextFireBurst)
            {
                fireBurstEnds = Time.time + Random.Range(profile.BurstSeconds.x, profile.BurstSeconds.y);
                nextFireBurst = fireBurstEnds + Random.Range(profile.BurstPause.x, profile.BurstPause.y);
            }

            bool firing = reacted && Time.time < fireBurstEnds;
            motor.SetCommands(plannedMovement, jumpDecision, dropDecision, firing, targetDirection);
            jumpDecision = false;
            dropDecision = false;
        }

        /// <summary>
        /// Re-checks whether a shot would connect and advances the reaction timer. Public so the smoke test
        /// can drive it directly: verifying that a bot *moves* proved not to be enough, because the bot kept
        /// moving perfectly while never firing a single round.
        /// </summary>
        public bool EvaluateFiringSolution()
        {
            if (target == null)
            {
                hasFiringSolution = false;
                solutionHeldSince = -1f;
                return false;
            }

            BotProfile profile = BotProfile.Get(Difficulty);
            hasFiringSolution = HasFiringSolution(profile, target.position - transform.position);
            if (!hasFiringSolution) solutionHeldSince = -1f;
            else if (solutionHeldSince < 0f) solutionHeldSince = Time.time;
            return hasFiringSolution;
        }

        /// <summary>Test hook: when the current firing solution started holding, or -1 when there is none.</summary>
        public float SolutionHeldSince => solutionHeldSince;

        private void PlanMovement(in BotProfile profile, Vector3 offset, float targetDirection)
        {
            nextDecisionTime = Time.time + Random.Range(profile.DecisionInterval.x, profile.DecisionInterval.y);

            bool nearArenaEdge = Mathf.Abs(transform.position.x) > 9.5f;
            float distance = Mathf.Abs(offset.x);

            // Upgrading matters most on the base weapon or when nearly dry. Skilled tiers commit to the
            // detour more often, which is much of why they end up better armed than a distracted player.
            bool wantsWeapon = motor.WeaponId == PrototypeWeaponId.Carbine || (motor.Ammo >= 0 && motor.Ammo <= 2);
            desiredPickup = wantsWeapon ? WeaponPickup.FindNearestAvailable(transform.position, 7.5f) : null;
            bool pursuePickup = desiredPickup != null && Random.value < profile.PickupPursuit;

            if (pursuePickup && !nearArenaEdge)
            {
                Vector3 pickupOffset = desiredPickup.Position - transform.position;
                plannedMovement = Mathf.Abs(pickupOffset.x) > 0.5f ? Mathf.Sign(pickupOffset.x) : 0f;
                jumpDecision = pickupOffset.y > 1.1f && motor.IsGrounded;
                dropDecision = pickupOffset.y < -1.35f && motor.IsGrounded;
                nextDecisionTime += 0.15f;
            }
            else
            {
                // A disciplined bot fights at the distance its weapon is actually good at: it closes with a
                // scatter blaster and backs off with a sniper rather than trading at whatever range it is at.
                Vector2 band = Random.value < profile.RangeDiscipline
                    ? BotProfile.PreferredBand(motor.WeaponId)
                    : new Vector2(3.2f, 7.2f);

                if (nearArenaEdge) tactic = Tactic.EscapeEdge;
                else if (distance > band.y) tactic = Tactic.Approach;
                else if (distance < band.x) tactic = Tactic.Reposition;
                else tactic = Random.value < 0.62f ? Tactic.HoldRange : Tactic.Approach;

                plannedMovement = tactic switch
                {
                    Tactic.EscapeEdge => -Mathf.Sign(transform.position.x),
                    Tactic.Approach => targetDirection,
                    Tactic.Reposition => -targetDirection,
                    _ => Random.value < 0.55f ? 0f : -targetDirection * 0.45f
                };

                jumpDecision = (offset.y > 1.1f || nearArenaEdge || Random.value < profile.RandomJumpChance) &&
                    motor.IsGrounded;
                dropDecision = offset.y < -1.35f && motor.IsGrounded && Random.value < profile.DropThroughChance;
            }

            plannedMovement = RefuseStepIntoTheVoid(profile, plannedMovement);
        }

        /// <summary>
        /// Cancels a walk that would carry the bot off the end of its platform. Bots used to hand out free
        /// stocks by strolling into the pit while chasing, which reads as the AI being broken rather than
        /// easy. Only walking is guarded; a planned jump is still allowed to leave the ground.
        /// </summary>
        private float RefuseStepIntoTheVoid(in BotProfile profile, float movement)
        {
            if (Mathf.Approximately(movement, 0f) || jumpDecision || !motor.IsGrounded) return movement;
            if (Random.value > profile.EdgeSafety) return movement;

            Vector3 probe = transform.position + new Vector3(Mathf.Sign(movement) * 1.35f, 0.05f, 0f);
            bool groundAhead = Physics.Raycast(probe, Vector3.down, 2.6f, ~0, QueryTriggerInteraction.Ignore);
            return groundAhead ? movement : 0f;
        }

        /// <summary>
        /// Spends the air jump to climb back onto the deck after being launched. The motor caps the number of
        /// jumps, so asking for one every frame while falling cannot turn into flight.
        /// </summary>
        private void ApplyLaunchRecovery()
        {
            if (motor.IsGrounded) return;

            Vector3 position = transform.position;
            bool offDeck = Mathf.Abs(position.x) > DeckEdge || position.y < RecoveryFloor;
            if (!offDeck) return;

            plannedMovement = -Mathf.Sign(position.x);
            if (motor.PresentationVelocity.y < -1f) jumpDecision = true;
        }

        /// <summary>
        /// Jumps out of the path of a round already in flight. This is the single largest reason the upper
        /// tiers feel harder: the bot stops absorbing free chip damage while walking a straight line.
        /// </summary>
        private void TryDodgeIncomingFire(in BotProfile profile)
        {
            nextDodgeCheck = Time.time + 0.12f;
            if (!motor.IsGrounded) return;

            Vector3 position = transform.position;
            foreach (PrototypeProjectile shot in PrototypeProjectile.Active)
            {
                if (shot == null || shot.IsCosmetic || shot.Owner == self) continue;
                if (Mathf.Abs(shot.Direction.x) < 0.2f) continue;

                Vector3 toBot = position - shot.transform.position;
                if (Mathf.Sign(toBot.x) != Mathf.Sign(shot.Direction.x)) continue;
                if (Mathf.Abs(shot.transform.position.y - position.y) > 1.1f) continue;

                float distance = Mathf.Abs(toBot.x);
                if (distance > 9f || distance / Mathf.Max(1f, shot.Speed) > 0.42f) continue;

                // A missed read still burns the reaction window, so lower tiers simply eat the shot.
                if (Random.value > profile.DodgeChance)
                {
                    nextDodgeCheck = Time.time + 0.5f;
                    return;
                }

                jumpDecision = true;
                nextDodgeCheck = Time.time + 0.55f;
                return;
            }
        }

        /// <summary>
        /// Decides whether a shot would actually connect, instead of firing whenever the target is roughly
        /// level. Rounds travel horizontally, so aiming means judging where the target's height will be by
        /// the time the round arrives, whether the weapon reaches that far, and whether geometry is in the way.
        /// </summary>
        private bool HasFiringSolution(in BotProfile profile, Vector3 offset)
        {
            float distance = Mathf.Abs(offset.x);
            if (distance > BotProfile.EffectiveRange(motor.WeaponId)) return false;

            PrototypeWeaponProfile weapon = PrototypeWeaponProfile.Get(motor.WeaponId);
            float travelTime = distance / Mathf.Max(1f, weapon.ProjectileSpeed);
            float predictedRise = targetMotor != null
                ? targetMotor.PresentationVelocity.y * travelTime * profile.LeadAccuracy
                : 0f;

            float muzzleY = transform.position.y + 0.2f;
            float verticalError = Mathf.Abs(target.position.y + predictedRise - muzzleY);
            if (verticalError > profile.AimTolerance) return false;

            return !profile.UsesLineOfSight || HasLineOfSight(offset, muzzleY, distance);
        }

        private bool HasLineOfSight(Vector3 offset, float muzzleY, float distance)
        {
            float direction = offset.x >= 0f ? 1f : -1f;
            Vector3 origin = new(transform.position.x + direction * 0.9f, muzzleY, 0f);
            RaycastHit[] hits = Physics.RaycastAll(origin, new Vector3(direction, 0f, 0f), distance, ~0,
                QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == ownCollider || hit.collider.GetComponent<Fighter>() != null) continue;

                // Rounds pass up through a one-way platform exactly as fighters do, so a platform whose
                // surface sits above the shot line does not block the shot.
                OneWayPlatform oneWay = hit.collider.GetComponent<OneWayPlatform>();
                if (oneWay != null && muzzleY < oneWay.Top) continue;

                if (hit.distance < distance) return false;
            }

            return true;
        }
    }
}
