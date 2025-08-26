#!/usr/bin/env python3
"""
最终修正版COLMAP到Instant-NGP转换器
修复所有已知问题，确保正确的相机参数和坐标系转换
"""

import os
import json
import math


def quaternion_to_matrix(qw, qx, qy, qz):
    """四元数转旋转矩阵 - 使用标准公式"""
    # 归一化四元数
    norm = math.sqrt(qw * qw + qx * qx + qy * qy + qz * qz)
    qw /= norm
    qx /= norm
    qy /= norm
    qz /= norm

    # 四元数到旋转矩阵的转换（标准公式）
    matrix = [
        [
            1 - 2 * qy * qy - 2 * qz * qz,
            2 * qx * qy - 2 * qw * qz,
            2 * qx * qz + 2 * qw * qy,
        ],
        [
            2 * qx * qy + 2 * qw * qz,
            1 - 2 * qx * qx - 2 * qz * qz,
            2 * qy * qz - 2 * qw * qx,
        ],
        [
            2 * qx * qz - 2 * qw * qy,
            2 * qy * qz + 2 * qw * qx,
            1 - 2 * qx * qx - 2 * qy * qy,
        ],
    ]

    return matrix


def calculate_transform_matrix(image_data, camera_data):
    """计算变换矩阵 - 正确的坐标系转换"""
    # 四元数转旋转矩阵
    rotation_matrix = quaternion_to_matrix(
        image_data["qw"], image_data["qx"], image_data["qy"], image_data["qz"]
    )

    # 构建4x4变换矩阵 - 嵌套数组格式
    transform_matrix = [
        [
            rotation_matrix[0][0],
            rotation_matrix[0][1],
            rotation_matrix[0][2],
            image_data["tx"],
        ],
        [
            rotation_matrix[1][0],
            rotation_matrix[1][1],
            rotation_matrix[1][2],
            image_data["ty"],
        ],
        [
            rotation_matrix[2][0],
            rotation_matrix[2][1],
            rotation_matrix[2][2],
            image_data["tz"],
        ],
        [0.0, 0.0, 0.0, 1.0],
    ]

    # 坐标系转换：Unity (左手系) -> Instant-NGP (右手系)
    # Unity: X右, Y上, Z前
    # Instant-NGP: X右, Y上, Z后

    # 只需要翻转Z轴即可
    transform_matrix[0][2] *= -1  # 翻转X分量的Z
    transform_matrix[1][2] *= -1  # 翻转Y分量的Z
    transform_matrix[2][2] *= -1  # 翻转Z分量的Z

    return transform_matrix


def read_cameras(cameras_path):
    """读取相机参数"""
    cameras = []
    with open(cameras_path, "r") as f:
        for line in f:
            line = line.strip()
            if line.startswith("#") or not line:
                continue

            parts = line.split(" ")
            if len(parts) >= 8:
                camera = {
                    "id": int(parts[0]),
                    "model": parts[1],
                    "width": int(parts[2]),
                    "height": int(parts[3]),
                    "fx": float(parts[4]),
                    "fy": float(parts[5]),
                    "cx": float(parts[6]),
                    "cy": float(parts[7]),
                }
                cameras.append(camera)

    return cameras


def read_images(images_path):
    """读取图像参数"""
    images = []
    with open(images_path, "r") as f:
        lines = f.readlines()

    for i in range(0, len(lines), 2):
        if i + 1 >= len(lines):
            break

        line1 = lines[i].strip()
        line2 = lines[i + 1].strip()

        if line1.startswith("#") or line2.startswith("#"):
            continue

        parts1 = line1.split(" ")

        if len(parts1) >= 10:
            image = {
                "id": int(parts1[0]),
                "qw": float(parts1[1]),
                "qx": float(parts1[2]),
                "qy": float(parts1[3]),
                "qz": float(parts1[4]),
                "tx": float(parts1[5]),
                "ty": float(parts1[6]),
                "tz": float(parts1[7]),
                "cameraId": int(parts1[8]),
                "name": parts1[9],
            }
            images.append(image)

    return images


def calculate_camera_angle_x(fx, width):
    """根据焦距计算camera_angle_x"""
    # camera_angle_x = 2 * arctan(width / (2 * fx))
    return 2 * math.atan(width / (2 * fx))


def generate_transforms_json_final(colmap_data_path, output_path, aabb_scale=32):
    """生成最终修正的transforms.json"""
    try:
        print("开始转换COLMAP数据为Instant-NGP格式...")

        # 读取COLMAP数据
        cameras_path = os.path.join(colmap_data_path, "sparse", "0", "cameras.txt")
        images_path = os.path.join(colmap_data_path, "sparse", "0", "images.txt")

        if not os.path.exists(cameras_path):
            raise FileNotFoundError(f"cameras.txt不存在: {cameras_path}")

        if not os.path.exists(images_path):
            raise FileNotFoundError(f"images.txt不存在: {images_path}")

        # 读取数据
        cameras = read_cameras(cameras_path)
        images = read_images(images_path)

        if not cameras:
            raise ValueError("未找到相机数据")
        if not images:
            raise ValueError("未找到图像数据")

        print(f"读取完成: {len(cameras)} 个相机, {len(images)} 张图像")

        # 获取第一个相机的参数
        camera = cameras[0]

        # 计算正确的camera_angle_x
        camera_angle_x = calculate_camera_angle_x(camera["fx"], camera["width"])

        print(
            f"相机参数: fx={camera['fx']}, fy={camera['fy']}, cx={camera['cx']}, cy={camera['cy']}"
        )
        print(f"图像尺寸: {camera['width']}x{camera['height']}")
        print(f"计算得到的camera_angle_x: {camera_angle_x:.6f}")

        # 创建输出目录
        os.makedirs(output_path, exist_ok=True)

        # 生成transforms.json - 使用正确的相机参数
        transforms_data = {
            "camera_angle_x": camera_angle_x,
            "fl_x": camera["fx"],
            "fl_y": camera["fy"],
            "k1": 0.0,
            "k2": 0.0,
            "p1": 0.0,
            "p2": 0.0,
            "cx": camera["cx"],
            "cy": camera["cy"],
            "w": camera["width"],
            "h": camera["height"],
            "aabb_scale": aabb_scale,
            "frames": [],
        }

        # 为每个图像生成帧数据
        for i, image in enumerate(images):
            transform_matrix = calculate_transform_matrix(image, camera)

            frame = {
                "file_path": f"images/{image['name']}",
                "sharpness": 50.0,
                "transform_matrix": transform_matrix,
            }

            transforms_data["frames"].append(frame)

            # 打印前几个变换矩阵用于调试
            if i < 3:
                print(f"图像 {image['name']} 的变换矩阵:")
                for row in transform_matrix:
                    print(f"  {row}")

        # 保存文件
        output_file = os.path.join(output_path, "transforms.json")
        with open(output_file, "w") as f:
            json.dump(transforms_data, f, indent=2)

        print(f"[SUCCESS] 转换完成，文件保存到: {output_file}")
        print(f"transforms.json 文件大小: {os.path.getsize(output_file)} bytes")
        print(f"包含 {len(transforms_data['frames'])} 帧数据")

        return True

    except Exception as e:
        print(f"转换失败: {e}")
        import traceback

        traceback.print_exc()
        return False


def main():
    """主函数"""
    print("=== 最终修正版 COLMAP to Instant-NGP Converter ===")

    # 配置路径
    colmap_data_path = "./captured_data"
    output_path = "./Instant-NGP-for-RTX-3000-and-4000/data/nerf/unity_scene"
    aabb_scale = 32

    # 检查输入路径
    if not os.path.exists(colmap_data_path):
        print(f"错误: COLMAP数据路径不存在: {colmap_data_path}")
        return

    # 执行转换
    success = generate_transforms_json_final(colmap_data_path, output_path, aabb_scale)

    if success:
        print("\n[SUCCESS] 转换成功！现在可以启动Instant-NGP训练了")
        print("运行命令: .\\instant-ngp.exe data\\nerf\\unity_scene")
    else:
        print("\n[ERROR] 转换失败，请检查错误信息")


if __name__ == "__main__":
    main()
