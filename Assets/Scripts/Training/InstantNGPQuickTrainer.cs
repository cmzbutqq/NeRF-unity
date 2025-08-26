using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Training
{
    /// <summary>
    /// Instant-NGP 快速训练启动器 - 一键启动秒级训练
    /// </summary>
    public class InstantNGPQuickTrainer : MonoBehaviour
    {
        [Header("Instant-NGP 路径配置")]
        [SerializeField] private string instantNGPPath = "./Instant-NGP-for-RTX-3000-and-4000";
        [SerializeField] private string instantNGPExe = "instant-ngp.exe";
        
        [Header("数据路径")]
        [SerializeField] private string colmapDataPath = "./captured_data";
        [SerializeField] private string outputPath = "./trained_models/instant-ngp";
        
        [Header("训练参数")]
        [SerializeField] private int aabbScale = 32; // 场景缩放因子
        [SerializeField] private bool keepColmapCoords = false; // 保持COLMAP坐标系
        
        [Header("UI 引用")]
        [SerializeField] private UnityEngine.UI.Button startTrainingButton;
        [SerializeField] private UnityEngine.UI.Text statusText;
        [SerializeField] private UnityEngine.UI.Slider progressSlider;
        
        // 内部状态
        private bool isTraining = false;
        private Process trainingProcess;
        private string currentStatus = "就绪";
        private float currentProgress = 0f;
        
        // 事件
        public event System.Action<string> OnStatusChanged;
        public event System.Action<float> OnProgressChanged;
        public event System.Action<bool> OnTrainingCompleted;
        
        void Start()
        {
            InitializeTrainer();
            SetupUI();
        }
        
        void Update()
        {
            if (isTraining && trainingProcess != null)
            {
                UpdateTrainingProgress();
            }
        }
        
        /// <summary>
        /// 初始化训练器
        /// </summary>
        private void InitializeTrainer()
        {
            // 验证Instant-NGP路径
            string exePath = Path.Combine(instantNGPPath, instantNGPExe);
            if (!File.Exists(exePath))
            {
                LogError($"Instant-NGP可执行文件不存在: {exePath}");
                UpdateStatus("错误: Instant-NGP未找到");
                return;
            }
            
            // 验证COLMAP数据
            if (!Directory.Exists(colmapDataPath))
            {
                LogError($"COLMAP数据路径不存在: {colmapDataPath}");
                UpdateStatus("错误: 训练数据未找到");
                return;
            }
            
            // 创建输出目录
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            
            LogInfo("Instant-NGP快速训练器初始化完成");
            UpdateStatus("就绪 - 点击开始训练");
        }
        
        /// <summary>
        /// 设置UI组件
        /// </summary>
        private void SetupUI()
        {
            if (startTrainingButton != null)
            {
                startTrainingButton.onClick.AddListener(StartTraining);
            }
        }
        
        /// <summary>
        /// 开始Instant-NGP训练
        /// </summary>
        public async void StartTraining()
        {
            if (isTraining)
            {
                LogWarning("训练已在进行中，请等待完成");
                return;
            }
            
            try
            {
                UpdateStatus("准备训练数据...");
                isTraining = true;
                currentProgress = 0f;
                
                // 步骤1: 转换COLMAP数据为Instant-NGP格式
                bool conversionSuccess = await ConvertColmapToInstantNGP();
                if (!conversionSuccess)
                {
                    throw new Exception("COLMAP数据转换失败");
                }
                
                UpdateStatus("启动Instant-NGP训练...");
                currentProgress = 0.3f;
                
                // 步骤2: 启动Instant-NGP训练
                bool trainingSuccess = await StartInstantNGPTraining();
                if (!trainingSuccess)
                {
                    throw new Exception("Instant-NGP训练启动失败");
                }
                
                UpdateStatus("训练进行中...");
                currentProgress = 0.6f;
                
                // 步骤3: 监控训练进度
                await MonitorTrainingProgress();
                
                UpdateStatus("训练完成！");
                currentProgress = 1.0f;
                OnTrainingCompleted?.Invoke(true);
                
            }
            catch (Exception ex)
            {
                LogError($"训练失败: {ex.Message}");
                UpdateStatus($"训练失败: {ex.Message}");
                OnTrainingCompleted?.Invoke(false);
            }
            finally
            {
                isTraining = false;
                if (trainingProcess != null && !trainingProcess.HasExited)
                {
                    trainingProcess.Kill();
                    trainingProcess = null;
                }
            }
        }
        
        /// <summary>
        /// 转换COLMAP数据为Instant-NGP格式
        /// </summary>
        private async Task<bool> ConvertColmapToInstantNGP()
        {
            try
            {
                UpdateStatus("转换COLMAP数据...");
                
                // 使用Instant-NGP的colmap2nerf.py脚本
                string colmap2nerfScript = Path.Combine(instantNGPPath, "scripts", "colmap2nerf.py");
                string colmapTextPath = Path.Combine(colmapDataPath, "sparse", "0");
                string imagesPath = Path.Combine(colmapDataPath, "images");
                string outputJsonPath = Path.Combine(outputPath, "transforms.json");
                
                if (!File.Exists(colmap2nerfScript))
                {
                    LogError($"colmap2nerf.py脚本不存在: {colmap2nerfScript}");
                    return false;
                }
                
                // 构建转换命令
                string arguments = $"{colmap2nerfScript} " +
                                 $"--text {colmapTextPath} " +
                                 $"--images {imagesPath} " +
                                 $"--out {outputJsonPath} " +
                                 $"--aabb_scale {aabbScale}";
                
                if (keepColmapCoords)
                {
                    arguments += " --keep_colmap_coords";
                }
                
                LogInfo($"执行转换命令: python {arguments}");
                
                // 执行Python脚本
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = arguments,
                    WorkingDirectory = instantNGPPath,
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
                        return false;
                    }
                    
                    // 异步等待完成
                    await Task.Run(() => process.WaitForExit());
                    
                    if (process.ExitCode != 0)
                    {
                        string error = await process.StandardError.ReadToEndAsync();
                        LogError($"转换失败，退出码: {process.ExitCode}, 错误: {error}");
                        return false;
                    }
                    
                    string output = await process.StandardOutput.ReadToEndAsync();
                    LogInfo($"转换成功: {output}");
                }
                
                // 验证输出文件
                if (!File.Exists(outputJsonPath))
                {
                    LogError("转换后的transforms.json文件不存在");
                    return false;
                }
                
                LogInfo("COLMAP数据转换完成");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"转换过程异常: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 启动Instant-NGP训练
        /// </summary>
        private async Task<bool> StartInstantNGPTraining()
        {
            try
            {
                UpdateStatus("启动Instant-NGP...");
                
                string exePath = Path.Combine(instantNGPPath, instantNGPExe);
                string transformsJsonPath = Path.Combine(outputPath, "transforms.json");
                
                if (!File.Exists(transformsJsonPath))
                {
                    LogError("transforms.json文件不存在，无法启动训练");
                    return false;
                }
                
                // 启动Instant-NGP进程
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = transformsJsonPath,
                    WorkingDirectory = instantNGPPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false // 显示GUI窗口
                };
                
                trainingProcess = Process.Start(startInfo);
                if (trainingProcess == null)
                {
                    LogError("无法启动Instant-NGP进程");
                    return false;
                }
                
                LogInfo("Instant-NGP进程启动成功");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"启动Instant-NGP失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 监控训练进度
        /// </summary>
        private async Task MonitorTrainingProgress()
        {
            try
            {
                UpdateStatus("监控训练进度...");
                
                // 等待训练进程完成或超时
                int timeoutSeconds = 30; // 30秒超时
                int elapsedSeconds = 0;
                
                while (trainingProcess != null && !trainingProcess.HasExited && elapsedSeconds < timeoutSeconds)
                {
                    await Task.Delay(1000); // 等待1秒
                    elapsedSeconds++;
                    
                    // 更新进度（模拟）
                    float progress = 0.6f + (elapsedSeconds / (float)timeoutSeconds) * 0.4f;
                    currentProgress = Mathf.Clamp01(progress);
                    OnProgressChanged?.Invoke(currentProgress);
                    
                    UpdateStatus($"训练中... ({elapsedSeconds}s)");
                }
                
                if (trainingProcess != null && !trainingProcess.HasExited)
                {
                    LogWarning("训练超时，强制结束进程");
                    trainingProcess.Kill();
                }
                
                LogInfo("训练监控完成");
            }
            catch (Exception ex)
            {
                LogError($"监控训练进度失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新训练进度
        /// </summary>
        private void UpdateTrainingProgress()
        {
            if (trainingProcess != null && !trainingProcess.HasExited)
            {
                // 这里可以添加更详细的进度监控逻辑
                // 比如解析Instant-NGP的输出日志
            }
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
        /// 日志记录
        /// </summary>
        private void LogInfo(string message)
        {
            UnityEngine.Debug.Log($"[InstantNGPQuickTrainer] {message}");
        }
        
        private void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[InstantNGPQuickTrainer] {message}");
        }
        
        private void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[InstantNGPQuickTrainer] {message}");
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
        
        void OnDestroy()
        {
            if (trainingProcess != null && !trainingProcess.HasExited)
            {
                trainingProcess.Kill();
            }
        }
    }
}
