using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Training
{
    /// <summary>
    /// Instant-NGP 环境配置器 - 负责环境检测、依赖安装和环境验证
    /// </summary>
    public class InstantNGPEnvironmentSetup : MonoBehaviour
    {
        [Header("环境配置")]
        [SerializeField] private string instantNGPPath = "./instant-ngp";
        [SerializeField] private string pythonPath = "./instant-ngp-env/Scripts/python.exe";
        [SerializeField] private string virtualEnvPath = "./instant-ngp-env";
        [SerializeField] private string requirementsPath = "./instant-ngp/requirements.txt";
        
        [Header("环境检测")]
        [SerializeField] private bool autoDetectEnvironment = true;
        [SerializeField] private bool showEnvironmentInfo = true;
        
        [Header("调试信息")]
        [SerializeField] private bool enableLogging = true;
        
        // 环境状态
        private EnvironmentStatus environmentStatus = EnvironmentStatus.Unknown;
        private List<string> environmentIssues = new List<string>();
        private Dictionary<string, bool> componentStatus = new Dictionary<string, bool>();
        
        // 事件
        public event System.Action<EnvironmentStatus> OnEnvironmentStatusChanged;
        public event System.Action<string> OnEnvironmentIssueFound;
        
        // 环境状态枚举
        public enum EnvironmentStatus
        {
            Unknown,
            Checking,
            Ready,
            MissingDependencies,
            ConfigurationError,
            Failed
        }
        
        void Start()
        {
            if (autoDetectEnvironment)
            {
                StartCoroutine(CheckEnvironmentAsync());
            }
        }
        
        /// <summary>
        /// 异步检查环境
        /// </summary>
        public IEnumerator CheckEnvironmentAsync()
        {
            SetEnvironmentStatus(EnvironmentStatus.Checking);
            
            yield return new WaitForSeconds(0.1f);
            
            // 1. 检查Instant-NGP路径
            yield return StartCoroutine(CheckInstantNGPPath());
            
            // 2. 检查Python环境
            yield return StartCoroutine(CheckPythonEnvironment());
            
            // 3. 检查CUDA环境
            yield return StartCoroutine(CheckCUDAEnvironment());
            
            // 4. 检查依赖包
            yield return StartCoroutine(CheckDependencies());
            
            // 5. 验证环境完整性
            yield return StartCoroutine(ValidateEnvironment());
            
            // 6. 设置最终状态
            SetFinalEnvironmentStatus();
            
            if (showEnvironmentInfo)
            {
                DisplayEnvironmentInfo();
            }
        }
        
        /// <summary>
        /// 检查Instant-NGP路径
        /// </summary>
        private IEnumerator CheckInstantNGPPath()
        {
            LogInfo("检查Instant-NGP路径...");
            
            if (Directory.Exists(instantNGPPath))
            {
                componentStatus["Instant-NGP路径"] = true;
                LogInfo($"Instant-NGP路径存在: {instantNGPPath}");
                
                // 检查关键文件
                string[] requiredFiles = {
                    "scripts/run.py",
                    "scripts/colmap2nerf.py",
                    "CMakeLists.txt"
                };
                
                bool allFilesExist = true;
                foreach (string file in requiredFiles)
                {
                    string fullPath = Path.Combine(instantNGPPath, file);
                    if (!File.Exists(fullPath))
                    {
                        LogWarning($"缺少关键文件: {file}");
                        allFilesExist = false;
                    }
                }
                
                componentStatus["Instant-NGP文件完整性"] = allFilesExist;
            }
            else
            {
                componentStatus["Instant-NGP路径"] = false;
                environmentIssues.Add($"Instant-NGP路径不存在: {instantNGPPath}");
                OnEnvironmentIssueFound?.Invoke($"Instant-NGP路径不存在: {instantNGPPath}");
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 检查Python环境
        /// </summary>
        private IEnumerator CheckPythonEnvironment()
        {
            LogInfo("检查Python环境...");
            
            try
            {
                // 检查Python版本
                string pythonVersion = GetPythonVersion();
                if (!string.IsNullOrEmpty(pythonVersion))
                {
                    componentStatus["Python安装"] = true;
                    LogInfo($"Python版本: {pythonVersion}");
                    
                    // 检查版本兼容性
                    if (IsPythonVersionCompatible(pythonVersion))
                    {
                        componentStatus["Python版本兼容性"] = true;
                    }
                    else
                    {
                        componentStatus["Python版本兼容性"] = false;
                        environmentIssues.Add($"Python版本不兼容: {pythonVersion} (需要3.8+)");
                        OnEnvironmentIssueFound?.Invoke($"Python版本不兼容: {pythonVersion}");
                    }
                }
                else
                {
                    componentStatus["Python安装"] = false;
                    environmentIssues.Add("Python未安装或无法访问");
                    OnEnvironmentIssueFound?.Invoke("Python未安装或无法访问");
                }
                
                // 检查虚拟环境
                if (Directory.Exists(virtualEnvPath))
                {
                    componentStatus["虚拟环境"] = true;
                    LogInfo($"虚拟环境存在: {virtualEnvPath}");
                }
                else
                {
                    componentStatus["虚拟环境"] = false;
                    LogWarning($"虚拟环境不存在: {virtualEnvPath}");
                }
            }
            catch (Exception e)
            {
                LogError($"Python环境检查错误: {e.Message}");
                componentStatus["Python环境"] = false;
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 检查CUDA环境
        /// </summary>
        private IEnumerator CheckCUDAEnvironment()
        {
            LogInfo("检查CUDA环境...");
            
            try
            {
                // 检查CUDA版本
                string cudaVersion = GetCUDAVersion();
                if (!string.IsNullOrEmpty(cudaVersion))
                {
                    componentStatus["CUDA安装"] = true;
                    LogInfo($"CUDA版本: {cudaVersion}");
                    
                    // 检查版本兼容性
                    if (IsCUDAVersionCompatible(cudaVersion))
                    {
                        componentStatus["CUDA版本兼容性"] = true;
                    }
                    else
                    {
                        componentStatus["CUDA版本兼容性"] = false;
                        environmentIssues.Add($"CUDA版本不兼容: {cudaVersion} (需要11.8+)");
                        OnEnvironmentIssueFound?.Invoke($"CUDA版本不兼容: {cudaVersion}");
                    }
                }
                else
                {
                    componentStatus["CUDA安装"] = false;
                    environmentIssues.Add("CUDA未安装或无法访问");
                    OnEnvironmentIssueFound?.Invoke("CUDA未安装或无法访问");
                }
                
                // 检查GPU设备
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 ||
                    SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D12)
                {
                    componentStatus["GPU支持"] = true;
                    LogInfo($"GPU: {SystemInfo.graphicsDeviceName}");
                }
                else
                {
                    componentStatus["GPU支持"] = false;
                    environmentIssues.Add("不支持的GPU类型");
                    OnEnvironmentIssueFound?.Invoke("不支持的GPU类型");
                }
            }
            catch (Exception e)
            {
                LogError($"CUDA环境检查错误: {e.Message}");
                componentStatus["CUDA环境"] = false;
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 检查依赖包
        /// </summary>
        private IEnumerator CheckDependencies()
        {
            LogInfo("检查Python依赖包...");
            
            try
            {
                if (File.Exists(requirementsPath))
                {
                    string[] requirements = File.ReadAllLines(requirementsPath);
                    bool allDependenciesInstalled = true;
                    
                    foreach (string requirement in requirements)
                    {
                        if (string.IsNullOrWhiteSpace(requirement) || requirement.StartsWith("#"))
                            continue;
                        
                        string packageName = requirement.Split('=')[0].Split('>')[0].Split('<')[0].Split('~')[0].Trim();
                        
                        if (!CheckPythonPackage(packageName))
                        {
                            LogWarning($"缺少依赖包: {packageName}");
                            allDependenciesInstalled = false;
                        }
                    }
                    
                    componentStatus["Python依赖包"] = allDependenciesInstalled;
                    
                    if (allDependenciesInstalled)
                    {
                        LogInfo("所有Python依赖包已安装");
                    }
                    else
                    {
                        environmentIssues.Add("部分Python依赖包未安装");
                        OnEnvironmentIssueFound?.Invoke("部分Python依赖包未安装");
                    }
                }
                else
                {
                    componentStatus["Python依赖包"] = false;
                    environmentIssues.Add("requirements.txt文件不存在");
                    OnEnvironmentIssueFound?.Invoke("requirements.txt文件不存在");
                }
            }
            catch (Exception e)
            {
                LogError($"依赖包检查错误: {e.Message}");
                componentStatus["Python依赖包"] = false;
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 验证环境完整性
        /// </summary>
        private IEnumerator ValidateEnvironment()
        {
            LogInfo("验证环境完整性...");
            
            // 检查关键组件状态
            bool environmentReady = true;
            
            foreach (var kvp in componentStatus)
            {
                if (!kvp.Value)
                {
                    environmentReady = false;
                    break;
                }
            }
            
            if (environmentReady)
            {
                LogInfo("环境验证通过");
            }
            else
            {
                LogWarning("环境验证失败");
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 设置最终环境状态
        /// </summary>
        private void SetFinalEnvironmentStatus()
        {
            if (environmentIssues.Count == 0)
            {
                SetEnvironmentStatus(EnvironmentStatus.Ready);
            }
            else if (environmentIssues.Count <= 2)
            {
                SetEnvironmentStatus(EnvironmentStatus.MissingDependencies);
            }
            else
            {
                SetEnvironmentStatus(EnvironmentStatus.Failed);
            }
        }
        
        /// <summary>
        /// 获取Python版本
        /// </summary>
        private string GetPythonVersion()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = pythonPath;
                startInfo.Arguments = "--version";
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.CreateNoWindow = true;
                
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0)
                    {
                        return output.Trim();
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"获取Python版本错误: {e.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// 检查Python版本兼容性
        /// </summary>
        private bool IsPythonVersionCompatible(string version)
        {
            try
            {
                // 提取版本号
                string[] parts = version.Split(' ');
                if (parts.Length > 1)
                {
                    string versionNumber = parts[1];
                    string[] numbers = versionNumber.Split('.');
                    
                    if (numbers.Length >= 2)
                    {
                        int major = int.Parse(numbers[0]);
                        int minor = int.Parse(numbers[1]);
                        
                        return major >= 3 && minor >= 8;
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"Python版本解析错误: {e.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取CUDA版本
        /// </summary>
        private string GetCUDAVersion()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "nvcc";
                startInfo.Arguments = "--version";
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.CreateNoWindow = true;
                
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0)
                    {
                        // 解析CUDA版本
                        string[] lines = output.Split('\n');
                        foreach (string line in lines)
                        {
                            if (line.Contains("release"))
                            {
                                string[] parts = line.Split(' ');
                                foreach (string part in parts)
                                {
                                    if (part.Contains("."))
                                    {
                                        return part.Trim(',');
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"获取CUDA版本错误: {e.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// 检查CUDA版本兼容性
        /// </summary>
        private bool IsCUDAVersionCompatible(string version)
        {
            try
            {
                string[] numbers = version.Split('.');
                if (numbers.Length >= 2)
                {
                    int major = int.Parse(numbers[0]);
                    int minor = int.Parse(numbers[1]);
                    
                    return (major == 11 && minor >= 8) || major >= 12;
                }
            }
            catch (Exception e)
            {
                LogError($"CUDA版本解析错误: {e.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// 检查Python包是否安装
        /// </summary>
        private bool CheckPythonPackage(string packageName)
        {
            if (packageName == "opencv-python-headless")
            {
                packageName = "cv2";
            }
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = pythonPath;
                startInfo.Arguments = $"-c \"import {packageName}\"";
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                    
                    return process.ExitCode == 0;
                }
            }
            catch (Exception e)
            {
                LogError($"检查Python包错误: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 设置环境状态
        /// </summary>
        private void SetEnvironmentStatus(EnvironmentStatus newStatus)
        {
            environmentStatus = newStatus;
            OnEnvironmentStatusChanged?.Invoke(newStatus);
            
            LogInfo($"环境状态: {newStatus}");
        }
        
        /// <summary>
        /// 显示环境信息
        /// </summary>
        private void DisplayEnvironmentInfo()
        {
            LogInfo("=== Instant-NGP 环境信息 ===");
            LogInfo($"环境状态: {environmentStatus}");
            
            foreach (var kvp in componentStatus)
            {
                string status = kvp.Value ? "✓" : "✗";
                LogInfo($"{status} {kvp.Key}");
            }
            
            if (environmentIssues.Count > 0)
            {
                LogWarning("=== 环境问题 ===");
                foreach (string issue in environmentIssues)
                {
                    LogWarning($"- {issue}");
                }
            }
            
            LogInfo("========================");
        }
        
        #region 公共接口
        
        /// <summary>
        /// 获取环境状态
        /// </summary>
        public EnvironmentStatus GetEnvironmentStatus() => environmentStatus;
        
        /// <summary>
        /// 获取环境问题列表
        /// </summary>
        public List<string> GetEnvironmentIssues() => new List<string>(environmentIssues);
        
        /// <summary>
        /// 获取组件状态
        /// </summary>
        public Dictionary<string, bool> GetComponentStatus() => new Dictionary<string, bool>(componentStatus);
        
        /// <summary>
        /// 手动检查环境
        /// </summary>
        public void CheckEnvironment()
        {
            StartCoroutine(CheckEnvironmentAsync());
        }
        
        /// <summary>
        /// 安装缺失的依赖
        /// </summary>
        public void InstallMissingDependencies()
        {
            if (environmentStatus == EnvironmentStatus.Ready)
            {
                LogInfo("环境已就绪，无需安装依赖");
                return;
            }
            
            LogInfo("开始安装缺失的依赖...");
            StartCoroutine(InstallDependenciesAsync());
        }
        
        /// <summary>
        /// 异步安装依赖
        /// </summary>
        private IEnumerator InstallDependenciesAsync()
        {
            // 创建虚拟环境
            if (!Directory.Exists(virtualEnvPath))
            {
                LogInfo("创建Python虚拟环境...");
                yield return StartCoroutine(CreateVirtualEnvironment());
            }
            
            // 安装依赖包
            LogInfo("安装Python依赖包...");
            yield return StartCoroutine(InstallPythonPackages());
            
            // 重新检查环境
            yield return StartCoroutine(CheckEnvironmentAsync());
        }
        
        /// <summary>
        /// 创建虚拟环境
        /// </summary>
        private IEnumerator CreateVirtualEnvironment()
        {
            bool success = false;
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "python";
                startInfo.Arguments = $"-m venv {virtualEnvPath}";
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                    
                    success = (process.ExitCode == 0);
                }
            }
            catch (Exception e)
            {
                LogError($"创建虚拟环境错误: {e.Message}");
                success = false;
            }
            
            if (success)
            {
                LogInfo("虚拟环境创建成功");
            }
            else
            {
                LogError("虚拟环境创建失败");
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 安装Python包
        /// </summary>
        private IEnumerator InstallPythonPackages()
        {
            bool success = false;
            try
            {
                if (File.Exists(requirementsPath))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = "pip";
                    startInfo.Arguments = $"install -r {requirementsPath}";
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = true;
                    
                    using (Process process = new Process())
                    {
                        process.StartInfo = startInfo;
                        process.Start();
                        process.WaitForExit();
                        
                        success = (process.ExitCode == 0);
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"安装Python包错误: {e.Message}");
                success = false;
            }
            
            if (success)
            {
                LogInfo("Python依赖包安装成功");
            }
            else
            {
                LogError("Python依赖包安装失败");
            }
            
            yield return null;
        }
        
        #endregion
        
        #region 日志系统
        
        private void LogInfo(string message)
        {
            if (enableLogging)
            {
                UnityEngine.Debug.Log($"[EnvironmentSetup] {message}");
            }
        }
        
        private void LogWarning(string message)
        {
            if (enableLogging)
            {
                UnityEngine.Debug.LogWarning($"[EnvironmentSetup] {message}");
            }
        }
        
        private void LogError(string message)
        {
            if (enableLogging)
            {
                UnityEngine.Debug.LogError($"[EnvironmentSetup] {message}");
            }
        }
        
        #endregion
    }
}
