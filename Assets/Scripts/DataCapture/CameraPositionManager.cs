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
        
        [Header("位置参数")]
        [SerializeField] private float radius = 8f; // 环绕半径
        [SerializeField] private int horizontalCount = 12; // 水平方向摄像机数量
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
            
            public CameraPosition(Vector3 pos, Quaternion rot, string n, int idx)
            {
                position = pos;
                rotation = rot;
                name = n;
                index = idx;
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
            
            GenerateCameraPositions();
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
            
            // 生成多层环绕位置
            for (int level = 0; level < verticalLevels; level++)
            {
                float height = Mathf.Lerp(minHeight, maxHeight, (float)level / (verticalLevels - 1));
                
                // 每层的摄像机数量可以不同，上层少一些
                int currentLevelCount = horizontalCount - level * 2;
                currentLevelCount = Mathf.Max(currentLevelCount, 6); // 最少6个
                
                for (int i = 0; i < currentLevelCount; i++)
                {
                    float angle = (float)i / currentLevelCount * 360f;
                    float radians = angle * Mathf.Deg2Rad;
                    
                    // 计算位置
                    Vector3 position = center + new Vector3(
                        Mathf.Cos(radians) * radius,
                        height,
                        Mathf.Sin(radians) * radius
                    );
                    
                    // 计算朝向（看向中心点）
                    Vector3 direction = (center - position).normalized;
                    Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                    
                    string name = $"Camera_L{level}_H{i:D2}";
                    
                    cameraPositions.Add(new CameraPosition(position, rotation, name, totalIndex));
                    totalIndex++;
                }
            }
            
            // 添加一些特殊角度的摄像机
            AddSpecialCameraPositions(center, ref totalIndex);
            
            Debug.Log($"生成了 {cameraPositions.Count} 个摄像机位置");
        }
        
        /// <summary>
        /// 添加特殊角度的摄像机位置
        /// </summary>
        private void AddSpecialCameraPositions(Vector3 center, ref int totalIndex)
        {
            // 顶部俯视
            Vector3 topPosition = center + Vector3.up * (maxHeight + 2f);
            Quaternion topRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            cameraPositions.Add(new CameraPosition(topPosition, topRotation, "Camera_Top", totalIndex++));
            
            // 底部仰视（如果合理的话）
            if (minHeight > 0.5f)
            {
                Vector3 bottomPosition = center + Vector3.up * 0.3f;
                Quaternion bottomRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                cameraPositions.Add(new CameraPosition(bottomPosition, bottomRotation, "Camera_Bottom", totalIndex++));
            }
            
            // 几个近距离特写位置
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                float radians = angle * Mathf.Deg2Rad;
                float closeRadius = radius * 0.6f;
                
                Vector3 position = center + new Vector3(
                    Mathf.Cos(radians) * closeRadius,
                    minHeight + 1f,
                    Mathf.Sin(radians) * closeRadius
                );
                
                Vector3 direction = (center - position).normalized;
                Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                
                cameraPositions.Add(new CameraPosition(position, rotation, $"Camera_Close_{i}", totalIndex++));
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
            
            Gizmos.color = gizmoColor;
            Vector3 center = targetCenter != null ? targetCenter.position : Vector3.zero;
            
            // 绘制中心点
            Gizmos.DrawWireSphere(center, 0.5f);
            
            // 绘制摄像机位置
            foreach (var pos in cameraPositions)
            {
                Gizmos.DrawWireCube(pos.position, Vector3.one * 0.3f);
                Gizmos.DrawLine(pos.position, center);
            }
            
            // 绘制环绕圆圈
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
                    
                    Vector3 point1 = circleCenter + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                    Vector3 point2 = circleCenter + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);
                    
                    Gizmos.DrawLine(point1, point2);
                }
            }
        }
    }
}
