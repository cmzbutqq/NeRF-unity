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

            // 保存原始设置
            RenderTexture previousRT = renderCamera.targetTexture;
            DepthTextureMode previousDepthMode = renderCamera.depthTextureMode;

            // 创建深度渲染纹理
            RenderTexture colorRT = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.ARGB32);
            colorRT.Create();

            // 启用深度纹理模式
            renderCamera.depthTextureMode = DepthTextureMode.Depth;
            renderCamera.targetTexture = colorRT;

            // 创建深度可视化材质
            Material depthMaterial = CreateDepthVisualizationMaterial();

            if (depthMaterial != null)
            {
                // 渲染场景到颜色纹理
                renderCamera.Render();

                // 使用深度材质将深度缓冲转换为可视化图像
                RenderTexture depthVisualization = new RenderTexture(imageWidth, imageHeight, 0, RenderTextureFormat.ARGB32);
                depthVisualization.Create();

                Graphics.Blit(colorRT, depthVisualization, depthMaterial);

                // 读取深度可视化结果
                RenderTexture.active = depthVisualization;
                Texture2D depthTex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
                depthTex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
                depthTex.Apply();

                // 后处理深度数据以增强可视化效果
                ProcessDepthTexture(depthTex);

                // 保存深度图
                byte[] depthData = depthTex.EncodeToPNG();
                File.WriteAllBytes(depthPath, depthData);

                // 清理资源
                RenderTexture.active = null;
                depthVisualization.Release();
                DestroyImmediate(depthTex);
                DestroyImmediate(depthMaterial);
            }
            else
            {
                // 备用方案：使用简单的距离计算
                yield return StartCoroutine(CaptureDepthMapFallback(basePath, imageName));
            }

            // 恢复原始设置
            renderCamera.targetTexture = previousRT;
            renderCamera.depthTextureMode = previousDepthMode;
            colorRT.Release();

            yield return null;
        }
        
        /// <summary>
        /// 创建深度可视化材质
        /// </summary>
        Material CreateDepthVisualizationMaterial()
        {
            // 创建深度可视化shader代码
            string shaderCode = @"
            Shader ""Hidden/DepthVisualization""
            {
                Properties
                {
                    _MainTex (""Texture"", 2D) = ""white"" {}
                }
                SubShader
                {
                    Tags { ""RenderType""=""Opaque"" }
                    Pass
                    {
                        CGPROGRAM
                        #pragma vertex vert
                        #pragma fragment frag
                        #include ""UnityCG.cginc""

                        struct appdata
                        {
                            float4 vertex : POSITION;
                            float2 uv : TEXCOORD0;
                        };

                        struct v2f
                        {
                            float2 uv : TEXCOORD0;
                            float4 vertex : SV_POSITION;
                        };

                        sampler2D _MainTex;
                        sampler2D _CameraDepthTexture;

                        v2f vert (appdata v)
                        {
                            v2f o;
                            o.vertex = UnityObjectToClipPos(v.vertex);
                            o.uv = v.uv;
                            return o;
                        }

                        fixed4 frag (v2f i) : SV_Target
                        {
                            // 读取深度值
                            float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                            // 转换为线性深度
                            depth = Linear01Depth(depth);
                            // 增强对比度
                            depth = pow(depth, 0.5);
                            // 反转深度（近处亮，远处暗）
                            depth = 1.0 - depth;

                            return fixed4(depth, depth, depth, 1.0);
                        }
                        ENDCG
                    }
                }
            }";

            Shader depthShader = Shader.Find("Hidden/DepthVisualization");
            if (depthShader == null)
            {
                // 如果shader不存在，尝试创建临时shader
                Debug.LogWarning("深度可视化Shader未找到，使用备用方案");
                return null;
            }

            return new Material(depthShader);
        }

        /// <summary>
        /// 处理深度纹理以增强可视化效果
        /// </summary>
        void ProcessDepthTexture(Texture2D depthTex)
        {
            Color[] pixels = depthTex.GetPixels();
            float minDepth = 1.0f;
            float maxDepth = 0.0f;

            // 找到深度范围
            for (int i = 0; i < pixels.Length; i++)
            {
                float depth = pixels[i].r;
                minDepth = Mathf.Min(minDepth, depth);
                maxDepth = Mathf.Max(maxDepth, depth);
            }

            Debug.Log($"深度范围: {minDepth:F3} - {maxDepth:F3}");

            // 重新映射深度值以增强对比度
            float depthRange = maxDepth - minDepth;
            if (depthRange > 0.001f) // 避免除零
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    float depth = pixels[i].r;
                    // 归一化到0-1范围
                    depth = (depth - minDepth) / depthRange;
                    // 应用伽马校正增强对比度
                    depth = Mathf.Pow(depth, 0.7f);
                    pixels[i] = new Color(depth, depth, depth, 1.0f);
                }

                depthTex.SetPixels(pixels);
                depthTex.Apply();
            }
        }

        /// <summary>
        /// 备用深度捕获方案
        /// </summary>
        IEnumerator CaptureDepthMapFallback(string basePath, string imageName)
        {
            string depthPath = Path.Combine(basePath, "depth", $"{imageName}_depth.png");

            // 使用简单的距离计算方案
            Texture2D depthTex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            Color[] pixels = new Color[imageWidth * imageHeight];

            // 获取摄像机参数
            Vector3 camPos = renderCamera.transform.position;
            float nearPlane = renderCamera.nearClipPlane;
            float farPlane = renderCamera.farClipPlane;

            // 简单的深度模拟（基于距离到摄像机的距离）
            for (int y = 0; y < imageHeight; y++)
            {
                for (int x = 0; x < imageWidth; x++)
                {
                    // 将屏幕坐标转换为世界射线
                    Vector3 screenPoint = new Vector3(
                        (float)x / imageWidth,
                        (float)y / imageHeight,
                        (nearPlane + farPlane) * 0.5f
                    );

                    Ray ray = renderCamera.ScreenPointToRay(screenPoint);

                    // 简单的深度估算
                    float depth = Vector3.Distance(camPos, ray.origin + ray.direction * 5f);
                    depth = Mathf.Clamp01((depth - nearPlane) / (farPlane - nearPlane));
                    depth = 1.0f - depth; // 反转

                    int index = y * imageWidth + x;
                    pixels[index] = new Color(depth, depth, depth, 1.0f);
                }
            }

            depthTex.SetPixels(pixels);
            depthTex.Apply();

            byte[] depthData = depthTex.EncodeToPNG();
            File.WriteAllBytes(depthPath, depthData);

            DestroyImmediate(depthTex);

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
