using UnityEngine;

namespace ChaosArena
{
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
            motor.SetCommands(horizontal, jump, drop, fire);
        }
    }
}
