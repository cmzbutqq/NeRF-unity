# 3DGS Unity Demo 实现计划

## 🎯 Demo目标
实现一个最小可行的3D Gaussian Splatting演示：
1. Unity摄像机捕获场景数据
2. 将数据传递给3DGS训练管线
3. 在Unity中渲染训练后的高斯场

## 📋 技术架构

### Phase 1: 数据采集 (1-2天)
**目标**: 从Unity场景中提取训练数据

#### 1.1 场景设置
- [ ] 创建简单测试场景（几个基础几何体）
- [ ] 设置多个摄像机位置（环绕拍摄）
- [ ] 配置适当的光照

#### 1.2 数据导出器
```csharp
// Scripts/DataCapture/SceneDataExporter.cs
public class SceneDataExporter : MonoBehaviour
{
    // 导出摄像机参数 (位置、旋转、内参)
    // 导出RGB图像
    // 导出深度图
    // 生成COLMAP格式数据
}
```

#### 1.3 输出格式
- `images/` - RGB图像 (.jpg)
- `cameras.txt` - 摄像机参数
- `points3D.txt` - 初始点云（可选）

### Phase 2: 3DGS训练集成 (2-3天)
**目标**: 集成现有3DGS训练管线

#### 2.1 训练环境
- [ ] 集成gaussian-splatting原始repo
- [ ] 创建Python训练脚本包装器
- [ ] 配置CUDA环境

#### 2.2 训练管线
```python
# Scripts/Training/train_wrapper.py
def train_scene(data_path, output_path):
    # 调用原始3DGS训练
    # 输出.ply高斯点云文件
    pass
```

#### 2.3 Unity-Python通信
```csharp
// Scripts/Training/TrainingManager.cs
public class TrainingManager : MonoBehaviour
{
    public void StartTraining(string dataPath)
    {
        // 调用Python训练脚本
        // 监控训练进度
        // 加载训练结果
    }
}
```

### Phase 3: 高斯场渲染 (3-4天)
**目标**: 在Unity中渲染3DGS结果

#### 3.1 高斯点数据结构
```csharp
// Scripts/Rendering/GaussianPoint.cs
[System.Serializable]
public struct GaussianPoint
{
    public Vector3 position;
    public Vector3 scale;
    public Vector4 rotation;  // quaternion
    public Vector3 color;
    public float opacity;
}
```

#### 3.2 PLY文件加载器
```csharp
// Scripts/Data/PLYLoader.cs
public class PLYLoader
{
    public static GaussianPoint[] LoadFromPLY(string filePath)
    {
        // 解析.ply文件
        // 转换为Unity数据格式
    }
}
```

#### 3.3 高斯渲染器
```csharp
// Scripts/Rendering/GaussianRenderer.cs
public class GaussianRenderer : MonoBehaviour
{
    // 使用Compute Shader渲染高斯点
    // 实现splatting算法
    // 处理透明度混合
}
```

#### 3.4 渲染Shader
```hlsl
// Shaders/GaussianSplat.shader
// 实现3D高斯投影到2D屏幕
// 计算椭圆形状和透明度
// 深度排序和混合
```

### Phase 4: 集成测试 (1天)
**目标**: 端到端流程验证

#### 4.1 完整流程
1. 场景设置 → 数据导出
2. 数据导出 → Python训练
3. 训练结果 → Unity渲染
4. 实时预览和调试

#### 4.2 性能优化
- [ ] GPU内存管理
- [ ] 渲染批处理
- [ ] LOD系统（可选）

## 🛠️ 技术实现细节

### 数据格式
```
project/
├── captured_data/
│   ├── images/           # RGB图像
│   ├── cameras.txt       # 摄像机参数
│   └── sparse/           # COLMAP稀疏重建
├── trained_models/
│   └── scene.ply         # 训练后的高斯点云
└── Assets/
    ├── Scripts/
    ├── Shaders/
    └── Scenes/
```

### 关键算法
1. **3D高斯投影**: 将3D高斯椭球投影到2D屏幕椭圆
2. **深度排序**: 按深度对高斯点排序
3. **Alpha混合**: 正确的透明度混合顺序

### 性能考虑
- **点云数量**: 初期限制在10万个点以内
- **渲染分辨率**: 1080p目标60fps
- **内存使用**: 控制在2GB以内

## 📦 依赖项

### Unity包
- Universal Render Pipeline
- Burst Compiler (性能优化)
- Mathematics (向量计算)

### 外部依赖
- Python 3.8+
- PyTorch
- CUDA 11.8+
- gaussian-splatting repo

### 硬件要求
- NVIDIA GPU (RTX 3060+)
- 16GB+ RAM
- 10GB+ 存储空间

## 🎮 Demo界面

### 简单UI
- [ ] "Capture Scene" 按钮
- [ ] "Start Training" 按钮  
- [ ] "Load Result" 按钮
- [ ] 训练进度条
- [ ] 渲染质量设置

### 调试功能
- [ ] 显示高斯点数量
- [ ] 渲染性能统计
- [ ] 原始场景/高斯场对比

## ⏱️ 时间估算
- **Phase 1**: 1-2天 (数据采集)
- **Phase 2**: 2-3天 (训练集成)  
- **Phase 3**: 3-4天 (渲染实现)
- **Phase 4**: 1天 (集成测试)
- **总计**: 7-10天

## 🚀 成功标准
1. ✅ 能够从Unity场景导出训练数据
2. ✅ 成功训练出高斯点云模型
3. ✅ 在Unity中实时渲染高斯场
4. ✅ 视觉质量接近原始场景
5. ✅ 帧率保持在30fps以上

## 📝 后续扩展
完成基础demo后可以考虑：
- 实时训练（边拍摄边训练）
- 动态场景支持
- VR/AR集成
- 移动端优化
