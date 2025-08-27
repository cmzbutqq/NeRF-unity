using System.Collections.Generic;
using UnityEngine;

namespace DataCapture
{
    /// <summary>
    /// 管理3DGS数据采集用的多摄像机位置
    /// </summary>
    public class CameraPositionManager : MonoBehaviour
    {
        [Header("摄像机设置")]
        [SerializeField] private Camera cameraTemplate;
        [SerializeField] private Transform targetCenter; // 场景中心点
        
        [Header("多半径设置")]
        [SerializeField] private float[] radiusArray = { 4f, 8f, 12f, 16f }; // 多个半径值
        [SerializeField] private int[] camerasPerRadius = { 8, 12, 8, 6 }; // 每个半径的摄像机数量
        
        [Header("位置参数")]
        [SerializeField] private int verticalLevels = 3; // 垂直层数
        [SerializeField] private float minHeight = 1f; // 最低高度
        [SerializeField] private float maxHeight = 6f; // 最高高度
        
        [Header("调试")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color gizmoColor = Color.yellow;
        
        private List<CameraPosition> cameraPositions = new List<CameraPosition>();
        
        [System.Serializable]
        public class CameraPosition
        {
            public Vector3 position;
            public Quaternion rotation;
            public string name;
            public int index;
            public float radius; // 添加半径信息
            
            public CameraPosition(Vector3 pos, Quaternion rot, string n, int idx, float r)
            {
                position = pos;
                rotation = rot;
                name = n;
                index = idx;
                radius = r;
            }
        }
        
        void Start()
        {
            if (targetCenter == null)
            {
                // 如果没有指定目标，使用场景中心
                targetCenter = new GameObject("SceneCenter").transform;
                targetCenter.position = Vector3.zero;
            }
            
            // 验证半径数组设置
            ValidateRadiusSettings();
            
            GenerateCameraPositions();
        }
        
        /// <summary>
        /// 验证半径数组设置
        /// </summary>
        private void ValidateRadiusSettings()
        {
            if (radiusArray == null || radiusArray.Length == 0)
            {
                Debug.LogWarning("半径数组为空，使用默认半径设置");
                radiusArray = new float[] { 8f };
                camerasPerRadius = new int[] { 12 };
                return;
            }
            
            if (camerasPerRadius == null || camerasPerRadius.Length != radiusArray.Length)
            {
                Debug.LogWarning("摄像机数量数组与半径数组长度不匹配，自动调整");
                camerasPerRadius = new int[radiusArray.Length];
                for (int i = 0; i < radiusArray.Length; i++)
                {
                    // 根据半径大小自动分配摄像机数量
                    camerasPerRadius[i] = Mathf.Max(6, Mathf.RoundToInt(12f * (radiusArray[i] / 8f)));
                }
            }
            
            // 确保每个半径至少有6个摄像机
            for (int i = 0; i < camerasPerRadius.Length; i++)
            {
                camerasPerRadius[i] = Mathf.Max(6, camerasPerRadius[i]);
            }
        }
        
        /// <summary>
        /// 生成摄像机位置
        /// </summary>
        [ContextMenu("生成摄像机位置")]
        public void GenerateCameraPositions()
        {
            cameraPositions.Clear();
            
            Vector3 center = targetCenter.position;
            int totalIndex = 0;
            
            // 为每个半径生成摄像机位置
            for (int radiusIndex = 0; radiusIndex < radiusArray.Length; radiusIndex++)
            {
                float currentRadius = radiusArray[radiusIndex];
                int currentRadiusCameraCount = camerasPerRadius[radiusIndex];
                
                // 生成多层环绕位置
                for (int level = 0; level < verticalLevels; level++)
                {
                    float height = Mathf.Lerp(minHeight, maxHeight, (float)level / (verticalLevels - 1));
                    
                    // 每层的摄像机数量可以不同，上层少一些
                    int currentLevelCount = currentRadiusCameraCount - level * 2;
                    currentLevelCount = Mathf.Max(currentLevelCount, 4); // 最少4个
                    
                    for (int i = 0; i < currentLevelCount; i++)
                    {
                        float angle = (float)i / currentLevelCount * 360f;
                        float radians = angle * Mathf.Deg2Rad;
                        
                        // 计算位置
                        Vector3 position = center + new Vector3(
                            Mathf.Cos(radians) * currentRadius,
                            height,
                            Mathf.Sin(radians) * currentRadius
                        );
                        
                        // 计算朝向（看向中心点）
                        Vector3 direction = (center - position).normalized;
                        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                        
                        string name = $"Camera_R{radiusIndex}_L{level}_H{i:D2}";
                        
                        cameraPositions.Add(new CameraPosition(position, rotation, name, totalIndex, currentRadius));
                        totalIndex++;
                    }
                }
            }
            
            // 添加一些特殊角度的摄像机
            AddSpecialCameraPositions(center, ref totalIndex);
            
            Debug.Log($"生成了 {cameraPositions.Count} 个摄像机位置，使用 {radiusArray.Length} 个不同半径");
        }
        
        /// <summary>
        /// 添加特殊角度的摄像机位置
        /// </summary>
        private void AddSpecialCameraPositions(Vector3 center, ref int totalIndex)
        {
            // 顶部俯视
            Vector3 topPosition = center + Vector3.up * (maxHeight + 2f);
            Quaternion topRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            cameraPositions.Add(new CameraPosition(topPosition, topRotation, "Camera_Top", totalIndex++, 0f));
            
            // 底部仰视（如果合理的话）
            if (minHeight > 0.5f)
            {
                Vector3 bottomPosition = center + Vector3.up * 0.3f;
                Quaternion bottomRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                cameraPositions.Add(new CameraPosition(bottomPosition, bottomRotation, "Camera_Bottom", totalIndex++, 0f));
            }
            
            // 几个近距离特写位置（使用最小半径）
            float minRadius = radiusArray.Length > 0 ? radiusArray[0] * 0.5f : 2f;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                float radians = angle * Mathf.Deg2Rad;
                
                Vector3 position = center + new Vector3(
                    Mathf.Cos(radians) * minRadius,
                    minHeight + 1f,
                    Mathf.Sin(radians) * minRadius
                );
                
                Vector3 direction = (center - position).normalized;
                Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                
                cameraPositions.Add(new CameraPosition(position, rotation, $"Camera_Close_{i}", totalIndex++, minRadius));
            }
        }
        
        /// <summary>
        /// 获取所有摄像机位置
        /// </summary>
        public List<CameraPosition> GetCameraPositions()
        {
            return new List<CameraPosition>(cameraPositions);
        }
        
        /// <summary>
        /// 获取指定半径的摄像机位置
        /// </summary>
        public List<CameraPosition> GetCameraPositionsByRadius(float radius)
        {
            List<CameraPosition> result = new List<CameraPosition>();
            foreach (var pos in cameraPositions)
            {
                if (Mathf.Approximately(pos.radius, radius))
                {
                    result.Add(pos);
                }
            }
            return result;
        }
        
        /// <summary>
        /// 获取所有使用的半径值
        /// </summary>
        public float[] GetUsedRadii()
        {
            return (float[])radiusArray.Clone();
        }
        
        /// <summary>
        /// 设置摄像机到指定位置
        /// </summary>
        public void SetCameraToPosition(Camera camera, int index)
        {
            if (index >= 0 && index < cameraPositions.Count)
            {
                var pos = cameraPositions[index];
                camera.transform.position = pos.position;
                camera.transform.rotation = pos.rotation;
            }
        }
        
        /// <summary>
        /// 创建预览摄像机
        /// </summary>
        [ContextMenu("创建预览摄像机")]
        public void CreatePreviewCamera()
        {
            if (cameraTemplate == null)
            {
                // 创建摄像机模板
                GameObject cameraObj = new GameObject("PreviewCamera");
                cameraTemplate = cameraObj.AddComponent<Camera>();
                cameraTemplate.fieldOfView = 60f;
                cameraTemplate.nearClipPlane = 0.1f;
                cameraTemplate.farClipPlane = 100f;
            }
            
            if (cameraPositions.Count > 0)
            {
                SetCameraToPosition(cameraTemplate, 0);
            }
        }
        
        void OnDrawGizmos()
        {
            if (!showGizmos || cameraPositions == null) return;
            
            Vector3 center = targetCenter != null ? targetCenter.position : Vector3.zero;
            
            // 绘制中心点
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, 0.5f);
            
            // 为每个半径绘制不同颜色的圆圈
            Color[] radiusColors = { Color.yellow, Color.green, Color.blue, Color.magenta, Color.cyan };
            
            for (int radiusIndex = 0; radiusIndex < radiusArray.Length; radiusIndex++)
            {
                Color circleColor = radiusColors[radiusIndex % radiusColors.Length];
                Gizmos.color = circleColor;
                
                // 绘制每个垂直层的圆圈
                for (int level = 0; level < verticalLevels; level++)
                {
                    float height = Mathf.Lerp(minHeight, maxHeight, (float)level / (verticalLevels - 1));
                    Vector3 circleCenter = center + Vector3.up * height;
                    
                    // 绘制圆圈
                    int segments = 32;
                    for (int i = 0; i < segments; i++)
                    {
                        float angle1 = (float)i / segments * 360f * Mathf.Deg2Rad;
                        float angle2 = (float)(i + 1) / segments * 360f * Mathf.Deg2Rad;
                        
                        Vector3 point1 = circleCenter + new Vector3(Mathf.Cos(angle1) * radiusArray[radiusIndex], 0, Mathf.Sin(angle1) * radiusArray[radiusIndex]);
                        Vector3 point2 = circleCenter + new Vector3(Mathf.Cos(angle2) * radiusArray[radiusIndex], 0, Mathf.Sin(angle2) * radiusArray[radiusIndex]);
                        
                        Gizmos.DrawLine(point1, point2);
                    }
                }
            }
            
            // 绘制摄像机位置
            foreach (var pos in cameraPositions)
            {
                // 根据半径选择颜色
                int radiusIndex = System.Array.IndexOf(radiusArray, pos.radius);
                if (radiusIndex >= 0)
                {
                    Gizmos.color = radiusColors[radiusIndex % radiusColors.Length];
                }
                else
                {
                    Gizmos.color = Color.white; // 特殊位置使用白色
                }
                
                Gizmos.DrawWireCube(pos.position, Vector3.one * 0.3f);
                Gizmos.DrawLine(pos.position, center);
            }
        }
        
        /// <summary>
        /// 在Inspector中显示统计信息
        /// </summary>
        void OnValidate()
        {
            if (Application.isPlaying) return;
            
            // 计算总摄像机数量
            int totalCameras = 0;
            if (radiusArray != null && camerasPerRadius != null)
            {
                for (int i = 0; i < Mathf.Min(radiusArray.Length, camerasPerRadius.Length); i++)
                {
                    totalCameras += camerasPerRadius[i] * verticalLevels;
                }
            }
            
            // 添加特殊摄像机
            totalCameras += 6; // 顶部、底部、4个近距离特写
            
            Debug.Log($"预计生成 {totalCameras} 个摄像机位置");
        }
    }
}
