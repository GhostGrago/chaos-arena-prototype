using System.Collections.Generic;
using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// Decides when and where weapons appear. Fixed pickup points made every match play the same way: the
    /// same weapon was always in the same corner and both fighters ran the same route. Drops are now random
    /// in time, place and type, and a collected weapon is consumed rather than respawning where it was taken.
    ///
    /// Only the host runs this; clients receive the resulting slot states.
    /// </summary>
    public sealed class PickupDirector : MonoBehaviour
    {
        public const int MaxSlots = 3;

        private const float FirstDropDelay = 3f;
        private const float MinInterval = 5.5f;
        private const float MaxInterval = 9.5f;
        private const float DropHeight = 0.75f;

        /// <summary>Share of drops that are power-ups. A minority, so weapons stay the main contested prize.</summary>
        private const float PowerUpShare = 0.34f;

        private static readonly PrototypeWeaponId[] DroppableWeapons =
        {
            PrototypeWeaponId.PulseSmg,
            PrototypeWeaponId.ScatterBlaster,
            PrototypeWeaponId.Sniper
        };

        private readonly List<WeaponPickup> slots = new();
        private float nextDropTime;

        public IReadOnlyList<WeaponPickup> Slots => slots;

        private void Awake()
        {
            for (int i = 0; i < MaxSlots; i++) slots.Add(WeaponPickup.CreateSlot());
            ResetCycle();
        }

        /// <summary>Clears the field and restarts the drop schedule for a fresh match.</summary>
        public void ResetCycle()
        {
            foreach (WeaponPickup slot in slots)
            {
                if (slot != null) slot.SetAvailableState(false);
            }

            nextDropTime = Time.time + FirstDropDelay;
        }

        /// <summary>Host-side tick. Drops a weapon whenever the schedule comes due and a slot is free.</summary>
        public void HostTick()
        {
            if (Time.time < nextDropTime) return;

            WeaponPickup free = null;
            foreach (WeaponPickup slot in slots)
            {
                if (slot != null && !slot.IsAvailable) { free = slot; break; }
            }

            // Every slot is occupied, so wait rather than stacking drops on top of each other.
            if (free == null)
            {
                nextDropTime = Time.time + MinInterval;
                return;
            }

            Vector3 point = PickDropPoint();
            Color flash;

            if (Random.value < PowerUpShare)
            {
                PowerUpKind kind = PowerUp.All[Random.Range(0, PowerUp.All.Length)];
                free.ConfigurePowerUp(kind, point);
                flash = PowerUp.Tint(kind);
            }
            else
            {
                PrototypeWeaponId weapon = DroppableWeapons[Random.Range(0, DroppableWeapons.Length)];
                free.Configure(PrototypeWeaponProfile.Get(weapon), point);
                flash = PrototypeWeaponProfile.Get(weapon).ProjectileColor;
            }

            free.SetAvailableState(true);
            CombatVfx.Impact(free.Position, 1, flash, true);

            nextDropTime = Time.time + Random.Range(MinInterval, MaxInterval);
        }

        /// <summary>
        /// Picks a spot above a random platform, inset from the edges so a drop never lands where it would
        /// immediately fall off or sit out of reach.
        /// </summary>
        private static Vector3 PickDropPoint()
        {
            ArenaBuilder.PlatformDefinition platform =
                ArenaBuilder.Layout[Random.Range(0, ArenaBuilder.Layout.Length)];

            float inset = Mathf.Min(1.2f, platform.Scale.x * 0.3f);
            float half = Mathf.Max(0.1f, platform.Scale.x * 0.5f - inset);
            float x = platform.Position.x + Random.Range(-half, half);
            return new Vector3(x, platform.Top + DropHeight, 0f);
        }
    }
}
