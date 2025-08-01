using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DataCapture
{
    /// <summary>
    /// 场景数据导出器 - 用于3DGS训练数据采集
    /// </summary>
    public class SceneDataExporter : MonoBehaviour
    {
        [Header("导出设置")]
        [SerializeField] private string exportPath = "captured_data";
        [SerializeField] private int imageWidth = 1920;
        [SerializeField] private int imageHeight = 1080;
        [SerializeField] private int imageQuality = 95;
        
        [Header("组件引用")]
        [SerializeField] private CameraPositionManager cameraManager;
        [SerializeField] private Camera renderCamera;
        
        [Header("导出选项")]
        [SerializeField] private bool exportRGBImages = true;
        [SerializeField] private bool exportDepthMaps = true;
        [SerializeField] private bool exportCameraParams = true;
        
        [Header("调试")]
        [SerializeField] private bool showProgress = true;
        
        private RenderTexture colorRT;
        private RenderTexture depthRT;
        private Texture2D colorTexture;
        private Texture2D depthTexture;
        
        // 摄像机参数数据
        [System.Serializable]
        public class CameraData
        {
            public int imageId;
            public string imageName;
            public Vector3 position;
            public Quaternion rotation;
            public Matrix4x4 projectionMatrix;
            public Matrix4x4 worldToCameraMatrix;
            public float fieldOfView;
            public float nearClipPlane;
            public float farClipPlane;
            public float aspect;
        }
        
        private List<CameraData> cameraDataList = new List<CameraData>();
        
        void Start()
        {
            // 自动查找组件
            if (cameraManager == null)
                cameraManager = FindObjectOfType<CameraPositionManager>();
                
            if (renderCamera == null)
                renderCamera = Camera.main;
                
            if (renderCamera == null)
            {
                Debug.LogError("未找到渲染摄像机！");
                return;
            }
            
            SetupRenderTextures();
        }
        
        /// <summary>
        /// 设置渲染纹理
        /// </summary>
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
            depthTexture = new Texture2D(imageWidth, imageHeight, TextureFormat.RFloat, false);
        }
        
        /// <summary>
        /// 开始导出数据
        /// </summary>
        [ContextMenu("开始导出数据")]
        public void StartExport()
        {
            StartCoroutine(ExportDataCoroutine());
        }
        
        /// <summary>
        /// 导出数据协程
        /// </summary>
        IEnumerator ExportDataCoroutine()
        {
            if (cameraManager == null)
            {
                Debug.LogError("CameraPositionManager 未设置！");
                yield break;
            }
            
            // 创建导出目录
            string fullExportPath = Path.Combine(Application.dataPath, "..", exportPath);
            CreateDirectories(fullExportPath);
            
            var cameraPositions = cameraManager.GetCameraPositions();
            cameraDataList.Clear();
            
            Debug.Log($"开始导出 {cameraPositions.Count} 个摄像机位置的数据...");
            
            for (int i = 0; i < cameraPositions.Count; i++)
            {
                var camPos = cameraPositions[i];
                
                if (showProgress)
                {
                    float progress = (float)i / cameraPositions.Count;
                    Debug.Log($"导出进度: {i + 1}/{cameraPositions.Count} ({progress * 100:F1}%)");
                }
                
                // 设置摄像机位置
                renderCamera.transform.position = camPos.position;
                renderCamera.transform.rotation = camPos.rotation;
                
                // 等待一帧确保渲染完成
                yield return new WaitForEndOfFrame();
                
                // 导出数据
                string imageName = $"{camPos.name}_{i:D4}";
                
                if (exportRGBImages)
                {
                    yield return StartCoroutine(CaptureRGBImage(fullExportPath, imageName));
                }
                
                if (exportDepthMaps)
                {
                    yield return StartCoroutine(CaptureDepthMap(fullExportPath, imageName));
                }
                
                if (exportCameraParams)
                {
                    CaptureCameraParameters(i, imageName, camPos);
                }
            }
            
            // 导出摄像机参数文件
            if (exportCameraParams)
            {
                ExportCameraParameters(fullExportPath);
            }
            
            Debug.Log($"数据导出完成！导出路径: {fullExportPath}");
        }
        
        /// <summary>
        /// 创建必要的目录
        /// </summary>
        void CreateDirectories(string basePath)
        {
            Directory.CreateDirectory(basePath);
            Directory.CreateDirectory(Path.Combine(basePath, "images"));
            Directory.CreateDirectory(Path.Combine(basePath, "depth"));
            Directory.CreateDirectory(Path.Combine(basePath, "sparse"));
        }
        
        /// <summary>
        /// 捕获RGB图像
        /// </summary>
        IEnumerator CaptureRGBImage(string basePath, string imageName)
        {
            // 设置摄像机渲染到纹理
            RenderTexture previousRT = renderCamera.targetTexture;
            renderCamera.targetTexture = colorRT;
            
            // 渲染
            renderCamera.Render();
            
            // 读取像素
            RenderTexture.active = colorRT;
            colorTexture.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            colorTexture.Apply();
            
            // 保存为JPG
            byte[] imageData = colorTexture.EncodeToJPG(imageQuality);
            string imagePath = Path.Combine(basePath, "images", $"{imageName}.jpg");
            File.WriteAllBytes(imagePath, imageData);
            
            // 恢复摄像机设置
            renderCamera.targetTexture = previousRT;
            RenderTexture.active = null;
            
            yield return null;
        }
        
        /// <summary>
        /// 捕获深度图
        /// </summary>
        IEnumerator CaptureDepthMap(string basePath, string imageName)
        {
            string depthPath = Path.Combine(basePath, "depth", $"{imageName}_depth.png");

            // 使用Unity内置的深度渲染
            RenderTexture previousRT = renderCamera.targetTexture;
            RenderTexture depthBuffer = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.Depth);
            depthBuffer.Create();

            // 创建临时摄像机用于深度渲染
            GameObject tempCamObj = new GameObject("TempDepthCamera");
            Camera depthCam = tempCamObj.AddComponent<Camera>();

            // 复制主摄像机设置
            depthCam.CopyFrom(renderCamera);
            depthCam.backgroundColor = Color.white;
            depthCam.clearFlags = CameraClearFlags.SolidColor;
            depthCam.targetTexture = depthBuffer;

            // 设置深度渲染模式
            depthCam.SetReplacementShader(Shader.Find("Hidden/Internal-DepthNormalsTexture"), "");

            // 渲染深度
            depthCam.Render();

            // 读取深度数据
            RenderTexture.active = depthBuffer;

            // 创建用于读取深度的纹理
            Texture2D depthTex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            depthTex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            depthTex.Apply();

            // 处理深度数据 - 转换为更可视化的格式
            Color[] pixels = depthTex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                // 从深度法线纹理中提取深度信息
                float depth = pixels[i].r;
                // 增强对比度使深度更明显
                depth = Mathf.Pow(depth, 0.5f);
                pixels[i] = new Color(depth, depth, depth, 1.0f);
            }
            depthTex.SetPixels(pixels);
            depthTex.Apply();

            // 保存深度图
            byte[] depthData = depthTex.EncodeToPNG();
            File.WriteAllBytes(depthPath, depthData);

            // 清理资源
            RenderTexture.active = null;
            renderCamera.targetTexture = previousRT;
            depthBuffer.Release();
            DestroyImmediate(depthTex);
            DestroyImmediate(tempCamObj);

            yield return null;
        }
        
        /// <summary>
        /// 捕获摄像机参数
        /// </summary>
        void CaptureCameraParameters(int imageId, string imageName, CameraPositionManager.CameraPosition camPos)
        {
            CameraData data = new CameraData
            {
                imageId = imageId,
                imageName = imageName,
                position = camPos.position,
                rotation = camPos.rotation,
                projectionMatrix = renderCamera.projectionMatrix,
                worldToCameraMatrix = renderCamera.worldToCameraMatrix,
                fieldOfView = renderCamera.fieldOfView,
                nearClipPlane = renderCamera.nearClipPlane,
                farClipPlane = renderCamera.farClipPlane,
                aspect = renderCamera.aspect
            };
            
            cameraDataList.Add(data);
        }
        
        /// <summary>
        /// 导出摄像机参数到文件
        /// </summary>
        void ExportCameraParameters(string basePath)
        {
            // 导出为JSON格式（便于调试）
            string jsonPath = Path.Combine(basePath, "cameras.json");
            string jsonData = JsonUtility.ToJson(new CameraDataWrapper { cameras = cameraDataList }, true);
            File.WriteAllText(jsonPath, jsonData);
            
            // 导出为COLMAP格式（下一步实现）
            // ExportCOLMAPFormat(basePath);
        }
        
        [System.Serializable]
        public class CameraDataWrapper
        {
            public List<CameraData> cameras;
        }
        
        void OnDestroy()
        {
            // 清理资源
            if (colorRT != null) colorRT.Release();
            if (depthRT != null) depthRT.Release();
            if (colorTexture != null) DestroyImmediate(colorTexture);
            if (depthTexture != null) DestroyImmediate(depthTexture);
        }
    }
}
