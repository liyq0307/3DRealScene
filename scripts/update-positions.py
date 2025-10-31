#!/usr/bin/env python3
"""
更新场景对象位置脚本
通过API将所有场景对象的位置更新为有效的地理坐标
"""

import requests
import json

# API配置
API_BASE_URL = "http://localhost:5000/api"
TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhM2I1YmYzMy01MjgxLTQ2ODQtYWYxMS00MTBhOTllOGU3MzEiLCJlbWFpbCI6InRlc3RAZXhhbXBsZS5jb20iLCJuYW1lIjoidGVzdHVzZXIiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJVc2VyIiwianRpIjoiMzE0MWM2NTQtZjdhMi00MGZiLWI3YmEtODU5ZjEzYWJjYjk0IiwiaWF0IjoiMTc2MTIwNjcyNiIsImV4cCI6MTc2MTI5MzEyNiwiaXNzIjoiUmVhbFNjZW5lM0QuV2ViQXBpIiwiYXVkIjoiUmVhbFNjZW5lM0QuQ2xpZW50In0.KF0pBJLhcEIeDPVq2nEtT21BUOl9GqemDmveuN4ikWY"

# 默认位置：北京天安门广场
DEFAULT_POSITION = [116.397128, 39.908802, 100]

headers = {
    "Authorization": f"Bearer {TOKEN}",
    "Content-Type": "application/json"
}

def get_all_scenes():
    """获取所有场景"""
    response = requests.get(f"{API_BASE_URL}/scenes", headers=headers)
    response.raise_for_status()
    return response.json()

def get_scene_objects(scene_id):
    """获取场景中的所有对象"""
    response = requests.get(f"{API_BASE_URL}/sceneobjects/scene/{scene_id}", headers=headers)
    response.raise_for_status()
    return response.json()

def update_object_position(object_id, position):
    """更新对象位置"""
    data = {
        "position": position
    }
    response = requests.put(f"{API_BASE_URL}/sceneobjects/{object_id}", json=data, headers=headers)
    response.raise_for_status()
    return response.json()

def main():
    print("=" * 60)
    print("场景对象位置更新脚本")
    print("=" * 60)

    # 获取所有场景
    print("\n正在获取场景列表...")
    scenes = get_all_scenes()
    print(f"找到 {len(scenes)} 个场景")

    total_updated = 0

    # 遍历每个场景
    for scene in scenes:
        scene_id = scene['id']
        scene_name = scene['name']
        print(f"\n处理场景: {scene_name} ({scene_id})")

        # 获取场景对象
        objects = get_scene_objects(scene_id)
        print(f"  - 找到 {len(objects)} 个场景对象")

        # 更新每个对象
        for obj in objects:
            obj_id = obj['id']
            obj_name = obj['name']
            current_pos = obj.get('position', [0, 0, 0])

            # 检查是否需要更新（位置为[0,0,0]）
            if current_pos == [0, 0, 0] or all(x == 0 for x in current_pos):
                print(f"  - 更新对象: {obj_name}")
                print(f"    当前位置: {current_pos}")
                print(f"    新位置: {DEFAULT_POSITION}")

                try:
                    updated = update_object_position(obj_id, DEFAULT_POSITION)
                    print(f"    ✓ 更新成功")
                    total_updated += 1
                except Exception as e:
                    print(f"    ✗ 更新失败: {e}")
            else:
                print(f"  - 跳过对象: {obj_name} (位置已设置: {current_pos})")

    print("\n" + "=" * 60)
    print(f"更新完成！共更新 {total_updated} 个对象")
    print("=" * 60)

if __name__ == "__main__":
    main()
