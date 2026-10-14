<!--
  地图渲染组件 (建议使用mars3d地图的地方都用该组件)
  @copyright 火星科技 mars3d.cn
  @author 木遥 2024-12-03
-->
<template>
  <div :id="withKeyId" class="mars3d-container"></div>
</template>
<script setup lang="ts">
import * as mars3d from "mars3d"
import "./expand/index" // 引入插件或注册扩展js
import { getDefaultContextMenu } from "@mars/utils/getDefaultContextMenu"

import { computed, onUnmounted, onMounted, toRaw } from "vue"
import { $alert, $message } from "@mars/components/mars-ui/index"
import { logInfo } from "@mars/utils/mars-util"

import { readfileConfig } from "@mars/api/admin"
import { useWidget } from "@mars/common/store/widget"
import {
  Close,
  HistoryQuery,
  LandSurveying,
  Layers,
  Ruler,
  Analysis,
  Navigation,
  Mark,
  Bookmark,
  Printer,
  TakeOff,
  HandPaintedPlate,
  SwitchContrast,
  FullScreenPlay,
  Local,
  Tool
} from "@icon-park/svg"
const { activate, disableAll, isActivate, disable } = useWidget()

const props = withDefaults(
  defineProps<{
    mapKey?: string // 多个地图时,可传入key区分地图
    url?: string // 传入的地图构造参数url，可为空，只传options
    options?: any // 传入的地图构造参数options，可覆盖url内的参数
  }>(),
  {
    mapKey: "default",
    url: undefined,
    options: undefined
  }
)

// 用于存放地球组件实例
let map: mars3d.Map // 地图对象

// 使用用户传入的 mapKey 拼接生成 withKeyId 作为当前显示容器的id
const withKeyId = computed(() => `mars3d-container-${props.mapKey}`)

// onload事件将在地图渲染后触发
const emit = defineEmits(["onload"])
// 接口获取config.json数据，初始化地图
const initConfig = async () => {
  // console.log("initConfig", props.mapKey)
  const config = "config.json"

  const res: any = await readfileConfig(config)
  const configData = JSON.parse(res.data.result)
  if (!configData) {
    return
  }

  initMap(configData)
}
const initMap = (configData: any) => {
  // 获取配置
  if (configData.map3d) {
    initMars3d(configData.map3d)
  } else {
    initMars3d(configData)
  }
}
const initMars3d = async (option: any) => {
  option = mars3d.Util.merge(option, toRaw(props.options)) // 合并配置
  map = new mars3d.Map(withKeyId.value, option)

  // 针对不同终端的优化配置
  if (mars3d.Util.isPCBroswer()) {
    map.zoomFactor = 2.0 // 鼠标滚轮放大的步长参数

    // IE浏览器优化
    if (window.navigator.userAgent.toLowerCase().indexOf("msie") >= 0) {
      map.viewer.targetFrameRate = 20 // 限制帧率
      map.scene.requestRenderMode = false // 取消实时渲染
    }
  } else {
    map.zoomFactor = 5.0 // 鼠标滚轮放大的步长参数

    // 移动设备上禁掉以下几个选项，可以相对更加流畅
    map.scene.requestRenderMode = false // 取消实时渲染
    map.scene.fog.enabled = false
    map.scene.skyAtmosphere.show = false
    map.scene.globe.showGroundAtmosphere = false
  }

  // 二三维切换不用动画
  if (map.viewer.sceneModePicker) {
    map.viewer.sceneModePicker.viewModel.duration = 0.0
  }

  // console.log("获取右键菜单", map.getContextMenu())
  // 绑定当前项目的默认右键菜单
  // map.bindContextMenu(getContextMenu())

  // webgl渲染失败后，刷新页面
  map.on(mars3d.EventType.renderError, async () => {
    await $alert("程序内存消耗过大，请重启浏览器")
    window.location.reload()
  })

  // onMapLoad() // map构造完成后的一些处理

  map.scene.globe.maximumScreenSpaceError = 4 / 3
  emit("onload", map)
  map.control.toolbar.container.style.right = "32px"
  console.log("地图版本", mars3d.version)
  // 监听主地图相机变化，发布全局事件
  // map.on(mars3d.EventType.cameraChanged, () => {
  //   const camera = map.getCameraView()
  //   // console.log("主地图相机变化", camera)
  //   window.dispatchEvent(new CustomEvent("mainMapCameraChanged", { detail: { camera } }))
  // })
}

// map构造完成后的一些处理，可以按需注释和选用
function onMapLoad() {
  // Mars3D地图内部使用，如右键菜单弹窗
  // @ts-ignore
  window.globalAlert = $alert
  // @ts-ignore
  window.globalMsg = $message
}

function getContextMenu() {
  const contextmenu: any = getDefaultContextMenu(map)

  const children = []
  const iconStyle: any = { theme: "outline", fill: "#fff", size: "18" }
  const widgetList = [
    // { name: "底图切换", icon: LandSurveying(iconStyle), widget: "manage-basemap" },
    // { name: "图层管理", icon: Layers(iconStyle), widget: "manage-layers" },
    { name: "图上量算", icon: Ruler(iconStyle), widget: "measure" },
    // { name: "空间分析", icon: Analysis(iconStyle), widget: "analysis" },
    { name: "坐标定位", icon: Local(iconStyle), widget: "location-point" }
    // { name: "地区导航", icon: Navigation(iconStyle), widget: "location-region" },
    // { name: "我的标记", icon: Mark(iconStyle), widget: "addmarker" },
    // { name: "视角书签", icon: Bookmark(iconStyle), widget: "bookmark" },
    // { name: "地图打印", icon: Printer(iconStyle), widget: "print" },
    // { name: "飞行漫游", icon: TakeOff(iconStyle), widget: "roamLine-list" },
    // { name: "图上标绘", icon: HandPaintedPlate(iconStyle), widget: "plot" },
    // { name: "卷帘对比", icon: SwitchContrast(iconStyle), widget: "map-split" },
    // { name: "分屏对比", icon: FullScreenPlay(iconStyle), widget: "map-compare" },
    // { name: "兴趣点查询", icon: HistoryQuery(iconStyle), widget: "query-poi" }
  ]
  widgetList.forEach((item) => {
    children.push({
      name: item.name,
      widget: item.widget,
      text: function () {
        if (isActivate(this.widget)) {
          return "关闭" + this.name
        } else {
          return this.name
        }
      },
      icon: item.icon,
      callback: function (e) {
        if (isActivate(this.widget)) {
          disable(this.widget)
        } else {
          activate(this.widget)
        }
      }
    })
  })
  children.push({
    text: "关闭所有",
    icon: Close(iconStyle),
    callback: function (e) {
      disableAll()
    }
  })

  // contextmenu.push()
  contextmenu.splice(2, 0, {
    text: "常用工具",
    icon: Tool(iconStyle),
    children
  })

  return contextmenu
}

onMounted(() => {
  initConfig()
})
// 组件卸载之前销毁mars3d实例
onUnmounted(() => {
  if (map) {
    map.destroy()
    map = null
  }
  logInfo("map销毁完成", map)
})
</script>

<style lang="less">
/**给控件工具按钮栏加边框样式*/
.cesium-button,
.cesium-geocoder-searchButton,
.cesium-viewer-geocoderContainer form .cesium-geocoder-input,
.mars3d-contextmenu-ul,
.mars3d-contextmenu-line {
  border-image: url("https://data.mars3d.cn/img/control/border.svg") 1 round stretch;
}
.mars3d-contextmenu-ul .mars3d-contextmenu-icon{
  top: 10px;
}
.cesium-viewer-toolbar .cesium-button{
  background-color: rgb(1 11 50);
}
/* 自定义Popup样式*/
.mars3d-popup-btn-custom {
  padding: 3px 10px;
  border: 1px solid #209ffd;
  background: #209ffd1c;
  color: #ffffff;
}

/* 自定义Popup样式*/
.mars3d-popup-btn-custom {
  padding: 3px 10px;
  border: 1px solid #209ffd;
  background: #209ffd1c;
  color: var(--mars-text-color, #ffffff);
}
</style>
