using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Training
{
    /// <summary>
    /// Instant-NGP 训练控制器 - 整合所有训练功能
    /// 简化版本，专注于核心功能
    /// </summary>
    public class InstantNGPTrainingController : MonoBehaviour
    {
        [Header("Instant-NGP 配置")]
        [SerializeField] private string instantNGPPath = "./Instant-NGP-for-RTX-3000-and-4000";
        [SerializeField] private string instantNGPExe = "instant-ngp.exe";
        
        [Header("数据路径")]
        [SerializeField] private string datasetPath = "./Instant-NGP-for-RTX-3000-and-4000/data/nerf/unity_scene";
        
        [Header("UI 组件")]
        [SerializeField] private Button startTrainingButton;
        [SerializeField] private Button stopTrainingButton;
        [SerializeField] private Button convertDataButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text progressText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private GameObject trainingPanel;
        
        [Header("调试信息")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private Text debugText;
        
        // 内部状态
        private bool isTraining = false;
        private Process trainingProcess;
        private string currentStatus = "就绪";
        private float currentProgress = 0f;
        
        // 事件
        public event System.Action<string> OnStatusChanged;
        public event System.Action<bool> OnTrainingCompleted;
        
        void Start()
        {
            InitializeController();
            SetupUI();
            ValidateEnvironment();
        }
        
        void Update()
        {
            if (isTraining && trainingProcess != null)
            {
                UpdateTrainingProgress();
            }
        }
        
        /// <summary>
        /// 初始化控制器
        /// </summary>
        private void InitializeController()
        {
            LogInfo("Instant-NGP 训练控制器初始化...");
            
            // 验证路径
            if (!Directory.Exists(instantNGPPath))
            {
                LogError($"Instant-NGP 路径不存在: {instantNGPPath}");
                UpdateStatus("错误: Instant-NGP 路径无效");
                return;
            }
            
            // 验证可执行文件
            string exePath = Path.Combine(instantNGPPath, instantNGPExe);
            if (!File.Exists(exePath))
            {
                LogError($"Instant-NGP 可执行文件不存在: {exePath}");
                UpdateStatus("错误: instant-ngp.exe 未找到");
                return;
            }
            
            LogInfo("训练控制器初始化完成");
            UpdateStatus("就绪");
        }
        
        /// <summary>
        /// 设置UI组件
        /// </summary>
        private void SetupUI()
        {
            // 设置按钮事件
            if (startTrainingButton != null)
                startTrainingButton.onClick.AddListener(StartTraining);
            
            if (stopTrainingButton != null)
                stopTrainingButton.onClick.AddListener(StopTraining);
            
            if (convertDataButton != null)
                convertDataButton.onClick.AddListener(ConvertData);
            
            // 初始化UI状态
            UpdateUIState();
        }
        
        /// <summary>
        /// 验证环境
        /// </summary>
        private void ValidateEnvironment()
        {
            LogInfo("验证训练环境...");
            
            // 检查数据集
            if (!Directory.Exists(datasetPath))
            {
                LogWarning($"数据集路径不存在: {datasetPath}");
                UpdateStatus("警告: 数据集路径不存在");
                return;
            }
            
            // 检查transforms.json
            string transformsPath = Path.Combine(datasetPath, "transforms.json");
            if (!File.Exists(transformsPath))
            {
                LogWarning("transforms.json 不存在，需要先转换数据");
                UpdateStatus("需要转换数据");
                return;
            }
            
            // 检查图像目录
            string imagesPath = Path.Combine(datasetPath, "images");
            if (!Directory.Exists(imagesPath))
            {
                LogWarning("图像目录不存在");
                UpdateStatus("警告: 图像目录不存在");
                return;
            }
            
            LogInfo("环境验证通过");
            UpdateStatus("环境就绪");
        }
        
        /// <summary>
        /// 转换数据
        /// </summary>
        public async void ConvertData()
        {
            try
            {
                UpdateStatus("开始转换数据...");
                LogInfo("启动Python数据转换器...");
                
                // 检查Python转换器
                string converterPath = Path.Combine(Application.dataPath, "../test_converter_final.py");
                if (!File.Exists(converterPath))
                {
                    LogError("Python转换器不存在，请确保 test_converter_final.py 在项目根目录");
                    UpdateStatus("错误: 转换器未找到");
                    return;
                }
                
                // 启动Python进程
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{converterPath}\"",
                    WorkingDirectory = Path.GetDirectoryName(converterPath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        LogError("无法启动Python转换进程");
                        UpdateStatus("错误: 转换进程启动失败");
                        return;
                    }
                    
                    // 异步等待完成
                    await Task.Run(() => process.WaitForExit());
                    
                    if (process.ExitCode != 0)
                    {
                        string error = await process.StandardError.ReadToEndAsync();
                        LogError($"转换失败，退出码: {process.ExitCode}, 错误: {error}");
                        UpdateStatus("转换失败");
                        return;
                    }
                    
                    string output = await process.StandardOutput.ReadToEndAsync();
                    LogInfo($"转换成功: {output}");
                }
                
                // 重新验证环境
                ValidateEnvironment();
                UpdateStatus("数据转换完成");
                
            }
            catch (Exception ex)
            {
                LogError($"转换过程异常: {ex.Message}");
                UpdateStatus($"转换失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 开始训练
        /// </summary>
        public async void StartTraining()
        {
            if (isTraining)
            {
                LogWarning("训练已在进行中");
                return;
            }
            
            try
            {
                isTraining = true;
                UpdateStatus("启动Instant-NGP训练...");
                UpdateUIState();
                
                // 启动Instant-NGP进程
                bool trainingSuccess = await StartInstantNGPTraining();
                
                if (trainingSuccess)
                {
                    UpdateStatus("训练启动成功！");
                    OnTrainingCompleted?.Invoke(true);
                }
                else
                {
                    UpdateStatus("训练启动失败");
                    OnTrainingCompleted?.Invoke(false);
                    isTraining = false;
                    UpdateUIState();
                }
            }
            catch (Exception ex)
            {
                LogError($"启动训练时发生错误: {ex.Message}");
                UpdateStatus($"错误: {ex.Message}");
                OnTrainingCompleted?.Invoke(false);
                isTraining = false;
                UpdateUIState();
            }
        }
        
        /// <summary>
        /// 启动Instant-NGP训练
        /// </summary>
        private async Task<bool> StartInstantNGPTraining()
        {
            try
            {
                string exePath = Path.Combine(instantNGPPath, instantNGPExe);
                string workingDir = Path.GetFullPath(instantNGPPath);
                string datasetName = Path.GetFileName(datasetPath);
                
                LogInfo($"启动Instant-NGP训练: {exePath}");
                LogInfo($"工作目录: {workingDir}");
                LogInfo($"数据集: {datasetName}");
                
                // 创建进程启动信息
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"data/nerf/{datasetName}",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                };
                
                // 启动进程
                trainingProcess = new Process { StartInfo = startInfo };
                trainingProcess.Start();
                
                LogInfo($"Instant-NGP进程已启动，PID: {trainingProcess.Id}");
                UpdateStatus("Instant-NGP训练已启动");
                
                return true;
            }
            catch (Exception ex)
            {
                LogError($"启动Instant-NGP失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 更新训练进度
        /// </summary>
        private void UpdateTrainingProgress()
        {
            if (trainingProcess == null || trainingProcess.HasExited)
            {
                isTraining = false;
                UpdateStatus("训练进程已结束");
                UpdateUIState();
                return;
            }
            
            // 这里可以添加进度监控逻辑
            // 由于Instant-NGP是独立进程，进度监控比较复杂
        }
        
        /// <summary>
        /// 停止训练
        /// </summary>
        public void StopTraining()
        {
            if (trainingProcess != null && !trainingProcess.HasExited)
            {
                trainingProcess.Kill();
                LogInfo("训练进程已停止");
                UpdateStatus("训练已停止");
            }
            
            isTraining = false;
            UpdateUIState();
        }
        
        /// <summary>
        /// 更新UI状态
        /// </summary>
        private void UpdateUIState()
        {
            if (startTrainingButton != null)
                startTrainingButton.interactable = !isTraining;
            
            if (stopTrainingButton != null)
                stopTrainingButton.interactable = isTraining;
            
            if (convertDataButton != null)
                convertDataButton.interactable = !isTraining;
            
            if (progressSlider != null)
                progressSlider.value = currentProgress;
            
            if (progressText != null)
                progressText.text = $"{(currentProgress * 100):F0}%";
        }
        
        /// <summary>
        /// 更新状态显示
        /// </summary>
        private void UpdateStatus(string status)
        {
            currentStatus = status;
            LogInfo($"状态更新: {status}");
            
            if (statusText != null)
            {
                statusText.text = status;
            }
            
            OnStatusChanged?.Invoke(status);
        }
        
        /// <summary>
        /// 日志输出
        /// </summary>
        private void LogInfo(string message)
        {
            UnityEngine.Debug.Log($"[InstantNGPTrainingController] {message}");
            if (showDebugInfo && debugText != null)
            {
                debugText.text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            }
        }
        
        private void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[InstantNGPTrainingController] {message}");
            if (showDebugInfo && debugText != null)
            {
                debugText.text += $"[{DateTime.Now:HH:mm:ss}] WARNING: {message}\n";
            }
        }
        
        private void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[InstantNGPTrainingController] {message}");
            if (showDebugInfo && debugText != null)
            {
                debugText.text += $"[{DateTime.Now:HH:mm:ss}] ERROR: {message}\n";
            }
        }
        
        void OnDestroy()
        {
            StopTraining();
        }
        
        /// <summary>
        /// 获取当前状态
        /// </summary>
        public string GetCurrentStatus() => currentStatus;
        
        /// <summary>
        /// 获取当前进度
        /// </summary>
        public float GetCurrentProgress() => currentProgress;
        
        /// <summary>
        /// 检查是否正在训练
        /// </summary>
        public bool IsTraining() => isTraining;
    }
}
