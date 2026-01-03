# 3dParty - 第三方库目录

本目录包含手动下载的头文件库（header-only libraries）。

## 📦 已包含的库

### 必需库

| 库名 | 版本 | 用途 | 文件 |
|-----|------|------|------|
| **GLM** | 1.0.1 | 数学库（向量、矩阵运算） | `include/glm/` |
| **Eigen3** | 3.4.0 | 线性代数库（矩阵运算、坐标转换） | `include/Eigen/` |
| **nlohmann-json** | latest | JSON解析和序列化 | `include/nlohmann/json.hpp` |
| **tinygltf** | latest | GLTF/GLB文件读写 | `include/tiny_gltf.h` |
| **stb** | latest | 图像读写（stb_image, stb_image_write） | `include/stb_*.h` |

## 🔄 更新说明

这些库为**头文件库**，已通过以下方式获取：

```bash
# GLM
git clone --depth 1 --branch 1.0.1 https://github.com/g-truc/glm.git
cp -r glm-temp/glm include/

# Eigen3
git clone --depth 1 --branch 3.4.0 https://gitlab.com/libeigen/eigen.git
cp -r eigen-temp/Eigen include/

# nlohmann-json
curl -o include/nlohmann/json.hpp https://raw.githubusercontent.com/nlohmann/json/develop/single_include/nlohmann/json.hpp

# tinygltf
curl -o include/tiny_gltf.h https://raw.githubusercontent.com/syoyo/tinygltf/master/tiny_gltf.h

# stb
curl -o include/stb_image.h https://raw.githubusercontent.com/nothings/stb/master/stb_image.h
curl -o include/stb_image_write.h https://raw.githubusercontent.com/nothings/stb/master/stb_image_write.h
```

## 📝 使用说明

CMakeLists.txt 已配置包含路径：
```cmake
target_include_directories(${PROJECT_NAME}
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/../3dParty/include
)
```

代码中直接使用：
```cpp
#include <glm/glm.hpp>
#include <Eigen/Eigen>
#include <nlohmann/json.hpp>
#include <tiny_gltf.h>
#include <stb_image.h>
#include <stb_image_write.h>
```

## ⚠️ 注意事项

- 这些库**不受 vcpkg 管理**
- 更新时需手动下载新版本
- 仅限头文件库，无需编译
- Eigen3 专注于线性代数，GLM 更适合图形学计算
