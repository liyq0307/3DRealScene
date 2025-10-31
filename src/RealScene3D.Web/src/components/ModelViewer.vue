<template>
  <div ref="containerRef" class="model-viewer">
    <!-- 加载状态 -->
    <div v-if="loading" class="loading-overlay">
      <div class="spinner"></div>
      <p>加载中... {{ loadingProgress }}%</p>
    </div>

    <!-- 错误提示 -->
    <div v-if="error" class="error-overlay">
      <div class="error-icon">⚠️</div>
      <p>{{ error }}</p>
      <button class="btn-retry" @click="reload">重试</button>
    </div>

    <!-- 工具栏 -->
    <div v-if="showControls && !loading && !error" class="controls">
      <button class="control-btn" @click="resetCamera" title="重置视角">
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
          <path
            d="M10 4v12m-6-6h12"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          />
        </svg>
      </button>

      <button class="control-btn" @click="toggleWireframe" title="线框模式">
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
          <rect
            x="3"
            y="3"
            width="14"
            height="14"
            stroke="currentColor"
            stroke-width="2"
            fill="none"
          />
        </svg>
      </button>

      <button class="control-btn" @click="toggleGrid" title="网格">
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
          <path
            d="M2 6h16M2 10h16M2 14h16M6 2v16M10 2v16M14 2v16"
            stroke="currentColor"
            stroke-width="1"
          />
        </svg>
      </button>

      <button class="control-btn" @click="toggleAnimation" title="播放/暂停动画">
        <svg v-if="!animationPlaying" width="20" height="20" viewBox="0 0 20 20" fill="none">
          <path d="M6 4l10 6-10 6V4z" fill="currentColor" />
        </svg>
        <svg v-else width="20" height="20" viewBox="0 0 20 20" fill="none">
          <path d="M6 4h3v12H6V4zm5 0h3v12h-3V4z" fill="currentColor" />
        </svg>
      </button>
    </div>

    <!-- 信息面板 -->
    <div v-if="showInfo && modelInfo && !loading" class="info-panel">
      <div class="info-item">
        <span class="info-label">三角面:</span>
        <span class="info-value">{{ modelInfo.triangles?.toLocaleString() }}</span>
      </div>
      <div class="info-item">
        <span class="info-label">顶点:</span>
        <span class="info-value">{{ modelInfo.vertices?.toLocaleString() }}</span>
      </div>
      <div class="info-item">
        <span class="info-label">材质:</span>
        <span class="info-value">{{ modelInfo.materials }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 3D模型加载器组件
 *
 * 功能特性:
 * - 支持多种3D格式: GLTF, GLB, OBJ, FBX, DAE (Collada)
 * - 自动相机适配
 * - 轨道控制器
 * - 加载进度显示
 * - 动画播放控制
 * - 模型信息统计
 * - 线框模式切换
 * - 网格显示切换
 *
 * 格式说明:
 * - GLTF/GLB: Web标准格式,支持材质、动画、PBR渲染
 * - OBJ: 传统格式,自动添加默认材质
 * - FBX: Autodesk格式,支持复杂动画和骨骼
 * - DAE: Collada格式,支持场景层级和动画
 *
 * @author liyq
 * @date 2025-10-15
 * @updated 2025-10-22 - 添加OBJ, FBX, DAE格式支持
 */

import { ref, onMounted, onBeforeUnmount, watch } from 'vue'
import * as THREE from 'three'
import { OrbitControls } from 'three/addons/controls/OrbitControls.js'
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js'
import { OBJLoader } from 'three/addons/loaders/OBJLoader.js'
import { FBXLoader } from 'three/addons/loaders/FBXLoader.js'
import { ColladaLoader } from 'three/addons/loaders/ColladaLoader.js'

interface Props {
  modelUrl?: string
  modelFile?: File  // 直接传递File对象
  showControls?: boolean
  showInfo?: boolean
  autoRotate?: boolean
  backgroundColor?: string
  fileExtension?: string  // 用于blob URL的文件扩展名
}

const props = withDefaults(defineProps<Props>(), {
  modelUrl: '',
  showControls: true,
  showInfo: true,
  autoRotate: false,
  backgroundColor: '#1a1a1a'
})

const emit = defineEmits<{
  loaded: [model: THREE.Object3D]
  error: [error: Error]
  progress: [percent: number]
}>()

const containerRef = ref<HTMLDivElement | null>(null)
const loading = ref(false)
const loadingProgress = ref(0)
const error = ref<string | null>(null)
const animationPlaying = ref(false)

// Three.js对象
let scene: THREE.Scene
let camera: THREE.PerspectiveCamera
let renderer: THREE.WebGLRenderer
let controls: OrbitControls
let animationFrameId: number
let mixer: THREE.AnimationMixer | null = null
let clock: THREE.Clock
let grid: THREE.GridHelper
let currentModel: THREE.Object3D | null = null

// 模型信息
const modelInfo = ref<{
  triangles: number
  vertices: number
  materials: number
} | null>(null)

const wireframeEnabled = ref(false)
const gridVisible = ref(true)

// 初始化Three.js场景
const initScene = () => {
  if (!containerRef.value) return

  // 场景
  scene = new THREE.Scene()
  scene.background = new THREE.Color(props.backgroundColor)

  // 相机
  const width = containerRef.value.clientWidth
  const height = containerRef.value.clientHeight
  camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 10000)
  camera.position.set(5, 5, 5)

  // 渲染器
  renderer = new THREE.WebGLRenderer({ antialias: true })
  renderer.setSize(width, height)
  renderer.setPixelRatio(window.devicePixelRatio)
  renderer.shadowMap.enabled = true
  containerRef.value.appendChild(renderer.domElement)

  // 控制器
  controls = new OrbitControls(camera, renderer.domElement)
  controls.enableDamping = true
  controls.dampingFactor = 0.05
  controls.autoRotate = props.autoRotate
  controls.autoRotateSpeed = 2.0

  // 光照
  const ambientLight = new THREE.AmbientLight(0xffffff, 0.6)
  scene.add(ambientLight)

  const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8)
  directionalLight.position.set(10, 10, 10)
  directionalLight.castShadow = true
  scene.add(directionalLight)

  // 网格
  grid = new THREE.GridHelper(20, 20, 0x888888, 0x444444)
  scene.add(grid)

  // 时钟
  clock = new THREE.Clock()

  // 窗口调整
  window.addEventListener('resize', handleResize)

  // 开始渲染
  animate()
}

// 动画循环
const animate = () => {
  animationFrameId = requestAnimationFrame(animate)

  const delta = clock.getDelta()

  // 更新控制器
  controls.update()

  // 更新动画
  if (mixer && animationPlaying.value) {
    mixer.update(delta)
  }

  // 渲染
  renderer.render(scene, camera)
}

// 加载模型
const loadModel = async (url: string) => {
  if (!url) return

  loading.value = true
  loadingProgress.value = 0
  error.value = null

  try {
    // 移除旧模型
    if (currentModel) {
      scene.remove(currentModel)
      currentModel = null
    }

    // 根据文件扩展名选择加载器
    // 从URL中提取文件扩展名，处理查询参数的情况
    let ext = ''

    // 处理blob URL - 使用传入的文件扩展名
    if (url.startsWith('blob:')) {
      if (props.fileExtension) {
        ext = props.fileExtension.replace('.', '').toLowerCase()
        console.log(`Blob URL detected, using provided extension: ${ext}`)
      } else {
        console.warn('Blob URL detected but no fileExtension provided, defaulting to GLB')
        ext = 'glb'
      }
    } else {
      // 移除查询参数和hash，只保留路径部分
      const urlPath = url.split('?')[0].split('#')[0]
      // 从路径中提取扩展名
      const match = urlPath.match(/\.([^./]+)$/)
      ext = match ? match[1].toLowerCase() : ''
      console.log(`Extracted extension from URL: ${ext}`, { url, urlPath })
    }

    // 处理相对路径 - 转换为代理URL
    let fullUrl = url
    if (url.startsWith('/')) {
      // 相对路径格式: /bucket/filename.ext
      // 转换为代理URL: /api/files/proxy/bucket/filename.ext
      const apiBaseUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'
      fullUrl = `${apiBaseUrl}/files/proxy${url}`
      console.log(`Converting relative path to proxy URL: ${url} -> ${fullUrl}`)
    } else if (!url.startsWith('http://') && !url.startsWith('https://')) {
      // 如果是MinIO对象名称（没有协议且不是相对路径），使用代理URL
      const apiBaseUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'
      fullUrl = `${apiBaseUrl}/files/proxy/models-3d/${url}`
      console.log(`Converting MinIO object name to proxy URL: ${url} -> ${fullUrl}`)
    }

    let model: THREE.Object3D

    switch (ext) {
      case 'gltf':
      case 'glb':
        model = await loadGLTF(fullUrl)
        break
      case 'obj':
        model = await loadOBJ(fullUrl)
        break
      case 'fbx':
        model = await loadFBX(fullUrl)
        break
      case 'dae':
        model = await loadCollada(fullUrl)
        break
      default:
        throw new Error(`不支持的文件格式: ${ext}。支持的格式: GLTF, GLB, OBJ, FBX, DAE`)
    }

    // 添加到场景
    scene.add(model)
    currentModel = model

    // 自动调整相机
    fitCameraToObject(model)

    // 计算模型信息
    calculateModelInfo(model)

    // 检查动画
    if (mixer) {
      mixer.stopAllAction()
      mixer = null
    }

    emit('loaded', model)
    loading.value = false
  } catch (err: any) {
    console.error('加载模型失败:', err)
    error.value = err.message || '加载失败'
    emit('error', err)
    loading.value = false
  }
}

// 从File对象加载模型
const loadModelFromFile = async (file: File) => {
  if (!file) return

  loading.value = true
  loadingProgress.value = 0
  error.value = null

  try {
    // 移除旧模型
    if (currentModel) {
      scene.remove(currentModel)
      currentModel = null
    }

    // 从文件名获取扩展名
    const ext = file.name.split('.').pop()?.toLowerCase()
    console.log('Loading model from file:', file.name, 'Extension:', ext)

    // 创建临时blob URL用于加载
    const blobUrl = URL.createObjectURL(file)

    let model: THREE.Object3D

    try {
      switch (ext) {
        case 'gltf':
        case 'glb':
          model = await loadGLTF(blobUrl)
          break
        case 'obj':
          model = await loadOBJ(blobUrl)
          break
        case 'fbx':
          model = await loadFBX(blobUrl)
          break
        case 'dae':
          model = await loadCollada(blobUrl)
          break
        default:
          throw new Error(`不支持的文件格式: ${ext}。支持的格式: GLTF, GLB, OBJ, FBX, DAE`)
      }
    } finally {
      // 加载完成后立即释放blob URL
      URL.revokeObjectURL(blobUrl)
    }

    // 添加到场景
    scene.add(model)
    currentModel = model

    // 自动调整相机
    fitCameraToObject(model)

    // 计算模型信息
    calculateModelInfo(model)

    // 检查动画
    if (mixer) {
      mixer.stopAllAction()
      mixer = null
    }

    emit('loaded', model)
    loading.value = false
  } catch (err: any) {
    console.error('从文件加载模型失败:', err)
    error.value = err.message || '加载失败'
    emit('error', err)
    loading.value = false
  }
}

// 加载GLTF模型
const loadGLTF = (url: string): Promise<THREE.Object3D> => {
  return new Promise((resolve, reject) => {
    const loader = new GLTFLoader()

    loader.load(
      url,
      (gltf: any) => {
        // 处理动画
        if (gltf.animations && gltf.animations.length > 0) {
          mixer = new THREE.AnimationMixer(gltf.scene)
          gltf.animations.forEach((clip: THREE.AnimationClip) => {
            mixer!.clipAction(clip).play()
          })
          animationPlaying.value = true
        }

        resolve(gltf.scene)
      },
      (progress: { loaded: number; total: number }) => {
        if (progress.total > 0) {
          const percent = Math.round((progress.loaded / progress.total) * 100)
          loadingProgress.value = percent
          emit('progress', percent)
        }
      },
      reject
    )
  })
}

// 加载OBJ模型
const loadOBJ = (url: string): Promise<THREE.Object3D> => {
  return new Promise((resolve, reject) => {
    const loader = new OBJLoader()

    loader.load(
      url,
      (object: THREE.Object3D) => {
        // OBJ模型通常没有材质,添加默认材质
        object.traverse((child) => {
          if ((child as THREE.Mesh).isMesh) {
            const mesh = child as THREE.Mesh
            if (!mesh.material) {
              mesh.material = new THREE.MeshStandardMaterial({
                color: 0xcccccc,
                roughness: 0.5,
                metalness: 0.5
              })
            }
          }
        })
        resolve(object)
      },
      (progress: { loaded: number; total: number }) => {
        if (progress.total > 0) {
          const percent = Math.round((progress.loaded / progress.total) * 100)
          loadingProgress.value = percent
          emit('progress', percent)
        }
      },
      reject
    )
  })
}

// 加载FBX模型
const loadFBX = (url: string): Promise<THREE.Object3D> => {
  return new Promise((resolve, reject) => {
    const loader = new FBXLoader()

    loader.load(
      url,
      (object: THREE.Object3D) => {
        // 处理FBX动画
        if ((object as any).animations && (object as any).animations.length > 0) {
          mixer = new THREE.AnimationMixer(object)
          ;(object as any).animations.forEach((clip: THREE.AnimationClip) => {
            mixer!.clipAction(clip).play()
          })
          animationPlaying.value = true
        }
        resolve(object)
      },
      (progress: { loaded: number; total: number }) => {
        if (progress.total > 0) {
          const percent = Math.round((progress.loaded / progress.total) * 100)
          loadingProgress.value = percent
          emit('progress', percent)
        }
      },
      reject
    )
  })
}

// 加载Collada (DAE)模型
const loadCollada = (url: string): Promise<THREE.Object3D> => {
  return new Promise((resolve, reject) => {
    const loader = new ColladaLoader()

    loader.load(
      url,
      (collada: any) => {
        // 处理Collada动画
        if (collada.animations && collada.animations.length > 0) {
          mixer = new THREE.AnimationMixer(collada.scene)
          collada.animations.forEach((clip: THREE.AnimationClip) => {
            mixer!.clipAction(clip).play()
          })
          animationPlaying.value = true
        }
        resolve(collada.scene)
      },
      (progress: { loaded: number; total: number }) => {
        if (progress.total > 0) {
          const percent = Math.round((progress.loaded / progress.total) * 100)
          loadingProgress.value = percent
          emit('progress', percent)
        }
      },
      reject
    )
  })
}

// 自适应相机
const fitCameraToObject = (object: THREE.Object3D) => {
  const box = new THREE.Box3().setFromObject(object)
  const size = box.getSize(new THREE.Vector3())
  const center = box.getCenter(new THREE.Vector3())

  const maxDim = Math.max(size.x, size.y, size.z)
  const fov = camera.fov * (Math.PI / 180)
  let cameraZ = Math.abs(maxDim / 2 / Math.tan(fov / 2))
  cameraZ *= 1.5 // 增加一些边距

  camera.position.set(center.x, center.y, center.z + cameraZ)
  camera.lookAt(center)
  camera.updateProjectionMatrix()

  controls.target.copy(center)
  controls.update()
}

// 计算模型信息
const calculateModelInfo = (object: THREE.Object3D) => {
  let triangles = 0
  let vertices = 0
  const materials = new Set()

  object.traverse((child) => {
    if ((child as THREE.Mesh).isMesh) {
      const mesh = child as THREE.Mesh
      const geometry = mesh.geometry

      if (geometry.index) {
        triangles += geometry.index.count / 3
      } else if (geometry.attributes.position) {
        triangles += geometry.attributes.position.count / 3
      }

      if (geometry.attributes.position) {
        vertices += geometry.attributes.position.count
      }

      if (mesh.material) {
        if (Array.isArray(mesh.material)) {
          mesh.material.forEach(m => materials.add(m))
        } else {
          materials.add(mesh.material)
        }
      }
    }
  })

  modelInfo.value = {
    triangles: Math.round(triangles),
    vertices,
    materials: materials.size
  }
}

// 重置相机
const resetCamera = () => {
  if (currentModel) {
    fitCameraToObject(currentModel)
  }
}

// 切换线框模式
const toggleWireframe = () => {
  wireframeEnabled.value = !wireframeEnabled.value

  if (currentModel) {
    currentModel.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mesh = child as THREE.Mesh
        if (Array.isArray(mesh.material)) {
          mesh.material.forEach(m => {
            if ('wireframe' in m) {
              (m as any).wireframe = wireframeEnabled.value
            }
          })
        } else {
          if ('wireframe' in mesh.material) {
            (mesh.material as any).wireframe = wireframeEnabled.value
          }
        }
      }
    })
  }
}

// 切换网格
const toggleGrid = () => {
  gridVisible.value = !gridVisible.value
  grid.visible = gridVisible.value
}

// 切换动画
const toggleAnimation = () => {
  animationPlaying.value = !animationPlaying.value
}

// 重新加载
const reload = () => {
  if (props.modelUrl) {
    loadModel(props.modelUrl)
  }
}

// 窗口调整
const handleResize = () => {
  if (!containerRef.value) return

  const width = containerRef.value.clientWidth
  const height = containerRef.value.clientHeight

  camera.aspect = width / height
  camera.updateProjectionMatrix()

  renderer.setSize(width, height)
}

// 清理资源
const cleanup = () => {
  if (animationFrameId) {
    cancelAnimationFrame(animationFrameId)
  }

  window.removeEventListener('resize', handleResize)

  if (renderer) {
    renderer.dispose()
  }

  if (controls) {
    controls.dispose()
  }
}

// 监听URL变化
watch(() => props.modelUrl, (newUrl) => {
  if (newUrl) {
    loadModel(newUrl)
  }
})

// 监听File对象变化
watch(() => props.modelFile, (newFile) => {
  if (newFile) {
    loadModelFromFile(newFile)
  }
})

onMounted(() => {
  initScene()

  // 优先使用File对象，其次使用URL
  if (props.modelFile) {
    loadModelFromFile(props.modelFile)
  } else if (props.modelUrl) {
    loadModel(props.modelUrl)
  }
})

onBeforeUnmount(() => {
  cleanup()
})
</script>

<style scoped>
.model-viewer {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 400px;
  border-radius: var(--border-radius-lg);
  overflow: hidden;
  background: #1a1a1a;
}

.loading-overlay,
.error-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(8px);
  color: white;
  z-index: 10;
}

.spinner {
  width: 48px;
  height: 48px;
  border: 4px solid rgba(255, 255, 255, 0.2);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.error-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
}

.btn-retry {
  margin-top: 1rem;
  padding: 0.75rem 1.5rem;
  background: var(--gradient-primary-alt);
  color: white;
  border: none;
  border-radius: var(--border-radius);
  cursor: pointer;
  font-weight: 600;
  transition: all var(--transition-base);
}

.btn-retry:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-xl);
}

.controls {
  position: absolute;
  top: 1rem;
  right: 1rem;
  display: flex;
  gap: 0.5rem;
  z-index: 5;
}

.control-btn {
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: var(--border-radius);
  color: white;
  cursor: pointer;
  transition: all var(--transition-base);
}

.control-btn:hover {
  background: rgba(255, 255, 255, 0.2);
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
}

.info-panel {
  position: absolute;
  bottom: 1rem;
  left: 1rem;
  display: flex;
  gap: 1rem;
  padding: 0.75rem 1rem;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(10px);
  border-radius: var(--border-radius);
  color: white;
  font-size: var(--font-size-sm);
  z-index: 5;
}

.info-item {
  display: flex;
  gap: 0.5rem;
}

.info-label {
  opacity: 0.7;
}

.info-value {
  font-weight: 600;
}
</style>
