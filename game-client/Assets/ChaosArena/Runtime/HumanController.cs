using UnityEngine;
using UnityEngine.InputSystem;

namespace ChaosArena
{
    public enum LocalInputSlot { PlayerOne, PlayerTwo, PlayerThree, PlayerFour }

    /// <summary>
    /// Reads one local input slot. Player one uses keyboard; players two and three use distinct Gamepad
    /// devices from the Input System. P2 keeps an alternate keyboard layout for controller-free testing.
    /// </summary>
    [RequireComponent(typeof(FighterMotor))]
    public sealed class HumanController : MonoBehaviour
    {
        private FighterMotor motor;
        private LocalInputSlot inputSlot;
        private bool secondPlayerDownHeld;

        public LocalInputSlot InputSlot => inputSlot;

        public void Configure(LocalInputSlot slot)
        {
            inputSlot = slot;
        }

        public static int ConnectedControllerCount => Gamepad.all.Count;
        public static bool ControllerConnected => ConnectedControllerCount > 0;

        public static string ControllerName(int controllerIndex)
        {
            return controllerIndex >= 0 && controllerIndex < Gamepad.all.Count
                ? Gamepad.all[controllerIndex].displayName
                : "NOT CONNECTED";
        }

        /// <summary>Gamepad index of a pad asking to join the lobby this frame, or -1 for none.</summary>
        public static int GamepadRequestingJoin()
        {
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad pad = Gamepad.all[i];
                if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) return i;
            }

            return -1;
        }

        /// <summary>Which gamepad drives a seat. Seat 0 is the keyboard, so pads start at seat 1.</summary>
        public static int GamepadIndexForSlot(LocalInputSlot slot) => slot switch
        {
            LocalInputSlot.PlayerTwo => 0,
            LocalInputSlot.PlayerThree => 1,
            LocalInputSlot.PlayerFour => 2,
            _ => -1
        };

        private void Awake()
        {
            motor = GetComponent<FighterMotor>();
        }

        private void Update()
        {
            float horizontal;
            bool jump;
            bool drop;
            bool fire;

            if (inputSlot != LocalInputSlot.PlayerOne)
            {
                int controllerIndex = GamepadIndexForSlot(inputSlot);
                Gamepad gamepad = controllerIndex >= 0 && controllerIndex < Gamepad.all.Count
                    ? Gamepad.all[controllerIndex] : null;
                float stick = gamepad != null ? gamepad.leftStick.x.ReadValue() : 0f;
                horizontal = Mathf.Abs(stick) >= 0.18f ? stick : 0f;
                bool keyboardFallback = inputSlot == LocalInputSlot.PlayerTwo;
                if (keyboardFallback && Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
                if (keyboardFallback && Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;

                bool stickDown = gamepad != null && gamepad.leftStick.y.ReadValue() < -0.65f;
                jump = (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) ||
                       (keyboardFallback && Input.GetKeyDown(KeyCode.UpArrow));
                drop = (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) ||
                       (keyboardFallback && Input.GetKeyDown(KeyCode.DownArrow)) ||
                       (stickDown && !secondPlayerDownHeld);
                fire = (gamepad != null &&
                        (gamepad.leftTrigger.ReadValue() > 0.35f || gamepad.rightTrigger.ReadValue() > 0.35f ||
                         gamepad.buttonWest.isPressed || gamepad.rightShoulder.isPressed)) ||
                       (keyboardFallback && (Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.Keypad0)));
                secondPlayerDownHeld = stickDown;
            }
            else
            {
                horizontal = 0f;
                if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
                if (Input.GetKey(KeyCode.D)) horizontal += 1f;
                jump = Input.GetKeyDown(KeyCode.Space);
                drop = Input.GetKeyDown(KeyCode.S);
                fire = Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.LeftControl);
            }

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
