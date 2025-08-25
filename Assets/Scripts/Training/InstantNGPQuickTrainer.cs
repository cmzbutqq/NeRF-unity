using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Training
{
    /// <summary>
    /// Instant-NGP 快速训练启动器 - 提供一键启动训练功能
    /// </summary>
    public class InstantNGPQuickTrainer : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private InstantNGPTrainingManager trainingManager;
        [SerializeField] private InstantNGPEnvironmentSetup environmentSetup;
        [SerializeField] private TrainingProgressMonitor progressMonitor;
        
        [Header("UI组件")]
        [SerializeField] private Button startTrainingButton;
        [SerializeField] private Button stopTrainingButton;
        [SerializeField] private Button checkEnvironmentButton;
        [SerializeField] private Button installDependenciesButton;
        [SerializeField] private Text environmentStatusText;
        [SerializeField] private Text trainingStatusText;
        [SerializeField] private GameObject environmentPanel;
        [SerializeField] private GameObject trainingPanel;
        
        [Header("训练配置")]
        [SerializeField] private string defaultColmapDataPath = "captured_data";
        [SerializeField] private bool autoStartAfterEnvironmentReady = false;
        
        [Header("调试信息")]
        [SerializeField] private bool enableLogging = true;
        
        // 内部状态
        private bool isEnvironmentReady = false;
        private bool isTrainingInProgress = false;
        
        void Start()
        {
            InitializeComponents();
            SetupEventHandlers();
            CheckEnvironmentOnStart();
        }
        
        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 自动查找组件
            if (trainingManager == null)
                trainingManager = FindObjectOfType<InstantNGPTrainingManager>();
            
            if (environmentSetup == null)
                environmentSetup = FindObjectOfType<InstantNGPEnvironmentSetup>();
            
            if (progressMonitor == null)
                progressMonitor = FindObjectOfType<TrainingProgressMonitor>();
            
            // 设置进度监控器的训练管理器引用
            if (progressMonitor != null && trainingManager != null)
            {
                progressMonitor.SetTrainingManager(trainingManager);
            }
            
            LogInfo("组件初始化完成");
        }
        
        /// <summary>
        /// 设置事件处理器
        /// </summary>
        private void SetupEventHandlers()
        {
            // 环境状态变化事件
            if (environmentSetup != null)
            {
                environmentSetup.OnEnvironmentStatusChanged += OnEnvironmentStatusChanged;
                environmentSetup.OnEnvironmentIssueFound += OnEnvironmentIssueFound;
            }
            
            // 训练完成事件
            if (trainingManager != null)
            {
                trainingManager.OnTrainingCompleted += OnTrainingCompleted;
                trainingManager.OnStatusChanged += OnTrainingStatusChanged;
            }
            
            // UI按钮事件
            if (startTrainingButton) startTrainingButton.onClick.AddListener(OnStartTrainingClicked);
            if (stopTrainingButton) stopTrainingButton.onClick.AddListener(OnStopTrainingClicked);
            if (checkEnvironmentButton) checkEnvironmentButton.onClick.AddListener(OnCheckEnvironmentClicked);
            if (installDependenciesButton) installDependenciesButton.onClick.AddListener(OnInstallDependenciesClicked);
            
            LogInfo("事件处理器设置完成");
        }
        
        /// <summary>
        /// 启动时检查环境
        /// </summary>
        private void CheckEnvironmentOnStart()
        {
            if (environmentSetup != null)
            {
                LogInfo("启动时检查环境...");
                environmentSetup.CheckEnvironment();
            }
        }
        
        /// <summary>
        /// 环境状态变化事件处理
        /// </summary>
        private void OnEnvironmentStatusChanged(InstantNGPEnvironmentSetup.EnvironmentStatus status)
        {
            isEnvironmentReady = (status == InstantNGPEnvironmentSetup.EnvironmentStatus.Ready);
            
            UpdateEnvironmentUI(status);
            
            if (isEnvironmentReady && autoStartAfterEnvironmentReady)
            {
                LogInfo("环境就绪，自动开始训练...");
                StartTraining();
            }
        }
        
        /// <summary>
        /// 环境问题发现事件处理
        /// </summary>
        private void OnEnvironmentIssueFound(string issue)
        {
            LogWarning($"环境问题: {issue}");
            UpdateEnvironmentStatusText($"环境问题: {issue}", Color.red);
        }
        
        /// <summary>
        /// 训练状态变化事件处理
        /// </summary>
        private void OnTrainingStatusChanged(string status)
        {
            UpdateTrainingStatusText(status);
            
            if (status.Contains("训练"))
            {
                isTrainingInProgress = true;
                UpdateTrainingUI(true);
            }
            else if (status.Contains("完成") || status.Contains("失败"))
            {
                isTrainingInProgress = false;
                UpdateTrainingUI(false);
            }
        }
        
        /// <summary>
        /// 训练完成事件处理
        /// </summary>
        private void OnTrainingCompleted(bool success)
        {
            if (success)
            {
                LogInfo("训练成功完成！");
                ShowSuccessMessage("训练成功完成！");
            }
            else
            {
                LogError("训练失败！");
                ShowErrorMessage("训练失败！");
            }
        }
        
        /// <summary>
        /// 更新环境UI
        /// </summary>
        private void UpdateEnvironmentUI(InstantNGPEnvironmentSetup.EnvironmentStatus status)
        {
            if (environmentStatusText == null) return;
            
            string statusText = "";
            Color statusColor = Color.white;
            
            switch (status)
            {
                case InstantNGPEnvironmentSetup.EnvironmentStatus.Ready:
                    statusText = "环境就绪 ✓";
                    statusColor = Color.green;
                    break;
                case InstantNGPEnvironmentSetup.EnvironmentStatus.Checking:
                    statusText = "检查中...";
                    statusColor = Color.yellow;
                    break;
                case InstantNGPEnvironmentSetup.EnvironmentStatus.MissingDependencies:
                    statusText = "缺少依赖";
                    statusColor = new Color(1.0f, 0.5f, 0.0f); // 橙色
                    break;
                case InstantNGPEnvironmentSetup.EnvironmentStatus.ConfigurationError:
                    statusText = "配置错误";
                    statusColor = Color.red;
                    break;
                case InstantNGPEnvironmentSetup.EnvironmentStatus.Failed:
                    statusText = "环境失败";
                    statusColor = Color.red;
                    break;
                default:
                    statusText = "未知状态";
                    statusColor = Color.gray;
                    break;
            }
            
            environmentStatusText.text = statusText;
            environmentStatusText.color = statusColor;
            
            // 更新按钮状态
            if (startTrainingButton) startTrainingButton.interactable = isEnvironmentReady;
            if (installDependenciesButton) installDependenciesButton.interactable = !isEnvironmentReady;
        }
        
        /// <summary>
        /// 更新训练UI
        /// </summary>
        private void UpdateTrainingUI(bool isTraining)
        {
            if (startTrainingButton) startTrainingButton.interactable = !isTraining && isEnvironmentReady;
            if (stopTrainingButton) stopTrainingButton.interactable = isTraining;
        }
        
        /// <summary>
        /// 更新环境状态文本
        /// </summary>
        private void UpdateEnvironmentStatusText(string text, Color color)
        {
            if (environmentStatusText != null)
            {
                environmentStatusText.text = text;
                environmentStatusText.color = color;
            }
        }
        
        /// <summary>
        /// 更新训练状态文本
        /// </summary>
        private void UpdateTrainingStatusText(string status)
        {
            if (trainingStatusText != null)
            {
                trainingStatusText.text = status;
            }
        }
        
        /// <summary>
        /// 显示成功消息
        /// </summary>
        private void ShowSuccessMessage(string message)
        {
            LogInfo(message);
            // 这里可以添加UI提示
        }
        
        /// <summary>
        /// 显示错误消息
        /// </summary>
        private void ShowErrorMessage(string message)
        {
            LogError(message);
            // 这里可以添加UI提示
        }
        
        #region 公共接口
        
        /// <summary>
        /// 开始训练
        /// </summary>
        public async void StartTraining()
        {
            if (!isEnvironmentReady)
            {
                LogWarning("环境未就绪，无法开始训练");
                ShowErrorMessage("环境未就绪，请先检查环境");
                return;
            }
            
            if (isTrainingInProgress)
            {
                LogWarning("训练已在进行中");
                return;
            }
            
            try
            {
                LogInfo("开始Instant-NGP训练...");
                
                if (trainingManager != null)
                {
                    bool success = await trainingManager.StartTraining(defaultColmapDataPath);
                    if (!success)
                    {
                        LogError("训练启动失败");
                        ShowErrorMessage("训练启动失败");
                    }
                }
                else
                {
                    LogError("训练管理器未找到");
                    ShowErrorMessage("训练管理器未找到");
                }
            }
            catch (Exception e)
            {
                LogError($"启动训练时发生错误: {e.Message}");
                ShowErrorMessage($"启动训练时发生错误: {e.Message}");
            }
        }
        
        /// <summary>
        /// 停止训练
        /// </summary>
        public void StopTraining()
        {
            if (trainingManager != null)
            {
                LogInfo("停止训练...");
                trainingManager.StopTraining();
            }
        }
        
        /// <summary>
        /// 检查环境
        /// </summary>
        public void CheckEnvironment()
        {
            if (environmentSetup != null)
            {
                LogInfo("手动检查环境...");
                environmentSetup.CheckEnvironment();
            }
        }
        
        /// <summary>
        /// 安装依赖
        /// </summary>
        public void InstallDependencies()
        {
            if (environmentSetup != null)
            {
                LogInfo("安装缺失的依赖...");
                environmentSetup.InstallMissingDependencies();
            }
        }
        
        /// <summary>
        /// 设置训练数据路径
        /// </summary>
        public void SetTrainingDataPath(string path)
        {
            defaultColmapDataPath = path;
            LogInfo($"训练数据路径设置为: {path}");
        }
        
        /// <summary>
        /// 获取环境状态
        /// </summary>
        public bool IsEnvironmentReady() => isEnvironmentReady;
        
        /// <summary>
        /// 获取训练状态
        /// </summary>
        public bool IsTrainingInProgress() => isTrainingInProgress;
        
        #endregion
        
        #region 按钮事件处理
        
        /// <summary>
        /// 开始训练按钮点击事件
        /// </summary>
        private void OnStartTrainingClicked()
        {
            StartTraining();
        }
        
        /// <summary>
        /// 停止训练按钮点击事件
        /// </summary>
        private void OnStopTrainingClicked()
        {
            StopTraining();
        }
        
        /// <summary>
        /// 检查环境按钮点击事件
        /// </summary>
        private void OnCheckEnvironmentClicked()
        {
            CheckEnvironment();
        }
        
        /// <summary>
        /// 安装依赖按钮点击事件
        /// </summary>
        private void OnInstallDependenciesClicked()
        {
            InstallDependencies();
        }
        
        #endregion
        
        #region 配置面板
        
        /// <summary>
        /// 显示训练配置面板
        /// </summary>
        public void ShowTrainingConfig()
        {
            if (trainingPanel != null)
            {
                trainingPanel.SetActive(true);
            }
        }
        
        /// <summary>
        /// 隐藏训练配置面板
        /// </summary>
        public void HideTrainingConfig()
        {
            if (trainingPanel != null)
            {
                trainingPanel.SetActive(false);
            }
        }
        
        /// <summary>
        /// 显示环境配置面板
        /// </summary>
        public void ShowEnvironmentConfig()
        {
            if (environmentPanel != null)
            {
                environmentPanel.SetActive(true);
            }
        }
        
        /// <summary>
        /// 隐藏环境配置面板
        /// </summary>
        public void HideEnvironmentConfig()
        {
            if (environmentPanel != null)
            {
                environmentPanel.SetActive(false);
            }
        }
        
        #endregion
        
        #region 日志系统
        
        private void LogInfo(string message)
        {
            if (enableLogging)
            {
                UnityEngine.Debug.Log($"[QuickTrainer] {message}");
            }
        }
        
        private void LogWarning(string message)
        {
            if (enableLogging)
            {
                UnityEngine.Debug.LogWarning($"[QuickTrainer] {message}");
            }
        }
        
        private void LogError(string message)
        {
            if (enableLogging)
            {
                UnityEngine.Debug.LogError($"[QuickTrainer] {message}");
            }
        }
        
        #endregion
        
        void OnDestroy()
        {
            // 清理事件处理器
            if (environmentSetup != null)
            {
                environmentSetup.OnEnvironmentStatusChanged -= OnEnvironmentStatusChanged;
                environmentSetup.OnEnvironmentIssueFound -= OnEnvironmentIssueFound;
            }
            
            if (trainingManager != null)
            {
                trainingManager.OnTrainingCompleted -= OnTrainingCompleted;
                trainingManager.OnStatusChanged -= OnTrainingStatusChanged;
            }
        }
    }
}
