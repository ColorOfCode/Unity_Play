using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SF_Studio.Shared.PlayerController {
    /// <summary>
    /// player controller inspired by the unity docs: https://docs.unity3d.com/ScriptReference/CharacterController.Move.html
    /// Supports both, old and new input system
    /// </summary>
   [RequireComponent(typeof(CharacterController))]
    public class SharedPlayerController : MonoBehaviour {
        public Camera cam;
        public float speed = 10;
        public float jumpHeight = 1.0f;
        public float mouseSpeed = 5;

        public bool rotateOnlyOnRightMouseHold = true;
        private CharacterController _controller;
        public float viewDirection;

        public CursorLockMode cursorLockMode = CursorLockMode.None;

        private const float Gravity = 9.81f;
        private float _jumpForce;
        private Vector3 _playerVelocity;
        private bool _grounded;

        private void Start() {
            Cursor.lockState = cursorLockMode;

            _controller = GetComponent<CharacterController>();
            viewDirection = transform.eulerAngles.y;
            _jumpForce = Mathf.Sqrt(jumpHeight * 3.0f * Gravity);
        }

        private void Update() {
            if (Time.timeScale == 0) {
                return;
            }

            _grounded = _controller.isGrounded;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current.spaceKey.isPressed && _grounded) {
                // apply jump force to vertical velocity
                _playerVelocity.y = _jumpForce;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space) && _grounded) {
                // apply jump force to vertical velocity
                _playerVelocity.y = _jumpForce;
            }
#endif

            // use TransformDirection to make sure we move in the right direction even if our view/player is rotated
            Vector3 inputVector;
#if ENABLE_INPUT_SYSTEM
            var x = 0f;
            var y = 0f;

            if (Keyboard.current.wKey.isPressed) {
                y += 1f;
            }

            if (Keyboard.current.sKey.isPressed) {
                y -= 1f;
            }

            if (Keyboard.current.aKey.isPressed) {
                x -= 1f;
            }

            if (Keyboard.current.dKey.isPressed) {
                x += 1f;
            }
            inputVector = new Vector3(x, 0, y).normalized;
            var worldDirection = transform.TransformDirection(inputVector);
#elif ENABLE_LEGACY_INPUT_MANAGER
            inputVector = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            var worldDirection = transform.TransformDirection(inputVector);
#endif
            var moveWorldDirection = worldDirection * speed;
            // apply gravity to the vertical velocity
            _playerVelocity.y -= Gravity * Time.deltaTime;
            // combine move direction (x & z) with the jump option (y velocity)
            _playerVelocity = new Vector3(moveWorldDirection.x, _playerVelocity.y, moveWorldDirection.z);
            // move the player based on the calculated playerVelocity
            _controller.Move(_playerVelocity * Time.deltaTime);

            // Unity Editor registers mouse input already before the game view loads completely
            // so the movement from the "start game" button to the game view will be registered as rotation, which is pretty annoying
            // for this reason I added a option to only activate the rotation if the right mouse button is hold
            // if you do not like this you can set the rotateOnlyOnRightMouseHold property to false
            var mouseInput = Vector2.zero;
            if (!rotateOnlyOnRightMouseHold || IsRightMouseButtonDown()) {
#if ENABLE_INPUT_SYSTEM
                mouseInput = Mouse.current.delta.ReadValue() * 0.01f;
#elif ENABLE_LEGACY_INPUT_MANAGER
                mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
            }

            // the up/down rotation is applied directly to the camera, we only want the view to rotate and not the whole player itself
            cam.transform.Rotate(Vector3.right * (-mouseInput.y * mouseSpeed));

            // handle viewDirection globally in script
            // because the teleport function will adapt the viewDirection based on the pos and rot of the calculated transformation matrix between the 2 portals and the player
            viewDirection += mouseInput.x * mouseSpeed;
            // Convert the viewDirection to a quaternion and apply the rotation to the transform
            transform.rotation = Quaternion.Euler(0, viewDirection, 0);
        }

        /// <summary>
        /// checks if the right mouse button is being pressed or being hold
        /// </summary>
        private bool IsRightMouseButtonDown() {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#endif
        }
    }
}