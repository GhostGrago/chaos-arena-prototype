using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace ChaosArena
{
    public enum SessionMode { Offline, Host, Client }

    /// <summary>
    /// Owns the online session: Unity Gaming Services sign-in, a Relay allocation for the host, and the join
    /// code clients type in. Relay is used rather than a direct IP because the host sits behind NAT, so a
    /// friend elsewhere could otherwise only connect through router port forwarding.
    /// </summary>
    public sealed class NetworkSession : MonoBehaviour
    {
        public const int MaxPlayers = 4;

        public static NetworkSession Instance { get; private set; }

        /// <summary>Registered network prefab the host spawns to carry replication. Assigned by the scene builder.</summary>
        public GameObject MatchPrefab;

        public SessionMode Mode { get; private set; } = SessionMode.Offline;
        public string JoinCode { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;
        public bool Busy { get; private set; }

        /// <summary>True when this process decides gameplay outcomes: offline play, or the host of a session.</summary>
        public bool HasAuthority => Mode != SessionMode.Client;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void PlayOffline()
        {
            Mode = SessionMode.Offline;
            JoinCode = string.Empty;
            Status = string.Empty;
        }

        public async void HostRelay()
        {
            if (Busy) return;
            Busy = true;
            Status = "Signing in...";

            try
            {
                await EnsureSignedIn();

                Status = "Allocating relay...";
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                RelayServerEndpoint endpoint = PickEndpoint(allocation.ServerEndpoints);
                GetTransport().SetRelayServerData(
                    endpoint.Host, (ushort)endpoint.Port,
                    allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData,
                    null, endpoint.Secure);

                if (!NetworkManager.Singleton.StartHost())
                {
                    throw new InvalidOperationException("StartHost failed.");
                }

                SpawnMatchObject();
                Mode = SessionMode.Host;
                Status = string.Empty;
            }
            catch (Exception error)
            {
                Mode = SessionMode.Offline;
                JoinCode = string.Empty;
                Status = Describe(error);
                Debug.LogWarning($"CHAOS_ARENA_HOST_FAILED: {error}");
            }
            finally
            {
                Busy = false;
            }
        }

        public async void JoinRelay(string code)
        {
            if (Busy) return;
            if (string.IsNullOrWhiteSpace(code))
            {
                Status = "Enter a room code first.";
                Debug.Log("CHAOS_NET_JOIN_EMPTY_CODE");
                return;
            }

            Debug.Log($"CHAOS_NET_JOIN_ATTEMPT code={code.Trim().ToUpperInvariant()}");

            Busy = true;
            Status = "Signing in...";

            try
            {
                await EnsureSignedIn();

                Status = "Joining room...";
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
                RelayServerEndpoint endpoint = PickEndpoint(allocation.ServerEndpoints);
                GetTransport().SetRelayServerData(
                    endpoint.Host, (ushort)endpoint.Port,
                    allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData,
                    allocation.HostConnectionData, endpoint.Secure);

                if (!NetworkManager.Singleton.StartClient())
                {
                    throw new InvalidOperationException("StartClient failed.");
                }

                Mode = SessionMode.Client;
                JoinCode = code.Trim().ToUpperInvariant();
                Status = string.Empty;
                Debug.Log("CHAOS_NET_JOIN_STARTED");
            }
            catch (Exception error)
            {
                Mode = SessionMode.Offline;
                Status = Describe(error);
                Debug.LogWarning($"CHAOS_ARENA_JOIN_FAILED: {error}");
            }
            finally
            {
                Busy = false;
            }
        }

        /// <summary>Only the host creates the replication object; clients receive it through the spawn message.</summary>
        private void SpawnMatchObject()
        {
            if (NetMatch.Instance != null) return;
            if (MatchPrefab == null)
            {
                throw new InvalidOperationException("Match prefab is not assigned; rebuild the prototype scene.");
            }

            GameObject instance = Instantiate(MatchPrefab);
            instance.GetComponent<NetworkObject>().Spawn();
        }

        public void Leave()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            Mode = SessionMode.Offline;
            JoinCode = string.Empty;
            Status = string.Empty;
        }

        private static async Task EnsureSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        /// <summary>
        /// Prefers the encrypted DTLS endpoint. This version of the Relay package ships no allocation-to-
        /// transport helper, so the endpoint is selected explicitly rather than assumed.
        /// </summary>
        private static RelayServerEndpoint PickEndpoint(List<RelayServerEndpoint> endpoints)
        {
            if (endpoints == null || endpoints.Count == 0)
            {
                throw new InvalidOperationException("Relay returned no server endpoints.");
            }

            return endpoints.Find(e => e.ConnectionType == "dtls") ?? endpoints[0];
        }

        private static UnityTransport GetTransport()
        {
            UnityTransport transport = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.GetComponent<UnityTransport>()
                : null;

            if (transport == null)
            {
                throw new InvalidOperationException("NetworkManager is missing a UnityTransport component.");
            }

            return transport;
        }

        /// <summary>
        /// Relay needs the Unity project to be linked to a cloud project, which is an account step the player
        /// has to do once. Surfacing that plainly saves a long hunt through a generic services error.
        /// </summary>
        private static string Describe(Exception error)
        {
            string message = error.Message ?? "Unknown error";
            if (message.Contains("project", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unauthor", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("environment", StringComparison.OrdinalIgnoreCase))
            {
                return "Online unavailable: link this project to a Unity Cloud project first.";
            }

            return "Online failed: " + message;
        }
    }
}
