# Instant-NGP 集成工作流程

## 概述

本项目已成功集成 Instant-NGP 到 Unity 3DGS 项目中，实现秒级图生场景功能。经过代码清理和重构，现在使用更简洁的架构。

## 工作流程

### 1. 数据采集 (Unity)

- 使用 `HybridDataCapture.cs` 在 Unity 中采集场景图像
- 自动生成 COLMAP 格式的数据结构
- 输出到 `captured_data/` 目录

### 2. 数据转换 (Python)

- 使用 `test_converter_final.py` 将 COLMAP 数据转换为 Instant-NGP 格式
- 自动计算正确的相机内参 (`camera_angle_x`)
- 处理坐标系转换 (Unity 左手系 → Instant-NGP 右手系)
- 生成 `transforms.json` 文件

### 3. 训练启动 (Unity)

- 使用 `InstantNGPTrainingController.cs` 一键管理训练流程
- 集成数据转换、训练启动、状态监控等功能
- 启动 Instant-NGP 可执行文件

## 文件结构

```
3dgs-unity/
├── captured_data/                    # Unity 采集的 COLMAP 数据
│   ├── images/                       # 场景图像
│   └── sparse/0/                     # 相机参数
│       ├── cameras.txt               # 相机内参
│       └── images.txt                # 相机外参
├── Instant-NGP-for-RTX-3000-and-4000/
│   └── data/nerf/unity_scene/        # Instant-NGP 数据集
│       ├── images/                    # 训练图像
│       └── transforms.json            # 相机参数 (Python 生成)
├── test_converter_final.py            # 数据转换脚本
└── Assets/Scripts/Training/
    ├── InstantNGPTrainingController.cs # 训练控制器 (主要组件)
    ├── InstantNGPQuickTrainer.cs      # 快速训练器 (可选)
    ├── InstantNGPTrainingManager.cs    # 训练管理器 (可选)
    └── TrainingProgressMonitor.cs      # 进度监控 (可选)
```

## 使用方法

### 1. 生成训练数据

```bash
# 在项目根目录运行
python test_converter_final.py
```

### 2. 启动训练

```bash
# 方法1: 直接运行
cd Instant-NGP-for-RTX-3000-and-4000
.\instant-ngp.exe data\nerf\unity_scene

# 方法2: 在 Unity 中使用 InstantNGPTrainingController
```

## Unity 场景配置

### 主要组件

1. **InstantNGPTrainingManager** (GameObject)

   - 添加 `InstantNGPTrainingController` 脚本
   - 配置所有路径和 UI 引用

2. **TrainingCanvas** (Canvas)

   - 包含所有训练相关的 UI 元素
   - 建议使用 "Scale With Screen Size" 缩放模式

3. **TrainingPanel** (Panel)
   - 训练控制按钮
   - 状态显示
   - 进度条
   - 调试信息

### UI 组件配置

- **Start Training Button**: 开始训练
- **Stop Training Button**: 停止训练
- **Convert Data Button**: 转换数据
- **Status Text**: 显示当前状态
- **Progress Slider**: 训练进度条
- **Debug Text**: 详细日志信息

详细配置说明请参考 `Assets/Scripts/Training/UI_Setup_Guide.md`

## 关键修复

### 相机内参问题

- **问题**: 使用了错误的 `camera_angle_x` 值 (0.7481849417937728)
- **解决**: 根据 Unity 相机焦距自动计算: `2 * arctan(width / (2 * fx))`
- **结果**: 从错误的 0.7481849417937728 修正为正确的 1.596851

### 坐标系转换问题

- **问题**: 复杂的轴交换逻辑导致变换矩阵错误
- **解决**: 简化为只进行必要的 Z 轴翻转
- **结果**: 变换矩阵更加合理，位置坐标正确

### 数据格式问题

- **问题**: 变换矩阵格式不符合 Instant-NGP 期望
- **解决**: 使用嵌套数组格式 `[[], [], [], []]`
- **结果**: Instant-NGP 成功加载数据集

## 架构优势

### 1. 代码简化

- 移除了冗余的测试脚本和转换器
- 整合所有训练功能到一个控制器
- 清晰的职责分离

### 2. 易于维护

- 单一数据转换脚本 (Python)
- 统一的训练管理 (Unity C#)
- 详细的配置文档

### 3. 功能完整

- 数据转换、训练启动、状态监控
- 错误处理和调试信息
- 异步操作和进程管理

## 注意事项

1. **数据转换**: 必须先运行 Python 转换器生成 `transforms.json`
2. **图像路径**: 确保图像文件在 `Instant-NGP-for-RTX-3000-and-4000/data/nerf/unity_scene/images/` 目录
3. **坐标系**: Unity 左手系会自动转换为 Instant-NGP 右手系
4. **训练监控**: Instant-NGP 是独立进程，Unity 中主要监控启动状态
5. **UI 配置**: 按照配置指南正确设置所有 UI 组件引用

## 性能特点

- **训练速度**: 1-3 秒快速收敛 (Instant-NGP 特性)
- **内存占用**: 约 13MB 网络参数
- **GPU 要求**: 支持 CUDA 的 NVIDIA GPU
- **输出质量**: 高保真 3D 场景重建

## 故障排除

### 常见问题

1. **"No training images were found"**: 检查 `transforms.json` 和图像路径
2. **"Rotation matrix scaling component"**: 坐标系转换警告，Instant-NGP 会自动处理
3. **训练效果差**: 检查相机内参和坐标系转换是否正确
4. **UI 组件引用丢失**: 重新拖拽 UI 组件到脚本字段

### 调试方法

1. 检查 `transforms.json` 中的相机参数
2. 验证图像文件路径和数量
3. 查看 Unity Console 和 Debug Text 输出
4. 使用 Unity 编辑器中的调试信息
5. 测试 Python 脚本独立运行

## 下一步计划

1. **Unity 集成**: 训练完成后加载模型到 Unity 进行实时渲染
2. **性能优化**: 分析训练收敛速度，优化模型参数
3. **生产部署**: 准备生产环境的部署方案
4. **UI 优化**: 根据使用反馈优化用户界面
