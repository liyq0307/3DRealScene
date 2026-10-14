import { useAnalysisStore } from "@mars/stores/analysis"
import * as mapWork from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"

const Cesium = mars3d.Cesium
const store = useAnalysisStore() // 分析数据存储对象


// 空间距离
export async function measureLength() {
  const graphic = await mapWork.measure.distance({
    showAddText: true,
    style: {
      // color: "rgba(255, 255, 0, 1)",
      // width: 3,
      clampToGround: false // 是否贴地
    },
    label: {
      font_family: "楷体",
      font_size: 20,
      color: "rgba(255, 255, 255, 1)",
      background: false
    }
  })

  store.saveAnalysisDataToSelected({ analysisType: "kjjl_distance", data: { data: graphic.toJSON(), shijiao: mapWork.map.getCameraView(), analysisType: "kjjl_distance" } })
}


// 贴地距离
export async function measureSurfaceLength() {
  const graphic = await mapWork.measure.distanceSurface({
    showAddText: true,
    minDistance: 10,
    exact: false, // 是否进行精确计算， 传false时是否快速概略计算方式，该方式计算精度较低，但计算速度快，仅能计算在当前视域内坐标的高度
    // unit: 'm', //支持传入指定计量单位
    // style: {
    //   color: 'rgba(255, 255, 0, 1)',
    //   width: 3,
    //   clampToGround: true //是否贴地
    // }
    label: {
      font_family: "楷体",
      font_size: 20,
      color: "rgba(255, 255, 255, 1)",
      background: false
    }
  })
  store.saveAnalysisDataToSelected({ analysisType: "tdjl_distance", data: { data: graphic.toJSON(), shijiao: mapWork.map.getCameraView(), analysisType: "tdjl_distance" } })
  // showInterLineResult(graphic._measured.positionsSurface)
}


// 水平面积
export async function measureArea() {
  const graphic = await mapWork.measure.area({
    // style: {
    //   color: '#00fff2',
    //   opacity: 0.4,
    //   outline: true,
    //   outlineColor: '#fafa5a',
    //   outlineWidth: 1,
    //   clampToGround: false //贴地
    // }
    label: {
      font_family: "楷体",
      font_size: 20,
      color: "rgba(255, 255, 255, 1)",
      background: false
    }
  })

  // 下面代码抬升面的高度到一个平面，来示意“水平”
  if (mapWork.map.scene.mode === Cesium.SceneMode.SCENE3D) {
    const oldPositions = graphic.positionsShow
    const rang = await mars3d.PolyUtil.getHeightRangeByDepth(oldPositions, mapWork.map.scene)
    graphic.positions = mars3d.PointUtil.setPositionsHeight(oldPositions, rang.maxHeight)
  }

  store.saveAnalysisDataToSelected({ analysisType: "spmj_area", data: { data: graphic.toJSON(), shijiao: mapWork.map.getCameraView(), analysisType: "spmj_area" } })
}

// 贴地面积
export async function measureSurfaceeArea() {
  const graphic = await mapWork.measure.areaSurface({
    style: {
      color: "rgba(255, 255, 0, 1)"
    },
    label: {
      font_family: "楷体",
      font_size: 20,
      color: "rgba(255, 255, 255, 1)",
      background: false
    },
    splitNum: 10, // step插值分割的个数
    exact: true // 是否进行精确计算， 传false时是否快速概略计算方式，该方式计算精度较低，但计算速度快，仅能计算在当前视域内坐标的高度
  })
  store.saveAnalysisDataToSelected({ analysisType: "tdmj_area", data: { data: graphic.toJSON(), shijiao: mapWork.map.getCameraView(), analysisType: "tdmj_area" } })
}
// 高度差
export async function measureHeight() {
  const graphic = await mapWork.measure.height({
    label: {
      font_family: "楷体",
      font_size: 20,
      color: "rgba(255, 255, 255, 1)",
      background: false
    }
  })
  store.saveAnalysisDataToSelected({ analysisType: "gdc_height", data: { data: graphic.toJSON(), shijiao: mapWork.map.getCameraView(), analysisType: "gdc_height" } })
}

// 三角测量
export async function measureTriangleHeight() {
  const graphic = await mapWork.measure.heightTriangle({
    label: {
      font_family: "楷体",
      font_size: 20,
      color: "rgba(255, 255, 255, 1)",
      background: false
    }
  })
  store.saveAnalysisDataToSelected({ analysisType: "sjcl_triangle", data: { data: graphic.toJSON(), shijiao: mapWork.map.getCameraView(), analysisType: "sjcl_triangle" } })
}

// 方位角
export async function measureAngle() {
  const graphic = await mapWork.measure.angle({
    label: {
      font_family: "楷体",
      font_size: 20,
      color: "rgba(255, 255, 255, 1)",
      background: false
    }
  })
  store.saveAnalysisDataToSelected({ analysisType: "fwj_direction", data: { data: graphic.toJSON(), shijiao: mapWork.map.getCameraView(), analysisType: "fwj_direction" } })
}

// 坐标测量
export async function measurePoint() {
  const graphic = await mapWork.measure.point({
    popupOptions: {
      pointerEvents: true
    },
    popup: function (point, event) {
      return `<div class="mars3d-template-container">
        <div class="mars3d-template-title">位置信息</div>
        <div class="mars3d-template-content">
            <div><label>经度</label>${point.lng}</div>
            <div><label>纬度</label>${point.lat}</div>
            <div><label>海拔</label>${point.alt}米</div>
        </div>
      </div>`
    }
  })
  console.log("标绘完成", graphic.toJSON())
  store.saveAnalysisDataToSelected({ analysisType: "zbcl_point", data: { data: graphic.toJSON(), shijiao: mapWork.map.getCameraView(), analysisType: "zbcl_point" } })
}
