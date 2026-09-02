using UnityEngine;

namespace ChaosArena
{
    public enum BotDifficulty { Easy, Normal, Hard }

    [RequireComponent(typeof(FighterMotor))]
    public sealed class BotController : MonoBehaviour
    {
        private enum Tactic { Approach, HoldRange, Reposition, EscapeEdge }

        private FighterMotor motor;
        private Transform target;
        private float nextDecisionTime;
        private bool jumpDecision;
        private bool dropDecision;
        private float fireBurstEnds;
        private float nextFireBurst;
        private float plannedMovement;
        private Tactic tactic;
        private WeaponPickup desiredPickup;

        public BotDifficulty Difficulty { get; private set; } = BotDifficulty.Easy;

        public void SetDifficulty(BotDifficulty difficulty)
        {
            Difficulty = difficulty;
            nextDecisionTime = 0f;
            fireBurstEnds = 0f;
            nextFireBurst = 0f;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void Awake()
        {
            motor = GetComponent<FighterMotor>();
        }

        private void Update()
        {
            if (target == null)
            {
                motor.SetCommands(0f, false, false, false);
                return;
            }

            Vector3 offset = target.position - transform.position;
            float targetDirection = Mathf.Sign(offset.x);

            if (Time.time >= nextDecisionTime)
            {
                Vector2 interval = Difficulty switch
                {
                    BotDifficulty.Easy => new Vector2(0.45f, 0.75f),
                    BotDifficulty.Normal => new Vector2(0.3f, 0.52f),
                    _ => new Vector2(0.16f, 0.32f)
                };
                nextDecisionTime = Time.time + Random.Range(interval.x, interval.y);
                bool nearArenaEdge = Mathf.Abs(transform.position.x) > 9.5f;
                float distance = Mathf.Abs(offset.x);
                desiredPickup = motor.WeaponId == PrototypeWeaponId.Carbine
                    ? WeaponPickup.FindNearestAvailable(transform.position, 7.5f) : null;
                bool pursuePickup = desiredPickup != null &&
                    Random.value < (Difficulty == BotDifficulty.Easy ? 0.45f : 0.72f);

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
                    if (nearArenaEdge) tactic = Tactic.EscapeEdge;
                    else if (distance > 7.2f) tactic = Tactic.Approach;
                    else if (distance < 3.2f) tactic = Tactic.Reposition;
                    else tactic = Random.value < 0.62f ? Tactic.HoldRange : Tactic.Approach;

                    plannedMovement = tactic switch
                    {
                        Tactic.EscapeEdge => -Mathf.Sign(transform.position.x),
                        Tactic.Approach => targetDirection,
                        Tactic.Reposition => -targetDirection,
                        _ => Random.value < 0.55f ? 0f : -targetDirection * 0.45f
                    };
                    float randomJump = Difficulty == BotDifficulty.Easy ? 0.08f : 0.14f;
                    jumpDecision = (offset.y > 1.1f || nearArenaEdge || Random.value < randomJump) && motor.IsGrounded;
                    dropDecision = offset.y < -1.35f && motor.IsGrounded && Random.value <
                        (Difficulty == BotDifficulty.Easy ? 0.25f : Difficulty == BotDifficulty.Normal ? 0.55f : 0.78f);
                }
            }

            bool clearShot = Mathf.Abs(offset.y) < 1.5f && Mathf.Abs(offset.x) < 13f;
            if (clearShot && Time.time >= nextFireBurst)
            {
                float burst = Difficulty == BotDifficulty.Easy ? Random.Range(0.58f, 0.86f) :
                    Difficulty == BotDifficulty.Normal ? Random.Range(0.7f, 1.02f) : Random.Range(0.9f, 1.25f);
                float pause = Difficulty == BotDifficulty.Easy ? Random.Range(0.7f, 1.1f) :
                    Difficulty == BotDifficulty.Normal ? Random.Range(0.45f, 0.8f) : Random.Range(0.2f, 0.42f);
                fireBurstEnds = Time.time + burst;
                nextFireBurst = fireBurstEnds + pause;
            }

            bool firing = clearShot && Time.time < fireBurstEnds;
            motor.SetCommands(plannedMovement, jumpDecision, dropDecision, firing, targetDirection);
            jumpDecision = false;
            dropDecision = false;
        }
    }
}
