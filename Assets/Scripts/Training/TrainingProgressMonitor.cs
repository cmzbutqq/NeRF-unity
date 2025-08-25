using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Training
{
    /// <summary>
    /// 训练进度监控器 - 提供实时训练状态和进度显示
    /// </summary>
    public class TrainingProgressMonitor : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text timeText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Image progressFillImage;
        [SerializeField] private Button startButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button resetButton;
        
        [Header("进度条颜色")]
        [SerializeField] private Color preparingColor = Color.yellow;
        [SerializeField] private Color trainingColor = Color.blue;
        [SerializeField] private Color completedColor = Color.green;
        [SerializeField] private Color failedColor = Color.red;
        
        [Header("训练管理器引用")]
        [SerializeField] private InstantNGPTrainingManager trainingManager;
        
        // 内部状态
        private float startTime = 0f;
        private bool isMonitoring = false;
        private string lastStatus = "";
        
        void Start()
        {
            InitializeUI();
            SetupEventHandlers();
        }
        
        void Update()
        {
            if (isMonitoring)
            {
                UpdateTimeDisplay();
                UpdateProgressDisplay();
            }
        }
        
        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            // 设置初始状态
            if (statusText) statusText.text = "就绪";
            if (progressText) progressText.text = "0%";
            if (timeText) timeText.text = "00:00";
            if (progressSlider) progressSlider.value = 0f;
            if (progressFillImage) progressFillImage.color = Color.gray;
            
            // 设置按钮状态
            if (startButton) startButton.interactable = true;
            if (stopButton) stopButton.interactable = false;
            if (resetButton) resetButton.interactable = false;
        }
        
        /// <summary>
        /// 设置事件处理器
        /// </summary>
        private void SetupEventHandlers()
        {
            if (trainingManager != null)
            {
                trainingManager.OnStatusChanged += OnTrainingStatusChanged;
                trainingManager.OnProgressChanged += OnTrainingProgressChanged;
                trainingManager.OnTrainingCompleted += OnTrainingCompleted;
            }
            
            if (startButton) startButton.onClick.AddListener(OnStartButtonClicked);
            if (stopButton) stopButton.onClick.AddListener(OnStopButtonClicked);
            if (resetButton) resetButton.onClick.AddListener(OnResetButtonClicked);
        }
        
        /// <summary>
        /// 开始监控
        /// </summary>
        public void StartMonitoring()
        {
            isMonitoring = true;
            startTime = Time.time;
            
            if (startButton) startButton.interactable = false;
            if (stopButton) stopButton.interactable = true;
            if (resetButton) resetButton.interactable = false;
            
            LogInfo("开始监控训练进度");
        }
        
        /// <summary>
        /// 停止监控
        /// </summary>
        public void StopMonitoring()
        {
            isMonitoring = false;
            
            if (startButton) startButton.interactable = true;
            if (stopButton) stopButton.interactable = false;
            if (resetButton) resetButton.interactable = true;
            
            LogInfo("停止监控训练进度");
        }
        
        /// <summary>
        /// 重置监控器
        /// </summary>
        public void ResetMonitor()
        {
            isMonitoring = false;
            startTime = 0f;
            
            // 重置UI
            if (statusText) statusText.text = "就绪";
            if (progressText) progressText.text = "0%";
            if (timeText) timeText.text = "00:00";
            if (progressSlider) progressSlider.value = 0f;
            if (progressFillImage) progressFillImage.color = Color.gray;
            
            // 重置按钮状态
            if (startButton) startButton.interactable = true;
            if (stopButton) stopButton.interactable = false;
            if (resetButton) resetButton.interactable = false;
            
            LogInfo("监控器已重置");
        }
        
        /// <summary>
        /// 更新状态显示
        /// </summary>
        private void UpdateStatusDisplay(string status)
        {
            if (statusText && status != lastStatus)
            {
                statusText.text = status;
                lastStatus = status;
                
                // 根据状态更新颜色
                UpdateStatusColor(status);
            }
        }
        
        /// <summary>
        /// 更新状态颜色
        /// </summary>
        private void UpdateStatusColor(string status)
        {
            if (!progressFillImage) return;
            
            Color targetColor = Color.gray;
            
            if (status.Contains("准备"))
                targetColor = preparingColor;
            else if (status.Contains("训练"))
                targetColor = trainingColor;
            else if (status.Contains("完成"))
                targetColor = completedColor;
            else if (status.Contains("失败"))
                targetColor = failedColor;
            
            progressFillImage.color = targetColor;
        }
        
        /// <summary>
        /// 更新进度显示
        /// </summary>
        private void UpdateProgressDisplay()
        {
            if (trainingManager != null)
            {
                float progress = trainingManager.GetTrainingProgress();
                
                if (progressSlider)
                {
                    progressSlider.value = progress;
                }
                
                if (progressText)
                {
                    int percentage = Mathf.RoundToInt(progress * 100f);
                    progressText.text = $"{percentage}%";
                }
            }
        }
        
        /// <summary>
        /// 更新时间显示
        /// </summary>
        private void UpdateTimeDisplay()
        {
            if (timeText)
            {
                float elapsedTime = Time.time - startTime;
                TimeSpan timeSpan = TimeSpan.FromSeconds(elapsedTime);
                timeText.text = $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
        }
        
        /// <summary>
        /// 训练状态变化事件处理
        /// </summary>
        private void OnTrainingStatusChanged(string status)
        {
            UpdateStatusDisplay(status);
            
            // 根据状态自动开始/停止监控
            if (status.Contains("准备") || status.Contains("训练"))
            {
                if (!isMonitoring)
                {
                    StartMonitoring();
                }
            }
            else if (status.Contains("完成") || status.Contains("失败"))
            {
                if (isMonitoring)
                {
                    StopMonitoring();
                }
            }
        }
        
        /// <summary>
        /// 训练进度变化事件处理
        /// </summary>
        private void OnTrainingProgressChanged(float progress)
        {
            // 进度更新已在Update中处理
        }
        
        /// <summary>
        /// 训练完成事件处理
        /// </summary>
        private void OnTrainingCompleted(bool success)
        {
            if (success)
            {
                LogInfo("训练成功完成！");
                ShowCompletionMessage("训练成功完成！", Color.green);
            }
            else
            {
                LogError("训练失败！");
                ShowCompletionMessage("训练失败！", Color.red);
            }
        }
        
        /// <summary>
        /// 显示完成消息
        /// </summary>
        private void ShowCompletionMessage(string message, Color color)
        {
            if (statusText)
            {
                statusText.text = message;
                statusText.color = color;
            }
            
            // 3秒后恢复默认颜色
            StartCoroutine(ResetStatusColor(3f));
        }
        
        /// <summary>
        /// 重置状态颜色
        /// </summary>
        private IEnumerator ResetStatusColor(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (statusText)
            {
                statusText.color = Color.white;
            }
        }
        
        #region 按钮事件处理
        
        /// <summary>
        /// 开始按钮点击事件
        /// </summary>
        private void OnStartButtonClicked()
        {
            if (trainingManager != null)
            {
                LogInfo("手动开始训练...");
                // 这里可以添加手动开始训练的逻辑
            }
        }
        
        /// <summary>
        /// 停止按钮点击事件
        /// </summary>
        private void OnStopButtonClicked()
        {
            if (trainingManager != null)
            {
                LogInfo("手动停止训练...");
                trainingManager.StopTraining();
            }
        }
        
        /// <summary>
        /// 重置按钮点击事件
        /// </summary>
        private void OnResetButtonClicked()
        {
            ResetMonitor();
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置训练管理器引用
        /// </summary>
        public void SetTrainingManager(InstantNGPTrainingManager manager)
        {
            trainingManager = manager;
            
            if (trainingManager != null)
            {
                trainingManager.OnStatusChanged += OnTrainingStatusChanged;
                trainingManager.OnProgressChanged += OnTrainingProgressChanged;
                trainingManager.OnTrainingCompleted += OnTrainingCompleted;
            }
        }
        
        /// <summary>
        /// 获取当前训练状态
        /// </summary>
        public string GetCurrentStatus()
        {
            if (trainingManager != null)
            {
                return trainingManager.GetCurrentStatus();
            }
            return "未知";
        }
        
        /// <summary>
        /// 获取当前训练进度
        /// </summary>
        public float GetCurrentProgress()
        {
            if (trainingManager != null)
            {
                return trainingManager.GetTrainingProgress();
            }
            return 0f;
        }
        
        /// <summary>
        /// 获取训练时间
        /// </summary>
        public float GetTrainingTime()
        {
            if (isMonitoring)
            {
                return Time.time - startTime;
            }
            return 0f;
        }
        
        #endregion
        
        #region 日志系统
        
        private void LogInfo(string message)
        {
            UnityEngine.Debug.Log($"[TrainingMonitor] {message}");
        }
        
        private void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[TrainingMonitor] {message}");
        }
        
        private void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[TrainingMonitor] {message}");
        }
        
        #endregion
        
        void OnDestroy()
        {
            // 清理事件处理器
            if (trainingManager != null)
            {
                trainingManager.OnStatusChanged -= OnTrainingStatusChanged;
                trainingManager.OnProgressChanged -= OnTrainingProgressChanged;
                trainingManager.OnTrainingCompleted -= OnTrainingCompleted;
            }
        }
    }
}
