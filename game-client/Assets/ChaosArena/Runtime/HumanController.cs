using UnityEngine;

namespace ChaosArena
{
    /// <summary>
    /// Reads the keyboard for the local player. Offline and on the host the input drives the motor directly;
    /// on a client it is sent to the host instead, because the host is authoritative over movement.
    /// </summary>
    [RequireComponent(typeof(FighterMotor))]
    public sealed class HumanController : MonoBehaviour
    {
        private FighterMotor motor;

        private void Awake()
        {
            motor = GetComponent<FighterMotor>();
        }

        private void Update()
        {
            float horizontal = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;

            bool jump = Input.GetKeyDown(KeyCode.Space);
            bool drop = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            bool fire = Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.LeftControl);

            NetMatch net = NetMatch.Instance;
            bool isRemoteClient = net != null && net.IsSpawned && !net.IsServer;
            if (isRemoteClient)
            {
                net.SubmitLocalInput(horizontal, jump, drop, fire);
                return;
            }

            motor.SetCommands(horizontal, jump, drop, fire);
        }
    }
}
