using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ChaosArena
{
    /// <summary>Input a client sends to the host each frame.</summary>
    public struct NetInput : INetworkSerializable
    {
        public float Horizontal;
        public bool Fire;
        // Jump and drop are edge triggered. Sending counters instead of booleans means a dropped packet
        // delays a jump rather than losing it entirely.
        public ushort JumpCount;
        public ushort DropCount;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Horizontal);
            serializer.SerializeValue(ref Fire);
            serializer.SerializeValue(ref JumpCount);
            serializer.SerializeValue(ref DropCount);
        }
    }

    /// <summary>One fighter's authoritative state, broadcast by the host.</summary>
    public struct NetFighterState : INetworkSerializable
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Health;
        public float ProtectionRemaining;
        public byte Lives;
        public byte Weapon;
        public short Ammo;
        public sbyte Facing;
        public bool Active;
        public bool Grounded;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref ProtectionRemaining);
            serializer.SerializeValue(ref Lives);
            serializer.SerializeValue(ref Weapon);
            serializer.SerializeValue(ref Ammo);
            serializer.SerializeValue(ref Facing);
            serializer.SerializeValue(ref Active);
            serializer.SerializeValue(ref Grounded);
        }
    }

    /// <summary>Match-wide state clients need to show the same result screen as the host.</summary>
    public struct NetMatchState : INetworkSerializable
    {
        public bool Ended;
        public sbyte WinnerSeat;
        public float Duration;
        public float RestartIn;
        public byte PickupMask;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Ended);
            serializer.SerializeValue(ref WinnerSeat);
            serializer.SerializeValue(ref Duration);
            serializer.SerializeValue(ref RestartIn);
            serializer.SerializeValue(ref PickupMask);
        }
    }

    /// <summary>
    /// Host-authoritative replication for the whole match. The arena and fighters are built at runtime on
    /// every peer, so instead of networking each fighter as its own object this single scene object carries
    /// all state: clients push input up and receive fighter and match state back.
    ///
    /// There is deliberately no client-side prediction yet, so a client sees its own movement after one
    /// round trip. GAME_VISION lists prediction as later work; this is the first proof that the loop runs.
    /// </summary>
    public sealed class NetMatch : NetworkBehaviour
    {
        public const int MaxFighters = 4;
        private const float SendInterval = 1f / 25f;

        public static NetMatch Instance { get; private set; }

        /// <summary>Fighter index owned by each connected client. Index 0 is always the host.</summary>
        private readonly Dictionary<ulong, int> seatByClient = new();
        private readonly NetInput[] latestInput = new NetInput[MaxFighters];
        private readonly ushort[] appliedJump = new ushort[MaxFighters];
        private readonly ushort[] appliedDrop = new ushort[MaxFighters];

        private NetFighterState[] received = new NetFighterState[MaxFighters];
        private float nextSendTime;
        private ushort localJumpCount;
        private ushort localDropCount;

        public int LocalSeat { get; private set; }
        public int HumanSeats { get; private set; } = 1;
        public bool HasNetworkState { get; private set; }
        public NetMatchState MatchState { get; private set; }

        public NetFighterState GetState(int index) => received[index];

        private void Awake()
        {
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                seatByClient.Clear();
                seatByClient[NetworkManager.ServerClientId] = 0;
                AssignSeats();
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }

            LocalSeat = IsServer ? 0 : -1;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            HasNetworkState = false;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!seatByClient.ContainsKey(clientId) && seatByClient.Count < MaxFighters)
            {
                seatByClient[clientId] = seatByClient.Count;
            }

            AssignSeats();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            seatByClient.Remove(clientId);
            AssignSeats();
        }

        /// <summary>Tells every client which fighter it drives and how many seats humans occupy.</summary>
        private void AssignSeats()
        {
            HumanSeats = Mathf.Max(1, seatByClient.Count);
            foreach (KeyValuePair<ulong, int> pair in seatByClient)
            {
                AssignSeatClientRpc(pair.Value, HumanSeats, RpcTarget.Single(pair.Key, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void AssignSeatClientRpc(int seat, int humanSeats, RpcParams rpcParams = default)
        {
            LocalSeat = seat;
            HumanSeats = humanSeats;
        }

        /// <summary>Client input for the fighter that client owns.</summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        public void SubmitInputRpc(NetInput input, RpcParams rpcParams = default)
        {
            if (!seatByClient.TryGetValue(rpcParams.Receive.SenderClientId, out int seat)) return;
            if (seat > 0 && seat < MaxFighters) latestInput[seat] = input;
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void BroadcastStateRpc(NetFighterState[] states, int humanSeats, NetMatchState matchState)
        {
            received = states;
            HumanSeats = humanSeats;
            MatchState = matchState;
            HasNetworkState = true;
        }

        /// <summary>Called by the host once per frame with the live fighter list.</summary>
        public void HostBroadcast(IReadOnlyList<Fighter> fighters, NetMatchState matchState)
        {
            if (!IsServer || Time.time < nextSendTime) return;
            nextSendTime = Time.time + SendInterval;

            NetFighterState[] states = new NetFighterState[MaxFighters];
            for (int i = 0; i < MaxFighters && i < fighters.Count; i++)
            {
                Fighter fighter = fighters[i];
                FighterMotor motor = fighter.GetComponent<FighterMotor>();
                Rigidbody body = fighter.GetComponent<Rigidbody>();
                states[i] = new NetFighterState
                {
                    Position = fighter.transform.position,
                    Velocity = body != null ? body.linearVelocity : Vector3.zero,
                    Health = fighter.Health,
                    ProtectionRemaining = fighter.ProtectionRemaining,
                    Lives = (byte)Mathf.Clamp(fighter.Lives, 0, 255),
                    Weapon = (byte)motor.WeaponId,
                    Ammo = (short)Mathf.Clamp(motor.Ammo, short.MinValue, short.MaxValue),
                    Facing = (sbyte)motor.Facing,
                    Active = fighter.gameObject.activeSelf,
                    Grounded = motor.IsGrounded
                };
            }

            BroadcastStateRpc(states, HumanSeats, matchState);
        }

        /// <summary>Reads the host's view of a remote client's input, for seats the host does not drive.</summary>
        public NetInput ConsumeInput(int seat, out bool jump, out bool drop)
        {
            jump = false;
            drop = false;
            if (seat <= 0 || seat >= MaxFighters) return default;

            NetInput input = latestInput[seat];
            if (input.JumpCount != appliedJump[seat])
            {
                appliedJump[seat] = input.JumpCount;
                jump = true;
            }

            if (input.DropCount != appliedDrop[seat])
            {
                appliedDrop[seat] = input.DropCount;
                drop = true;
            }

            return input;
        }

        /// <summary>
        /// Host tells everyone a shot was fired. Clients spawn a cosmetic projectile from this; damage and
        /// hit detection stay on the host, so these carry no authority.
        /// </summary>
        public void BroadcastShot(int seat, PrototypeWeaponId weapon, Vector3 muzzle, Vector3 direction)
        {
            if (!IsServer || !IsSpawned) return;
            ShotFiredRpc(seat, (byte)weapon, muzzle, direction);
        }

        [Rpc(SendTo.NotServer)]
        private void ShotFiredRpc(int seat, byte weapon, Vector3 muzzle, Vector3 direction)
        {
            OnRemoteShot?.Invoke(seat, (PrototypeWeaponId)weapon, muzzle, direction);
        }

        /// <summary>Raised on clients when the host reports a shot. Bootstrap builds the visual round.</summary>
        public event System.Action<int, PrototypeWeaponId, Vector3, Vector3> OnRemoteShot;

        /// <summary>Sends this client's own input up to the host.</summary>
        public void SubmitLocalInput(float horizontal, bool jumpPressed, bool dropPressed, bool fire)
        {
            if (IsServer || !IsSpawned) return;
            if (jumpPressed) localJumpCount++;
            if (dropPressed) localDropCount++;

            SubmitInputRpc(new NetInput
            {
                Horizontal = horizontal,
                Fire = fire,
                JumpCount = localJumpCount,
                DropCount = localDropCount
            });
        }
    }
}
