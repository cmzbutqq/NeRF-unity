# Instant-NGP 训练控制器 UI 配置指南

## 概述

`InstantNGPTrainingController` 是一个整合了所有训练功能的简化控制器，需要在 Unity 场景中正确配置 UI 组件。

## 场景设置步骤

### 1. 创建训练管理器 GameObject

1. 在 Hierarchy 中右键 → Create Empty
2. 重命名为 "InstantNGPTrainingManager"
3. 添加 `InstantNGPTrainingController` 脚本组件

### 2. 创建 UI Canvas

1. 右键 → UI → Canvas
2. 重命名为 "TrainingCanvas"
3. 确保 Canvas Scaler 设置为 "Scale With Screen Size"

### 4. 创建训练控制面板

在 Canvas 下创建 Panel：

1. 右键 Canvas → UI → Panel
2. 重命名为 "TrainingPanel"
3. 设置合适的位置和大小

### 5. 添加 UI 组件

**重要**: 以下所有组件都是 TrainingPanel 的**子对象**，不是 Panel 的组件！

在 TrainingPanel 下添加以下子对象：

#### 按钮组件

- **Start Training Button** (Button)
  - 创建方法: 右键 TrainingPanel → UI → Button
  - 文本: "开始训练"
  - 功能: 启动 Instant-NGP 训练
- **Stop Training Button** (Button)
  - 创建方法: 右键 TrainingPanel → UI → Button
  - 文本: "停止训练"
  - 功能: 停止当前训练
- **Convert Data Button** (Button)
  - 创建方法: 右键 TrainingPanel → UI → Button
  - 文本: "转换数据"
  - 功能: 运行 Python 转换器

#### 状态显示组件

- **Status Text** (Text)
  - 创建方法: 右键 TrainingPanel → UI → Text
  - 文本: "就绪"
  - 功能: 显示当前训练状态
- **Progress Text** (Text)
  - 创建方法: 右键 TrainingPanel → UI → Text
  - 文本: "0%"
  - 功能: 显示训练进度百分比
- **Progress Slider** (Slider)
  - 创建方法: 右键 TrainingPanel → UI → Slider
  - 值范围: 0-1
  - 功能: 可视化训练进度

#### 调试信息组件

- **Debug Text** (Text)
  - 创建方法: 右键 TrainingPanel → UI → Text
  - 文本: ""
  - 功能: 显示详细的调试日志
  - 建议: 使用 Scroll View 包装，支持滚动

### 6. 配置脚本引用

选择 InstantNGPTrainingManager，在 Inspector 中配置：

#### Instant-NGP 配置

- **Instant NGPPath**: `./Instant-NGP-for-RTX-3000-and-4000`
- **Instant NGPExe**: `instant-ngp.exe`

#### 数据路径

- **Dataset Path**: `./Instant-NGP-for-RTX-3000-and-4000/data/nerf/unity_scene`

#### 训练参数

- 已简化，只保留核心功能
- 启动 Instant-NGP GUI 进行训练

#### UI 组件引用

- **Start Training Button**: 拖拽 Start Training Button
- **Stop Training Button**: 拖拽 Stop Training Button
- **Convert Data Button**: 拖拽 Convert Data Button
- **Status Text**: 拖拽 Status Text
- **Progress Text**: 拖拽 Progress Text
- **Progress Slider**: 拖拽 Progress Slider
- **Training Panel**: 拖拽 Training Panel
- **Debug Text**: 拖拽 Debug Text

#### 调试信息

- **Show Debug Info**: 勾选（显示详细日志）

## UI 层级结构详解

```
TrainingCanvas (Canvas)
└── TrainingPanel (Panel)
    ├── Start Training Button (Button)
    ├── Stop Training Button (Button)
    ├── Convert Data Button (Button)
    ├── Status Text (Text)
    ├── Progress Text (Text)
    ├── Progress Slider (Slider)
    └── Debug Text (Text)
```

**关键点**:

- Canvas 是根容器
- Panel 是 Canvas 的子对象
- 所有按钮、文本、滑块都是 Panel 的子对象
- 这样设计是为了便于布局管理和样式统一

## UI 布局建议

```
TrainingCanvas
└── TrainingPanel
    ├── Title Text ("Instant-NGP 训练控制")
    ├── Status Section
    │   ├── Status Text
    │   └── Progress Slider + Progress Text
    ├── Control Section
    │   ├── Convert Data Button
    │   ├── Start Training Button
    │   └── Stop Training Button
    └── Debug Section
        └── Debug Text (Scroll View)
```

## 功能说明

### 1. 数据转换

- 点击"转换数据"按钮
- 自动运行 Python 转换器
- 生成 transforms.json 文件
- **自动复制图像文件**到 Instant-NGP 数据集目录
- 创建完整的训练数据集结构

### 2. 训练启动

- 点击"开始训练"按钮
- 启动 Instant-NGP 进程
- 显示训练状态

### 3. 训练停止

- 点击"停止训练"按钮
- 强制终止训练进程
- 重置 UI 状态

### 4. 状态监控

- 实时显示训练状态
- 显示训练进度
- 输出详细调试信息

## 注意事项

1. **路径配置**: 确保所有路径都正确指向实际文件位置
2. **Python 环境**: 确保系统已安装 Python 且可执行
3. **文件权限**: 确保 Unity 有权限访问项目目录
4. **UI 层级**: 确保训练面板在其他 UI 元素之上
5. **组件层级**: Button、Text、Slider 都是 Panel 的子对象，不是组件

## 故障排除

### 常见问题

1. **组件引用丢失**: 重新拖拽 UI 组件到脚本字段
2. **路径错误**: 检查路径是否正确，使用相对路径
3. **Python 执行失败**: 检查 Python 是否在系统 PATH 中
4. **权限问题**: 以管理员身份运行 Unity

### 调试方法

1. 查看 Console 日志
2. 检查 Debug Text 输出
3. 验证文件路径存在性
4. 测试 Python 脚本独立运行
