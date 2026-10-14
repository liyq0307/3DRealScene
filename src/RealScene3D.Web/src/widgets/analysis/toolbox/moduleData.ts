// 模块数据配置
// icon 字段使用 IconPark kebab-case 名称,由 mars-icon 组件渲染
export const moduleData = [
  {
    key: "basicTools",
    title: "基础工具",
    children: [
      { name: "坐标定位", icon: "coordinate-system", fullWidth: false, analysisType: "zbdw_point", show: true },
      { name: "压平", icon: "level", fullWidth: false, analysisType: "yp_ground", show: true },
      { name: "图上标记", icon: "pencil", fullWidth: false, analysisType: "basic_draw", show: true },
      { name: "多视角", icon: "eyes", fullWidth: false, analysisType: "gct_point", show: true }
    ]
  },
  {
    key: "measurement",
    title: "测量",
    children: [
      { name: "空间距离", icon: "ruler", fullWidth: false, analysisType: "kjjl_distance", show: true },
      { name: "贴地距离", icon: "map-distance", fullWidth: false, analysisType: "tdjl_distance", show: true },
      { name: "高度差", icon: "up-and-down", fullWidth: false, analysisType: "gdc_height", show: true },
      { name: "水平面积", icon: "square", fullWidth: false, analysisType: "spmj_area", show: true },
      { name: "贴地面积", icon: "area-map", fullWidth: false, analysisType: "tdmj_area", show: true },
      { name: "坐标测量", icon: "aiming", fullWidth: false, analysisType: "zbcl_point", show: true },
      { name: "三角测量", icon: "pyramid", fullWidth: false, analysisType: "sjcl_triangle", show: true },
      { name: "方位角", icon: "compass", fullWidth: false, analysisType: "fwj_direction", show: true }
    ]
  },
  {
    key: "sitePlanning",
    title: "选址与规划",
    children: [
      { name: "在线选址", icon: "pin", fullWidth: false, analysisType: "zxxz_online", show: true },
      { name: "市政三维", icon: "cube", fullWidth: false, analysisType: "szs3d_design", show: true },
      { name: "道路铺设", icon: "road", fullWidth: false, analysisType: "dlps", show: false },
      { name: "塔基建模", icon: "tower", fullWidth: false, analysisType: "tjjm_pole", show: true },
      { name: "管线分析", icon: "tool-kit", fullWidth: false, analysisType: "gxfx_pipe", show: true },
      { name: "限制分析", icon: "forbid", fullWidth: true, analysisType: "zbdkxzx_analysis", show: true }
    ]
  },
  {
    key: "spatialEnvironment",
    title: "空间形态与环境",
    children: [
      { name: "业态分析", icon: "factory-building", fullWidth: false, analysisType: "yt_analysis", show: true },
      { name: "通视分析", icon: "find-one", fullWidth: false, analysisType: "ts_analysis", show: true },
      { name: "日照分析", icon: "sun", fullWidth: false, analysisType: "rx_analysis", show: true },
      { name: "天际线", icon: "building-three", fullWidth: false, analysisType: "tjx_analysis", show: true },
      { name: "建筑布局", icon: "layout-four", fullWidth: false, analysisType: "jzbj_analysis", show: true },
      { name: "容积率", icon: "building-one", fullWidth: false, analysisType: "rjl_analysis", show: true },
      { name: "多建筑间距", icon: "vertical-spacing-between-items", fullWidth: false, analysisType: "djzjx_analysis", show: true }
    ]
  },
  {
    key: "engineeringAnalysis",
    title: "工程分析",
    children: [
      { name: "剖面分析", icon: "chart-line-area", fullWidth: false, analysisType: "pmfx_analysis", show: true },
      { name: "等高线", icon: "mountain", fullWidth: false, analysisType: "dgx_analysis", show: true },
      { name: "挖方填方", icon: "chart-histogram", fullWidth: false, analysisType: "wftffx_analysis", show: true }
    ]
  },
  {
    key: "designComparison",
    title: "设计对比",
    children: [{ name: "卷帘对比", icon: "switch-track", fullWidth: false, analysisType: "jlzd_comparison", show: true }]
  }
]
