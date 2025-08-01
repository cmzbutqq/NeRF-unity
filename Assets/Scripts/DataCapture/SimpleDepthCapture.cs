using UnityEngine;
using UnityEngine.Rendering;

namespace DataCapture
{
    /// <summary>
    /// 简单的深度图捕获组件
    /// </summary>
    public class SimpleDepthCapture : MonoBehaviour
    {
        [Header("深度捕获设置")]
        public Camera targetCamera;
        public int textureWidth = 1920;
        public int textureHeight = 1080;
        
        private RenderTexture depthTexture;
        private Material depthMaterial;
        private Shader depthShader;
        
        void Start()
        {
            SetupDepthCapture();
        }
        
        void SetupDepthCapture()
        {
            // 创建深度渲染纹理
            depthTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
            depthTexture.Create();
            
            // 查找或创建深度shader
            depthShader = Shader.Find("Custom/DepthCapture");
            if (depthShader == null)
            {
                Debug.LogWarning("未找到深度Shader，使用内置方案");
            }
            else
            {
                depthMaterial = new Material(depthShader);
            }
        }
        
        /// <summary>
        /// 捕获深度图并返回Texture2D
        /// </summary>
        public Texture2D CaptureDepthTexture()
        {
            if (targetCamera == null)
            {
                Debug.LogError("目标摄像机未设置！");
                return null;
            }
            
            // 方法1：使用替换shader渲染深度
            if (depthMaterial != null)
            {
                return CaptureWithReplacementShader();
            }
            
            // 方法2：使用Unity内置深度缓冲
            return CaptureWithBuiltinDepth();
        }
        
        Texture2D CaptureWithReplacementShader()
        {
            RenderTexture previousRT = targetCamera.targetTexture;
            targetCamera.targetTexture = depthTexture;
            
            // 使用替换shader渲染
            targetCamera.RenderWithShader(depthShader, "RenderType");
            
            // 读取结果
            RenderTexture.active = depthTexture;
            Texture2D result = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
            result.Apply();
            
            // 恢复设置
            targetCamera.targetTexture = previousRT;
            RenderTexture.active = null;
            
            return result;
        }
        
        Texture2D CaptureWithBuiltinDepth()
        {
            // 创建临时摄像机
            GameObject tempObj = new GameObject("TempDepthCamera");
            Camera depthCam = tempObj.AddComponent<Camera>();
            
            // 复制设置
            depthCam.CopyFrom(targetCamera);
            depthCam.targetTexture = depthTexture;
            depthCam.backgroundColor = Color.white;
            depthCam.clearFlags = CameraClearFlags.SolidColor;
            
            // 设置深度渲染模式
            depthCam.depthTextureMode = DepthTextureMode.Depth;
            
            // 渲染
            depthCam.Render();
            
            // 读取深度缓冲
            RenderTexture.active = depthTexture;
            Texture2D result = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
            result.Apply();
            
            // 后处理深度数据
            Color[] pixels = result.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                // 简单的深度可视化
                float depth = pixels[i].r;
                // 反转深度值（近处亮，远处暗）
                depth = 1.0f - depth;
                // 增强对比度
                depth = Mathf.Pow(depth, 2.0f);
                pixels[i] = new Color(depth, depth, depth, 1.0f);
            }
            result.SetPixels(pixels);
            result.Apply();
            
            // 清理
            RenderTexture.active = null;
            DestroyImmediate(tempObj);
            
            return result;
        }
        
        void OnDestroy()
        {
            if (depthTexture != null)
            {
                depthTexture.Release();
            }
            if (depthMaterial != null)
            {
                DestroyImmediate(depthMaterial);
            }
        }
    }
}
