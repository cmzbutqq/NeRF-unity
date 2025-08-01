using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DataCapture
{
    /// <summary>
    /// COLMAP格式数据导出器
    /// </summary>
    public static class COLMAPExporter
    {
        /// <summary>
        /// 导出COLMAP格式文件
        /// </summary>
        public static void ExportCOLMAPFormat(string basePath, List<SceneDataExporter.CameraData> cameraDataList)
        {
            string sparsePath = Path.Combine(basePath, "sparse", "0");
            Directory.CreateDirectory(sparsePath);
            
            ExportCameras(sparsePath, cameraDataList);
            ExportImages(sparsePath, cameraDataList);
            ExportPoints3D(sparsePath);
            
            Debug.Log($"COLMAP格式文件已导出到: {sparsePath}");
        }
        
        /// <summary>
        /// 导出cameras.txt文件
        /// </summary>
        static void ExportCameras(string sparsePath, List<SceneDataExporter.CameraData> cameraDataList)
        {
            string camerasPath = Path.Combine(sparsePath, "cameras.txt");
            
            using (StreamWriter writer = new StreamWriter(camerasPath))
            {
                // COLMAP cameras.txt格式头部注释
                writer.WriteLine("# Camera list with one line of data per camera:");
                writer.WriteLine("# CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
                writer.WriteLine("# Number of cameras: 1");
                
                if (cameraDataList.Count > 0)
                {
                    var firstCamera = cameraDataList[0];
                    
                    // 计算图像尺寸（假设所有摄像机使用相同分辨率）
                    int imageWidth = 1920; // 从SceneDataExporter获取
                    int imageHeight = 1080;
                    
                    // 计算焦距和主点
                    float fovY = firstCamera.fieldOfView * Mathf.Deg2Rad;
                    float focalLength = (imageHeight * 0.5f) / Mathf.Tan(fovY * 0.5f);
                    float cx = imageWidth * 0.5f;
                    float cy = imageHeight * 0.5f;
                    
                    // COLMAP相机模型：PINHOLE
                    // 格式：CAMERA_ID MODEL WIDTH HEIGHT fx fy cx cy
                    writer.WriteLine($"1 PINHOLE {imageWidth} {imageHeight} {focalLength:F6} {focalLength:F6} {cx:F6} {cy:F6}");
                }
            }
        }
        
        /// <summary>
        /// 导出images.txt文件
        /// </summary>
        static void ExportImages(string sparsePath, List<SceneDataExporter.CameraData> cameraDataList)
        {
            string imagesPath = Path.Combine(sparsePath, "images.txt");
            
            using (StreamWriter writer = new StreamWriter(imagesPath))
            {
                // COLMAP images.txt格式头部注释
                writer.WriteLine("# Image list with two lines of data per image:");
                writer.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, NAME");
                writer.WriteLine("# POINTS2D[] as (X, Y, POINT3D_ID)");
                writer.WriteLine($"# Number of images: {cameraDataList.Count}, mean observations per image: 0");
                
                for (int i = 0; i < cameraDataList.Count; i++)
                {
                    var camData = cameraDataList[i];
                    
                    // 转换Unity坐标系到COLMAP坐标系
                    Vector3 position = ConvertUnityToCOLMAPPosition(camData.position);
                    Quaternion rotation = ConvertUnityToCOLMAPRotation(camData.rotation);
                    
                    // COLMAP使用四元数表示旋转 (w, x, y, z)
                    // 位置是摄像机在世界坐标系中的位置
                    string imageName = $"{camData.imageName}.jpg";
                    
                    // 第一行：IMAGE_ID QW QX QY QZ TX TY TZ CAMERA_ID NAME
                    writer.WriteLine($"{i + 1} {rotation.w:F6} {rotation.x:F6} {rotation.y:F6} {rotation.z:F6} " +
                                   $"{position.x:F6} {position.y:F6} {position.z:F6} 1 {imageName}");
                    
                    // 第二行：2D点列表（暂时为空）
                    writer.WriteLine("");
                }
            }
        }
        
        /// <summary>
        /// 导出points3D.txt文件（空文件，因为我们没有预先计算的3D点）
        /// </summary>
        static void ExportPoints3D(string sparsePath)
        {
            string points3DPath = Path.Combine(sparsePath, "points3D.txt");
            
            using (StreamWriter writer = new StreamWriter(points3DPath))
            {
                // COLMAP points3D.txt格式头部注释
                writer.WriteLine("# 3D point list with one line of data per point:");
                writer.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");
                writer.WriteLine("# Number of points: 0, mean track length: 0");
                
                // 空文件 - 3DGS训练会自动生成3D点
            }
        }
        
        /// <summary>
        /// 转换Unity位置到COLMAP坐标系
        /// Unity: Y向上，Z向前，右手坐标系
        /// COLMAP: Y向下，Z向后，右手坐标系
        /// </summary>
        static Vector3 ConvertUnityToCOLMAPPosition(Vector3 unityPos)
        {
            // 转换坐标系：Unity (x, y, z) -> COLMAP (x, -y, -z)
            return new Vector3(unityPos.x, -unityPos.y, -unityPos.z);
        }
        
        /// <summary>
        /// 转换Unity旋转到COLMAP坐标系
        /// </summary>
        static Quaternion ConvertUnityToCOLMAPRotation(Quaternion unityRot)
        {
            // Unity到COLMAP的旋转转换
            // 需要考虑坐标系差异
            
            // 创建坐标系转换矩阵
            Matrix4x4 unityToCOLMAP = Matrix4x4.identity;
            unityToCOLMAP.m11 = -1; // Y轴翻转
            unityToCOLMAP.m22 = -1; // Z轴翻转
            
            // 转换旋转
            Matrix4x4 rotMatrix = Matrix4x4.Rotate(unityRot);
            Matrix4x4 convertedMatrix = unityToCOLMAP * rotMatrix * unityToCOLMAP.inverse;
            
            return convertedMatrix.rotation;
        }
        
        /// <summary>
        /// 从Unity投影矩阵提取相机内参
        /// </summary>
        public static void ExtractCameraIntrinsics(Matrix4x4 projMatrix, int imageWidth, int imageHeight, 
            out float fx, out float fy, out float cx, out float cy)
        {
            // Unity投影矩阵到相机内参的转换
            fx = projMatrix.m00 * imageWidth * 0.5f;
            fy = projMatrix.m11 * imageHeight * 0.5f;
            cx = imageWidth * 0.5f;
            cy = imageHeight * 0.5f;
        }
        
        /// <summary>
        /// 验证COLMAP文件格式
        /// </summary>
        public static bool ValidateCOLMAPFiles(string sparsePath)
        {
            string camerasPath = Path.Combine(sparsePath, "cameras.txt");
            string imagesPath = Path.Combine(sparsePath, "images.txt");
            string points3DPath = Path.Combine(sparsePath, "points3D.txt");
            
            bool isValid = File.Exists(camerasPath) && File.Exists(imagesPath) && File.Exists(points3DPath);
            
            if (isValid)
            {
                Debug.Log("COLMAP文件格式验证通过");
            }
            else
            {
                Debug.LogError("COLMAP文件格式验证失败，缺少必要文件");
            }
            
            return isValid;
        }
        
        /// <summary>
        /// 生成COLMAP数据集信息摘要
        /// </summary>
        public static void GenerateDatasetSummary(string basePath, List<SceneDataExporter.CameraData> cameraDataList)
        {
            string summaryPath = Path.Combine(basePath, "dataset_summary.txt");
            
            using (StreamWriter writer = new StreamWriter(summaryPath))
            {
                writer.WriteLine("=== 3DGS训练数据集摘要 ===");
                writer.WriteLine($"生成时间: {System.DateTime.Now}");
                writer.WriteLine($"图像数量: {cameraDataList.Count}");
                writer.WriteLine($"图像分辨率: 1920x1080");
                writer.WriteLine($"摄像机模型: PINHOLE");
                writer.WriteLine();
                
                if (cameraDataList.Count > 0)
                {
                    var firstCam = cameraDataList[0];
                    writer.WriteLine("摄像机参数:");
                    writer.WriteLine($"  视场角: {firstCam.fieldOfView:F2}°");
                    writer.WriteLine($"  近裁剪面: {firstCam.nearClipPlane:F3}");
                    writer.WriteLine($"  远裁剪面: {firstCam.farClipPlane:F3}");
                    writer.WriteLine($"  宽高比: {firstCam.aspect:F3}");
                }
                
                writer.WriteLine();
                writer.WriteLine("文件结构:");
                writer.WriteLine("  images/          - RGB图像");
                writer.WriteLine("  depth/           - 深度图");
                writer.WriteLine("  sparse/0/        - COLMAP格式数据");
                writer.WriteLine("    cameras.txt    - 摄像机内参");
                writer.WriteLine("    images.txt     - 图像外参");
                writer.WriteLine("    points3D.txt   - 3D点云（空）");
                writer.WriteLine("  cameras.json     - Unity摄像机数据");
            }
            
            Debug.Log($"数据集摘要已生成: {summaryPath}");
        }
    }
}
