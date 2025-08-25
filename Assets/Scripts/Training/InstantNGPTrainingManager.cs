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
    /// Instant-NGP 训练管理器 - 实现秒级图生场景
    /// </summary>
    public class InstantNGPTrainingManager : MonoBehaviour
    {
        [Header("Instant-NGP 配置")]
        [SerializeField] private string instantNGPPath = "./instant-ngp";
        [SerializeField] private string pythonPath = "./instant-ngp-env/Scripts/python.exe";
        
        [Header("训练参数")]
        [SerializeField] private int maxTrainingSteps = 1000;
        [SerializeField] private float targetTrainingTime = 3.0f; // 目标训练时间(秒)
        
        [Header("输出设置")]
        [SerializeField] private string outputPath = "trained_models/instant-ngp";
        [SerializeField] private string snapshotName = "scene_snapshot.ingp";
        
        [Header("调试信息")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool enableLogging = true;
        
        // 内部状态
        private bool isTraining = false;
        private float trainingStartTime = 0f;
        private float currentTrainingTime = 0f;
        private string currentStatus = "就绪";
        
        // 事件
        public event System.Action<string> OnStatusChanged;
        public event System.Action<float> OnProgressChanged;
        public event System.Action<bool> OnTrainingCompleted;
        
        // 训练状态
        public enum TrainingState
        {
            Ready,
            Preparing,
            Training,
            Completed,
            Failed
        }
        
        private TrainingState currentState = TrainingState.Ready;
        
        void Start()
        {
            InitializeTrainingManager();
        }
        
        void Update()
        {
            if (isTraining)
            {
                UpdateTrainingProgress();
            }
        }
        
        /// <summary>
        /// 初始化训练管理器
        /// </summary>
        private void InitializeTrainingManager()
        {
            // 验证Instant-NGP路径
            if (!Directory.Exists(instantNGPPath))
            {
                LogError($"Instant-NGP路径不存在: {instantNGPPath}");
                return;
            }
            
            // 创建输出目录
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            
            LogInfo("Instant-NGP训练管理器初始化完成");
        }
        
        /// <summary>
        /// 开始Instant-NGP训练
        /// </summary>
        public async Task<bool> StartTraining(string colmapDataPath)
        {
            if (isTraining)
            {
                LogWarning("训练已在进行中，请等待完成");
                return false;
            }
            
            try
            {
                LogInfo("开始Instant-NGP训练...");
                SetState(TrainingState.Preparing);
                
                // 1. 验证训练数据
                if (!ValidateTrainingData(colmapDataPath))
                {
                    LogError("训练数据验证失败");
                    SetState(TrainingState.Failed);
                    return false;
                }
                
                // 2. 转换数据格式
                string transformsJsonPath = await ConvertToInstantNGPFormat(colmapDataPath);
                if (string.IsNullOrEmpty(transformsJsonPath))
                {
                    LogError("数据格式转换失败");
                    SetState(TrainingState.Failed);
                    return false;
                }
                
                // 3. 开始训练
                SetState(TrainingState.Training);
                bool success = await ExecuteTraining(transformsJsonPath);
                
                if (success)
                {
                    SetState(TrainingState.Completed);
                    LogInfo("Instant-NGP训练完成！");
                    OnTrainingCompleted?.Invoke(true);
                }
                else
                {
                    SetState(TrainingState.Failed);
                    LogError("Instant-NGP训练失败");
                    OnTrainingCompleted?.Invoke(false);
                }
                
                return success;
            }
            catch (Exception e)
            {
                LogError($"训练过程中发生错误: {e.Message}");
                SetState(TrainingState.Failed);
                OnTrainingCompleted?.Invoke(false);
                return false;
            }
            finally
            {
                isTraining = false;
            }
        }
        
        /// <summary>
        /// 验证训练数据
        /// </summary>
        private bool ValidateTrainingData(string colmapDataPath)
        {
            if (!Directory.Exists(colmapDataPath))
            {
                LogError($"COLMAP数据目录不存在: {colmapDataPath}");
                return false;
            }
            
            // 检查必要文件
            string imagesPath = Path.Combine(colmapDataPath, "images");
            string sparsePath = Path.Combine(colmapDataPath, "sparse");
            
            if (!Directory.Exists(imagesPath))
            {
                LogError($"图像目录不存在: {imagesPath}");
                return false;
            }
            
            if (!Directory.Exists(sparsePath))
            {
                LogError($"稀疏重建目录不存在: {sparsePath}");
                return false;
            }
            
            LogInfo("训练数据验证通过");
            return true;
        }
        
        /// <summary>
        /// 转换为Instant-NGP格式
        /// </summary>
        private async Task<string> ConvertToInstantNGPFormat(string colmapDataPath)
        {
            try
            {
                LogInfo("转换数据格式为Instant-NGP格式...");
                
                // 使用Instant-NGP的colmap2nerf.py脚本
                string colmap2nerfScript = Path.Combine(instantNGPPath, "scripts", "colmap2nerf.py");
                string imagesPath = Path.Combine(colmapDataPath, "images");
                string sparsePath = Path.Combine(colmapDataPath, "sparse");
                string outputJsonPath = Path.Combine(outputPath, "transforms.json");
                
                // 构建Python命令
                string pythonCommand = $"{pythonPath} {colmap2nerfScript} " +
                                     $"--images {imagesPath} " +
                                     $"--text {sparsePath} " +
                                     $"--out {outputJsonPath} " +
                                     $"--aabb_scale 32";
                
                LogInfo($"执行命令: {pythonCommand}");
                
                // 异步执行Python脚本
                bool success = await ExecutePythonScript(pythonCommand);
                
                if (success && File.Exists(outputJsonPath))
                {
                    LogInfo($"数据格式转换完成: {outputJsonPath}");
                    return outputJsonPath;
                }
                else
                {
                    LogError("数据格式转换失败");
                    return null;
                }
            }
            catch (Exception e)
            {
                LogError($"数据格式转换错误: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 执行Instant-NGP训练
        /// </summary>
        private async Task<bool> ExecuteTraining(string transformsJsonPath)
        {
            try
            {
                LogInfo("开始Instant-NGP训练...");
                
                // 构建训练命令
                string runScript = Path.Combine(instantNGPPath, "scripts", "run.py");
                string snapshotPath = Path.Combine(outputPath, snapshotName);
                
                string trainingCommand = $"{pythonPath} {runScript} " +
                                       $"--scene {transformsJsonPath} " +
                                       $"--train " +
                                       $"--n_steps {maxTrainingSteps} " +
                                       $"--save_snapshot {snapshotPath} " +
                                       $"--no_gui";
                
                LogInfo($"执行训练命令: {trainingCommand}");
                
                // 开始训练计时
                trainingStartTime = Time.time;
                isTraining = true;
                
                // 异步执行训练
                bool success = await ExecutePythonScript(trainingCommand);
                
                if (success)
                {
                    // 验证训练结果
                    if (File.Exists(snapshotPath))
                    {
                        LogInfo($"训练完成，快照保存到: {snapshotPath}");
                        return true;
                    }
                    else
                    {
                        LogWarning("训练完成但未找到快照文件");
                        return false;
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                LogError($"训练执行错误: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 执行Python脚本
        /// </summary>
        private async Task<bool> ExecutePythonScript(string command)
        {
            return await Task.Run(() =>
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = $"/c {command}";
                    startInfo.UseShellExecute = false;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.CreateNoWindow = true;
                    
                    using (Process process = new Process())
                    {
                        process.StartInfo = startInfo;
                        process.Start();
                        
                        // 读取输出
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        
                        process.WaitForExit();
                        
                        if (process.ExitCode == 0)
                        {
                            if (enableLogging)
                            {
                                LogInfo($"Python脚本执行成功: {output}");
                            }
                            return true;
                        }
                        else
                        {
                            LogError($"Python脚本执行失败: {error}");
                            return false;
                        }
                    }
                }
                catch (Exception e)
                {
                    LogError($"执行Python脚本时发生错误: {e.Message}");
                    return false;
                }
            });
        }
        
        /// <summary>
        /// 更新训练进度
        /// </summary>
        private void UpdateTrainingProgress()
        {
            if (isTraining)
            {
                currentTrainingTime = Time.time - trainingStartTime;
                float progress = Mathf.Clamp01(currentTrainingTime / targetTrainingTime);
                
                OnProgressChanged?.Invoke(progress);
                
                // 检查是否超时
                if (currentTrainingTime > targetTrainingTime * 2)
                {
                    LogWarning("训练时间超时，强制停止");
                    StopTraining();
                }
            }
        }
        
        /// <summary>
        /// 停止训练
        /// </summary>
        public void StopTraining()
        {
            if (isTraining)
            {
                isTraining = false;
                SetState(TrainingState.Ready);
                LogInfo("训练已停止");
            }
        }
        
        /// <summary>
        /// 设置训练状态
        /// </summary>
        private void SetState(TrainingState newState)
        {
            currentState = newState;
            currentStatus = GetStateDescription(newState);
            OnStatusChanged?.Invoke(currentStatus);
            
            LogInfo($"训练状态: {currentStatus}");
        }
        
        /// <summary>
        /// 获取状态描述
        /// </summary>
        private string GetStateDescription(TrainingState state)
        {
            switch (state)
            {
                case TrainingState.Ready: return "就绪";
                case TrainingState.Preparing: return "准备中";
                case TrainingState.Training: return "训练中";
                case TrainingState.Completed: return "完成";
                case TrainingState.Failed: return "失败";
                default: return "未知";
            }
        }
        
        /// <summary>
        /// 获取当前状态
        /// </summary>
        public TrainingState GetCurrentState() => currentState;
        
        /// <summary>
        /// 获取当前状态描述
        /// </summary>
        public string GetCurrentStatus() => currentStatus;
        
        /// <summary>
        /// 获取训练进度
        /// </summary>
        public float GetTrainingProgress()
        {
            if (!isTraining) return 0f;
            return Mathf.Clamp01(currentTrainingTime / targetTrainingTime);
        }
        
        #region 日志系统
        
        private void LogInfo(string message)
        {
            if (showDebugInfo)
            {
                UnityEngine.Debug.Log($"[Instant-NGP] {message}");
            }
        }
        
        private void LogWarning(string message)
        {
            if (showDebugInfo)
            {
                UnityEngine.Debug.LogWarning($"[Instant-NGP] {message}");
            }
        }
        
        private void LogError(string message)
        {
            if (showDebugInfo)
            {
                UnityEngine.Debug.LogError($"[Instant-NGP] {message}");
            }
        }
        
        #endregion
    }
}
