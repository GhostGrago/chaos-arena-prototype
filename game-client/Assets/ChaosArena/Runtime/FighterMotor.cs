using UnityEngine;

namespace ChaosArena
{
    [RequireComponent(typeof(Rigidbody), typeof(Fighter))]
    public sealed class FighterMotor : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float groundAcceleration = 35f;
        [SerializeField] private float airAcceleration = 14f;
        [SerializeField] private float jumpSpeed = 10.8f;
        [SerializeField] private int maxJumps = 2;
        private Rigidbody body;
        private Collider ownCollider;
        private Fighter fighter;
        private float moveInput;
        private bool jumpQueued;
        private bool dropQueued;
        private bool fireHeld;
        private float nextFireTime;
        private int jumpsUsed;
        private int facing = 1;
        private OneWayPlatform currentSupport;
        private OneWayPlatform droppingThrough;
        private float dropTimeout;
        private PrototypeWeaponProfile weapon = PrototypeWeaponProfile.Carbine;
        private int ammo = -1;

        /// <summary>Roster slot this fighter occupies; used to attribute networked shots.</summary>
        public int SeatIndex { get; set; }

        // Hitstun. The motor overwrites horizontal velocity every physics step, so without a control lock the
        // movement code cancels an incoming knockback within about 0.09s on the ground and the hit reads as
        // having no effect at all. During the lock the fighter keeps its momentum and only light drag applies.
        private float controlLockUntil;
        private const float GroundKnockbackDrag = 5f;
        private const float AirKnockbackDrag = 1.2f;
        private const float ShooterRecoilVelocityScale = 4f;

        public int Facing => facing;
        public bool IsGrounded { get; private set; }

        // On a client the motor does not simulate, so the visual layer reads these instead of the rigidbody,
        // which is kinematic and therefore reports no velocity at all.
        private Vector3 remoteVelocity;
        private bool usingRemoteState;
        public Vector3 PresentationVelocity => usingRemoteState ? remoteVelocity : body.linearVelocity;

        /// <summary>Applies host-authoritative presentation state on a client.</summary>
        public void ApplyRemoteState(Vector3 velocity, int newFacing, bool grounded, PrototypeWeaponId weaponId, int remoteAmmo)
        {
            usingRemoteState = true;
            remoteVelocity = velocity;
            if (newFacing != 0) facing = newFacing;
            IsGrounded = grounded;
            if (weapon.Id != weaponId) weapon = PrototypeWeaponProfile.Get(weaponId);
            ammo = remoteAmmo;
        }
        public bool InKnockback => Time.time < controlLockUntil;

        /// <summary>Suspends movement control so an applied knockback impulse actually survives.</summary>
        public void ApplyKnockbackStun(float seconds)
        {
            controlLockUntil = Mathf.Max(controlLockUntil, Time.time + seconds);
        }

        /// <summary>
        /// Converts the weapon's small recoil tuning value into a brief physical shove. The short control lock
        /// prevents normal movement acceleration from erasing that shove on the very next physics step.
        /// </summary>
        public void ApplyShooterRecoil(PrototypeWeaponProfile profile)
        {
            float velocityChange = profile.ShooterRecoil * ShooterRecoilVelocityScale;
            body.linearVelocity = new Vector3(
                body.linearVelocity.x - facing * velocityChange,
                body.linearVelocity.y,
                0f);
            float momentumWindow = Mathf.Lerp(0.055f, 0.12f, Mathf.Clamp01(profile.ShooterRecoil / 1.25f));
            ApplyKnockbackStun(momentumWindow);
        }
        public PrototypeWeaponId WeaponId => weapon.Id;
        public string WeaponName => weapon.Name;
        public int Ammo => ammo;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownCollider = GetComponent<Collider>();
            fighter = GetComponent<Fighter>();
        }

        public void SetCommands(float horizontal, bool jumpPressed, bool dropPressed, bool wantsFire, float aimHorizontal = 0f)
        {
            moveInput = Mathf.Clamp(horizontal, -1f, 1f);
            jumpQueued |= jumpPressed;
            dropQueued |= dropPressed;
            fireHeld = wantsFire;
            float facingInput = Mathf.Abs(aimHorizontal) > 0.05f ? aimHorizontal : moveInput;
            if (Mathf.Abs(facingInput) > 0.05f)
            {
                facing = facingInput > 0f ? 1 : -1;
            }
        }

        public void Equip(PrototypeWeaponProfile profile)
        {
            weapon = profile;
            ammo = profile.PickupAmmo;
            nextFireTime = Mathf.Min(nextFireTime, Time.time + 0.12f);
        }

        public void ResetWeapon()
        {
            weapon = PrototypeWeaponProfile.Carbine;
            ammo = -1;
        }

        private void FixedUpdate()
        {
            UpdateOneWayCollisions();
            IsGrounded = CheckGrounded();
            if (IsGrounded && body.linearVelocity.y <= 0.5f)
            {
                jumpsUsed = 0;
            }

            if (dropQueued && currentSupport != null)
            {
                droppingThrough = currentSupport;
                dropTimeout = Time.time + 0.45f;
                Physics.IgnoreCollision(ownCollider, droppingThrough.PlatformCollider, true);
                body.linearVelocity = new Vector3(body.linearVelocity.x, Mathf.Min(body.linearVelocity.y, -2.2f), 0f);
                IsGrounded = false;
            }
            dropQueued = false;

            if (InKnockback)
            {
                // Momentum is preserved while stunned; only light drag bleeds it off so the fighter still settles.
                float drag = IsGrounded ? GroundKnockbackDrag : AirKnockbackDrag;
                float decayed = Mathf.MoveTowards(body.linearVelocity.x, 0f, drag * Time.fixedDeltaTime);
                body.linearVelocity = new Vector3(decayed, body.linearVelocity.y, 0f);
            }
            else
            {
                float targetVelocity = moveInput * moveSpeed;
                float acceleration = IsGrounded ? groundAcceleration : airAcceleration;
                float newX = Mathf.MoveTowards(body.linearVelocity.x, targetVelocity, acceleration * Time.fixedDeltaTime);
                body.linearVelocity = new Vector3(newX, body.linearVelocity.y, 0f);
            }

            ApplyAirGravityTuning();

            if (jumpQueued && jumpsUsed < maxJumps)
            {
                body.linearVelocity = new Vector3(body.linearVelocity.x, jumpSpeed, 0f);
                jumpsUsed++;
            }

            jumpQueued = false;

            if (fireHeld && Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + weapon.FireCooldown;
                Vector3 muzzle = transform.position + new Vector3(facing * 0.85f, 0.2f, 0f);
                int projectileCount = Mathf.Max(1, weapon.ProjectilesPerShot);
                for (int i = 0; i < projectileCount; i++)
                {
                    float t = projectileCount == 1 ? 0.5f : i / (float)(projectileCount - 1);
                    float angle = Mathf.Lerp(-weapon.SpreadDegrees * 0.5f, weapon.SpreadDegrees * 0.5f, t);
                    Vector3 shotDirection = Quaternion.Euler(0f, 0f, angle * facing) * Vector3.right * facing;
                    PrototypeProjectile.Spawn(fighter, muzzle, shotDirection, weapon);
                }
                // Clients cannot see host-side firing, so the shot is announced for cosmetic replication.
                if (NetMatch.Instance != null && NetMatch.Instance.IsServer)
                {
                    NetMatch.Instance.BroadcastShot(SeatIndex, weapon.Id, muzzle, new Vector3(facing, 0f, 0f));
                }

                ApplyShooterRecoil(weapon);
                GetComponent<FighterVisual>()?.OnFire(weapon.VisualRecoil);
                CombatVfx.Muzzle(muzzle, facing, weapon.ProjectileColor);
                PrototypeAudio.PlayShot(muzzle);

                if (!weapon.IsBaseWeapon)
                {
                    ammo--;
                    if (ammo <= 0) ResetWeapon();
                }
            }
        }

        private void ApplyAirGravityTuning()
        {
            if (IsGrounded) return;

            float verticalSpeed = body.linearVelocity.y;
            float gravityScale = verticalSpeed > 1.5f ? 0.82f :
                verticalSpeed > -1.5f ? 0.55f : 1.05f;
            Vector3 compensation = Physics.gravity * (gravityScale - 1f);
            body.AddForce(compensation, ForceMode.Acceleration);
        }

        private void UpdateOneWayCollisions()
        {
            float feet = ownCollider.bounds.min.y;
            foreach (OneWayPlatform platform in OneWayPlatform.ActivePlatforms)
            {
                if (platform == null || platform.PlatformCollider == null) continue;
                bool activeDrop = platform == droppingThrough;
                if (activeDrop && (Time.time >= dropTimeout || ownCollider.bounds.max.y < platform.Top - 0.03f))
                {
                    droppingThrough = null;
                    activeDrop = false;
                }

                bool belowTop = feet < platform.Top + 0.06f;
                bool ascendingThrough = body.linearVelocity.y > 0.05f && belowTop;
                bool centerBelowTop = ownCollider.bounds.center.y < platform.Top && belowTop;
                Physics.IgnoreCollision(ownCollider, platform.PlatformCollider, activeDrop || ascendingThrough || centerBelowTop);
            }
        }

        private bool CheckGrounded()
        {
            currentSupport = null;
            Vector3 origin = ownCollider.bounds.center;
            float distance = ownCollider.bounds.extents.y + 0.16f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != ownCollider && !hit.collider.isTrigger && !Physics.GetIgnoreCollision(ownCollider, hit.collider))
                {
                    currentSupport = hit.collider.GetComponent<OneWayPlatform>();
                    return true;
                }
            }

            return false;
        }
    }
}
