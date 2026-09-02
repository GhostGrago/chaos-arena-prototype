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

        // Ledge recovery. Being launched off the stage used to be unrecoverable; this gives one clear,
        // learnable save. Hanging is time-limited and has a cooldown so an edge cannot be camped.
        private const float LedgeReach = 0.62f;
        private const float LedgeGrabDepth = 1.1f;
        private const float MaxHangSeconds = 1.25f;
        private const float RegrabCooldown = 0.55f;
        private bool hanging;
        private float hangEndsAt;
        private float nextGrabAllowedAt;
        private Vector3 hangPosition;

        public int Facing => facing;
        public bool IsGrounded { get; private set; }
        public bool IsHanging => hanging;
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

            if (UpdateLedgeHang()) return;

            if (dropQueued && currentSupport != null)
            {
                droppingThrough = currentSupport;
                dropTimeout = Time.time + 0.45f;
                Physics.IgnoreCollision(ownCollider, droppingThrough.PlatformCollider, true);
                body.linearVelocity = new Vector3(body.linearVelocity.x, Mathf.Min(body.linearVelocity.y, -2.2f), 0f);
                IsGrounded = false;
            }
            dropQueued = false;

            float targetVelocity = moveInput * moveSpeed;
            float acceleration = IsGrounded ? groundAcceleration : airAcceleration;
            float newX = Mathf.MoveTowards(body.linearVelocity.x, targetVelocity, acceleration * Time.fixedDeltaTime);
            body.linearVelocity = new Vector3(newX, body.linearVelocity.y, 0f);

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
                body.AddForce(Vector3.left * (facing * weapon.ShooterRecoil), ForceMode.VelocityChange);
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

        /// <summary>
        /// Runs the hang state and looks for a new grab. Returns true while hanging, which suspends normal
        /// movement, gravity and firing for that step.
        /// </summary>
        private bool UpdateLedgeHang()
        {
            if (hanging)
            {
                body.linearVelocity = Vector3.zero;
                transform.position = hangPosition;

                // Jump climbs off the ledge; dropping down or running out of time lets go.
                if (jumpQueued)
                {
                    ReleaseLedge();
                    body.linearVelocity = new Vector3(body.linearVelocity.x, jumpSpeed, 0f);
                    jumpsUsed = 1;
                    jumpQueued = false;
                    return true;
                }

                if (dropQueued || Time.time >= hangEndsAt)
                {
                    ReleaseLedge();
                    dropQueued = false;
                    return true;
                }

                jumpQueued = false;
                dropQueued = false;
                return true;
            }

            if (IsGrounded || Time.time < nextGrabAllowedAt || body.linearVelocity.y > 0.2f) return false;

            if (!TryFindLedge(transform.position, ownCollider.bounds.extents.y, out Vector3 grabPosition, out int grabSide))
            {
                return false;
            }

            hanging = true;
            hangEndsAt = Time.time + MaxHangSeconds;
            hangPosition = grabPosition;
            transform.position = grabPosition;
            body.linearVelocity = Vector3.zero;
            jumpsUsed = 0;
            facing = -grabSide;
            return true;
        }

        /// <summary>
        /// Pure ledge lookup, kept free of component state so the smoke test can verify the grab window
        /// without running physics. Returns the hang position and which side of the platform was caught.
        /// </summary>
        public static bool TryFindLedge(Vector3 position, float halfHeight, out Vector3 hangPosition, out int side)
        {
            foreach (ArenaBuilder.PlatformDefinition platform in ArenaBuilder.Layout)
            {
                float top = platform.Top;
                float head = position.y + halfHeight;

                // The grab window sits just under the lip: head at or above it, body still below.
                if (head < top - 0.15f || head > top + LedgeGrabDepth) continue;

                float halfWidth = platform.Scale.x * 0.5f;
                foreach (int candidate in StaticSides)
                {
                    float edgeX = platform.Position.x + candidate * halfWidth;
                    float outward = (position.x - edgeX) * candidate;

                    // Only from outside the platform, and only within arm's reach of the lip.
                    if (outward < 0f || outward > LedgeReach) continue;

                    hangPosition = new Vector3(edgeX + candidate * 0.34f, top - halfHeight + 0.28f, 0f);
                    side = candidate;
                    return true;
                }
            }

            hangPosition = Vector3.zero;
            side = 0;
            return false;
        }

        private void ReleaseLedge()
        {
            hanging = false;
            nextGrabAllowedAt = Time.time + RegrabCooldown;
        }

        private static readonly int[] StaticSides = { -1, 1 };

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
