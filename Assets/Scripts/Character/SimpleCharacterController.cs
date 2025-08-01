using UnityEngine;

namespace DataCapture
{
    /// <summary>
    /// 简单的第一人称角色控制器
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimpleCharacterController : MonoBehaviour
    {
        [Header("移动设置")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 8f;
        
        [Header("鼠标视角设置")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxLookAngle = 80f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        private CharacterController characterController;
        private float verticalRotation = 0f;
        private Vector3 velocity;
        private bool isGrounded;
        
        void Start()
        {
            characterController = GetComponent<CharacterController>();
            
            // 查找摄像机
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
                
            if (playerCamera == null)
                playerCamera = Camera.main;
            
            // 锁定鼠标光标
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            Debug.Log("角色控制器已初始化");
        }
        
        void Update()
        {
            HandleMovement();
            HandleMouseLook();
            HandleJump();
            HandleInput();
        }
        
        void HandleMovement()
        {
            // 获取WASD输入
            float horizontal = Input.GetAxis("Horizontal"); // A/D
            float vertical = Input.GetAxis("Vertical");     // W/S
            
            // 计算移动方向（相对于角色朝向）
            Vector3 direction = transform.right * horizontal + transform.forward * vertical;
            direction = Vector3.ClampMagnitude(direction, 1f);
            
            // 应用移动
            characterController.Move(direction * moveSpeed * Time.deltaTime);
        }
        
        void HandleMouseLook()
        {
            // 获取鼠标输入
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            // 水平旋转（Y轴） - 旋转角色身体
            transform.Rotate(Vector3.up * mouseX);
            
            // 垂直旋转（X轴） - 旋转摄像机
            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
            
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
            }
        }
        
        void HandleJump()
        {
            // 检查是否在地面
            isGrounded = characterController.isGrounded;
            
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // 小的负值确保贴地
            }
            
            // 空格键跳跃
            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpForce * -2f * Physics.gravity.y);
            }
            
            // 应用重力
            velocity.y += Physics.gravity.y * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }
        
        void HandleInput()
        {
            // ESC键切换鼠标锁定状态
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleCursorLock();
            }
        }
        
        /// <summary>
        /// 切换鼠标锁定状态
        /// </summary>
        public void ToggleCursorLock()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Debug.Log("鼠标已解锁");
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Debug.Log("鼠标已锁定");
            }
        }
        
        /// <summary>
        /// 获取玩家摄像机
        /// </summary>
        public Camera GetPlayerCamera()
        {
            return playerCamera;
        }
        
        /// <summary>
        /// 获取当前移动速度
        /// </summary>
        public float GetCurrentSpeed()
        {
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
            return horizontalVelocity.magnitude;
        }
        
        /// <summary>
        /// 检查是否在移动
        /// </summary>
        public bool IsMoving()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            return Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
        }
        
        void OnGUI()
        {
            if (!showDebugInfo) return;
            
            // 显示控制说明
            GUI.Box(new Rect(Screen.width - 220, 10, 200, 140), "角色控制");
            GUI.Label(new Rect(Screen.width - 210, 35, 180, 20), "WASD: 移动");
            GUI.Label(new Rect(Screen.width - 210, 55, 180, 20), "鼠标: 视角");
            GUI.Label(new Rect(Screen.width - 210, 75, 180, 20), "空格: 跳跃");
            GUI.Label(new Rect(Screen.width - 210, 95, 180, 20), "C: 手动采集");
            GUI.Label(new Rect(Screen.width - 210, 115, 180, 20), "ESC: 解锁鼠标");
            
            // 显示状态信息
            GUI.Box(new Rect(Screen.width - 220, 160, 200, 80), "状态信息");
            GUI.Label(new Rect(Screen.width - 210, 185, 180, 20), $"移动: {(IsMoving() ? "是" : "否")}");
            GUI.Label(new Rect(Screen.width - 210, 205, 180, 20), $"在地面: {(isGrounded ? "是" : "否")}");
            GUI.Label(new Rect(Screen.width - 210, 225, 180, 20), $"速度: {GetCurrentSpeed():F1}");
        }
    }
}
