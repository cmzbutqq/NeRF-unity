using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DataCapture
{
    /// <summary>
    /// 混合数据采集系统 - 支持手动采集(C键) + 自动时间间隔采集
    /// </summary>
    public class HybridDataCapture : MonoBehaviour
    {
        [Header("采集模式")]
        [SerializeField] private bool enableManualCapture = true;
        [SerializeField] private bool enableAutoCapture = true;
        [SerializeField] private KeyCode manualCaptureKey = KeyCode.C;
        
        [Header("自动采集设置")]
        [SerializeField] private float autoInterval = 3f; // 自动采集间隔(秒)
        [SerializeField] private float minMoveDistance = 1f; // 最小移动距离才触发自动采集
        
        [Header("导出设置")]
        [SerializeField] private string exportPath = "hybrid_captured_data";
        [SerializeField] private int imageWidth = 1920;
        [SerializeField] private int imageHeight = 1080;
        [SerializeField] private int imageQuality = 95;
        
        [Header("组件引用")]
        [SerializeField] private Camera captureCamera;
        
        [Header("调试信息")]
        [SerializeField] private bool showDebugInfo = true;
        
        // 内部状态
        private float lastAutoCapture = 0f;
        private Vector3 lastCapturePosition;
        private int captureCount = 0;
        private bool isCapturing = false;
        
        // 采集数据
        private List<HybridCameraData> captureDataList = new List<HybridCameraData>();
        
        // 渲染纹理
        private RenderTexture colorRT;
        private RenderTexture depthRT;
        private Texture2D colorTexture;
        private Material depthMaterial;
        
        [System.Serializable]
        public class HybridCameraData
        {
            public int captureId;
            public string imageName;
            public Vector3 position;
            public Quaternion rotation;
            public float timestamp;
            public CaptureSource source;
            public Matrix4x4 projectionMatrix;
            public Matrix4x4 worldToCameraMatrix;
            public float fieldOfView;
        }
        
        public enum CaptureSource
        {
            Manual,     // C键手动采集
            AutoTime    // 时间间隔自动采集
        }
        
        void Start()
        {
            InitializeCapture();
        }
        
        void Update()
        {
            HandleInput();
            HandleAutoCapture();
            
            if (showDebugInfo)
            {
                UpdateDebugInfo();
            }
        }
        
        void InitializeCapture()
        {
            // 查找摄像机
            if (captureCamera == null)
            {
                captureCamera = Camera.main;
                if (captureCamera == null)
                {
                    captureCamera = FindObjectOfType<Camera>();
                }
            }
            
            if (captureCamera == null)
            {
                Debug.LogError("未找到采集摄像机！");
                enabled = false;
                return;
            }
            
            // 设置渲染纹理
            SetupRenderTextures();
            
            // 初始化位置
            lastCapturePosition = captureCamera.transform.position;
            
            Debug.Log("混合数据采集系统已初始化");
        }
        
        void SetupRenderTextures()
        {
            // RGB渲染纹理
            colorRT = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.ARGB32);
            colorRT.Create();
            
            // 深度渲染纹理
            depthRT = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.RFloat);
            depthRT.Create();
            
            // CPU纹理
            colorTexture = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            
            // 深度材质
            Shader depthShader = Shader.Find("Hidden/DepthVisualization");
            if (depthShader != null)
            {
                depthMaterial = new Material(depthShader);
            }
        }
        
        void HandleInput()
        {
            if (!enableManualCapture) return;
            
            // C键手动采集
            if (Input.GetKeyDown(manualCaptureKey))
            {
                StartCoroutine(CaptureFrame(CaptureSource.Manual));
            }
        }
        
        void HandleAutoCapture()
        {
            if (!enableAutoCapture || isCapturing) return;
            
            float currentTime = Time.time;
            
            // 检查时间间隔
            if (currentTime - lastAutoCapture >= autoInterval)
            {
                // 检查移动距离
                float moveDistance = Vector3.Distance(captureCamera.transform.position, lastCapturePosition);
                
                if (moveDistance >= minMoveDistance)
                {
                    StartCoroutine(CaptureFrame(CaptureSource.AutoTime));
                    lastAutoCapture = currentTime;
                }
            }
        }
        
        /// <summary>
        /// 采集单帧数据
        /// </summary>
        IEnumerator CaptureFrame(CaptureSource source)
        {
            if (isCapturing) yield break;
            
            isCapturing = true;
            
            // 创建导出目录
            string fullExportPath = Path.Combine(Application.dataPath, "..", exportPath);
            CreateDirectories(fullExportPath);
            
            // 生成文件名
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string sourcePrefix = source == CaptureSource.Manual ? "Manual" : "Auto";
            string imageName = $"{sourcePrefix}_{timestamp}_{captureCount:D4}";
            
            // 采集RGB图像
            yield return StartCoroutine(CaptureRGBImage(fullExportPath, imageName));
            
            // 采集深度图
            if (depthMaterial != null)
            {
                yield return StartCoroutine(CaptureDepthMap(fullExportPath, imageName));
            }
            
            // 记录摄像机数据
            RecordCameraData(imageName, source);
            
            // 更新状态
            captureCount++;
            lastCapturePosition = captureCamera.transform.position;
            
            Debug.Log($"[{source}] 采集完成: {imageName} (总计: {captureCount})");
            
            isCapturing = false;
        }
        
        /// <summary>
        /// 采集RGB图像
        /// </summary>
        IEnumerator CaptureRGBImage(string basePath, string imageName)
        {
            RenderTexture previousRT = captureCamera.targetTexture;
            captureCamera.targetTexture = colorRT;
            
            captureCamera.Render();
            
            RenderTexture.active = colorRT;
            colorTexture.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            colorTexture.Apply();
            
            byte[] imageData = colorTexture.EncodeToJPG(imageQuality);
            string imagePath = Path.Combine(basePath, "images", $"{imageName}.jpg");
            File.WriteAllBytes(imagePath, imageData);
            
            captureCamera.targetTexture = previousRT;
            RenderTexture.active = null;
            
            yield return null;
        }
        
        /// <summary>
        /// 采集深度图
        /// </summary>
        IEnumerator CaptureDepthMap(string basePath, string imageName)
        {
            RenderTexture previousRT = captureCamera.targetTexture;
            DepthTextureMode previousDepthMode = captureCamera.depthTextureMode;
            
            captureCamera.depthTextureMode = DepthTextureMode.Depth;
            captureCamera.targetTexture = colorRT;
            
            captureCamera.Render();
            
            RenderTexture depthVisualization = new RenderTexture(imageWidth, imageHeight, 0, RenderTextureFormat.ARGB32);
            depthVisualization.Create();
            
            Graphics.Blit(colorRT, depthVisualization, depthMaterial);
            
            RenderTexture.active = depthVisualization;
            Texture2D depthTex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            depthTex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            depthTex.Apply();
            
            byte[] depthData = depthTex.EncodeToPNG();
            string depthPath = Path.Combine(basePath, "depth", $"{imageName}_depth.png");
            File.WriteAllBytes(depthPath, depthData);
            
            captureCamera.targetTexture = previousRT;
            captureCamera.depthTextureMode = previousDepthMode;
            RenderTexture.active = null;
            
            depthVisualization.Release();
            DestroyImmediate(depthTex);
            
            yield return null;
        }
        
        /// <summary>
        /// 记录摄像机数据
        /// </summary>
        void RecordCameraData(string imageName, CaptureSource source)
        {
            HybridCameraData data = new HybridCameraData
            {
                captureId = captureCount,
                imageName = imageName,
                position = captureCamera.transform.position,
                rotation = captureCamera.transform.rotation,
                timestamp = Time.time,
                source = source,
                projectionMatrix = captureCamera.projectionMatrix,
                worldToCameraMatrix = captureCamera.worldToCameraMatrix,
                fieldOfView = captureCamera.fieldOfView
            };
            
            captureDataList.Add(data);
        }
        
        /// <summary>
        /// 创建必要的目录
        /// </summary>
        void CreateDirectories(string basePath)
        {
            Directory.CreateDirectory(basePath);
            Directory.CreateDirectory(Path.Combine(basePath, "images"));
            Directory.CreateDirectory(Path.Combine(basePath, "depth"));
        }
        
        /// <summary>
        /// 更新调试信息
        /// </summary>
        void UpdateDebugInfo()
        {
            // 这里可以添加屏幕调试信息显示
        }
        
        /// <summary>
        /// 导出所有数据
        /// </summary>
        [ContextMenu("导出所有采集数据")]
        public void ExportAllData()
        {
            if (captureDataList.Count == 0)
            {
                Debug.LogWarning("没有采集数据可导出");
                return;
            }

            string fullExportPath = Path.Combine(Application.dataPath, "..", exportPath);

            // 导出JSON格式的摄像机数据
            string jsonPath = Path.Combine(fullExportPath, "hybrid_cameras.json");
            string jsonData = JsonUtility.ToJson(new HybridCameraDataWrapper { cameras = captureDataList }, true);
            File.WriteAllText(jsonPath, jsonData);

            // 导出COLMAP格式数据
            ExportHybridCOLMAPFormat(fullExportPath);

            // 生成数据集摘要
            GenerateHybridDatasetSummary(fullExportPath);

            Debug.Log($"混合采集数据已导出: {fullExportPath} (共{captureDataList.Count}帧)");
        }
        
        /// <summary>
        /// 导出COLMAP格式数据
        /// </summary>
        void ExportHybridCOLMAPFormat(string basePath)
        {
            string sparsePath = Path.Combine(basePath, "sparse", "0");
            Directory.CreateDirectory(sparsePath);

            // 转换为COLMAP兼容的数据格式
            List<SceneDataExporter.CameraData> colmapData = ConvertToColmapFormat();

            // 使用现有的COLMAP导出器
            COLMAPExporter.ExportCOLMAPFormat(basePath, colmapData);

            Debug.Log($"混合采集COLMAP格式已导出到: {sparsePath}");
        }

        /// <summary>
        /// 转换混合采集数据为COLMAP格式
        /// </summary>
        List<SceneDataExporter.CameraData> ConvertToColmapFormat()
        {
            List<SceneDataExporter.CameraData> colmapData = new List<SceneDataExporter.CameraData>();

            for (int i = 0; i < captureDataList.Count; i++)
            {
                var hybridData = captureDataList[i];

                SceneDataExporter.CameraData colmapCam = new SceneDataExporter.CameraData
                {
                    imageId = hybridData.captureId,
                    imageName = hybridData.imageName,
                    position = hybridData.position,
                    rotation = hybridData.rotation,
                    projectionMatrix = hybridData.projectionMatrix,
                    worldToCameraMatrix = hybridData.worldToCameraMatrix,
                    fieldOfView = hybridData.fieldOfView,
                    nearClipPlane = captureCamera.nearClipPlane,
                    farClipPlane = captureCamera.farClipPlane,
                    aspect = (float)imageWidth / imageHeight
                };

                colmapData.Add(colmapCam);
            }

            return colmapData;
        }

        /// <summary>
        /// 生成混合采集数据集摘要
        /// </summary>
        void GenerateHybridDatasetSummary(string basePath)
        {
            string summaryPath = Path.Combine(basePath, "hybrid_dataset_summary.txt");

            // 统计采集来源
            int manualCount = 0;
            int autoCount = 0;

            foreach (var data in captureDataList)
            {
                if (data.source == CaptureSource.Manual)
                    manualCount++;
                else
                    autoCount++;
            }

            using (StreamWriter writer = new StreamWriter(summaryPath))
            {
                writer.WriteLine("=== 混合3DGS训练数据集摘要 ===");
                writer.WriteLine($"生成时间: {System.DateTime.Now}");
                writer.WriteLine($"总图像数量: {captureDataList.Count}");
                writer.WriteLine($"  手动采集: {manualCount}");
                writer.WriteLine($"  自动采集: {autoCount}");
                writer.WriteLine($"图像分辨率: {imageWidth}x{imageHeight}");
                writer.WriteLine($"摄像机模型: PINHOLE");
                writer.WriteLine();

                if (captureDataList.Count > 0)
                {
                    var firstCam = captureDataList[0];
                    writer.WriteLine("摄像机参数:");
                    writer.WriteLine($"  视场角: {firstCam.fieldOfView:F2}°");
                    writer.WriteLine($"  近裁剪面: {captureCamera.nearClipPlane:F3}");
                    writer.WriteLine($"  远裁剪面: {captureCamera.farClipPlane:F3}");
                    writer.WriteLine($"  宽高比: {(float)imageWidth / imageHeight:F3}");
                }

                writer.WriteLine();
                writer.WriteLine("采集设置:");
                writer.WriteLine($"  自动采集间隔: {autoInterval}秒");
                writer.WriteLine($"  最小移动距离: {minMoveDistance}米");
                writer.WriteLine($"  手动采集按键: {manualCaptureKey}");

                writer.WriteLine();
                writer.WriteLine("文件结构:");
                writer.WriteLine("  images/              - RGB图像");
                writer.WriteLine("  depth/               - 深度图");
                writer.WriteLine("  sparse/0/            - COLMAP格式数据");
                writer.WriteLine("    cameras.txt        - 摄像机内参");
                writer.WriteLine("    images.txt         - 图像外参");
                writer.WriteLine("    points3D.txt       - 3D点云（空）");
                writer.WriteLine("  hybrid_cameras.json  - Unity摄像机数据");

                writer.WriteLine();
                writer.WriteLine("采集时间线:");
                foreach (var data in captureDataList)
                {
                    string sourceStr = data.source == CaptureSource.Manual ? "手动" : "自动";
                    writer.WriteLine($"  {data.timestamp:F2}s - {data.imageName} ({sourceStr})");
                }
            }

            Debug.Log($"混合采集数据集摘要已生成: {summaryPath}");
        }

        [System.Serializable]
        public class HybridCameraDataWrapper
        {
            public List<HybridCameraData> cameras;
        }
        
        void OnDestroy()
        {
            // 清理资源
            if (colorRT != null) colorRT.Release();
            if (depthRT != null) depthRT.Release();
            if (colorTexture != null) DestroyImmediate(colorTexture);
            if (depthMaterial != null) DestroyImmediate(depthMaterial);
        }
        
        void OnGUI()
        {
            if (!showDebugInfo) return;
            
            // 显示调试信息
            GUI.Box(new Rect(10, 10, 300, 120), "混合数据采集");
            GUI.Label(new Rect(20, 35, 280, 20), $"采集总数: {captureCount}");
            GUI.Label(new Rect(20, 55, 280, 20), $"手动采集: {manualCaptureKey} 键");
            GUI.Label(new Rect(20, 75, 280, 20), $"自动间隔: {autoInterval:F1}秒");
            GUI.Label(new Rect(20, 95, 280, 20), $"状态: {(isCapturing ? "采集中..." : "就绪")}");
        }
    }
}
