const styleConfig = {
  // 点状
  label: {
    name: "文字",
    primitive: true,
    style: [
      { name: "text", label: "内容", type: "textarea", defval: "" },

      {
        name: "font_family",
        label: "字体",
        type: "combobox",
        defval: "微软雅黑",
        data: [
          { label: "微软雅黑", value: "微软雅黑" },
          { label: "宋体", value: "宋体" },
          { label: "楷体", value: "楷体" },
          { label: "隶书", value: "隶书" },
          { label: "黑体", value: "黑体" }
        ]
      },
      { name: "font_size", label: "字体大小", type: "number", min: 3, step: 1, defval: 20.0 },
      {
        name: "font_weight",
        label: "是否加粗",
        type: "combobox",
        defval: "normal",
        data: [
          { label: "是", value: "bold" },
          { label: "否", value: "normal" }
        ]
      },
      {
        name: "font_style",
        label: "是否斜体",
        type: "combobox",
        defval: "normal",
        data: [
          { label: "是", value: "italic" },
          { label: "否", value: "normal" }
        ]
      },

      { name: "color", label: "颜色", type: "color", defval: "rgba(255, 255, 255, 1)" },
      {
        name: "outline",
        label: "是否衬色",
        type: "radio",
        defval(style) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineColor",
        label: "衬色颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ style, allStyle }) {
          return style?.outline
        }
      },
      {
        name: "outlineWidth",
        label: "衬色宽度",
        type: "number",
        min: 1,
        max: 5,
        step: 1,
        defval: 1.0,
        show({ style, allStyle }) {
          return style?.outline
        }
      },

      {
        name: "background",
        label: "是否背景",
        type: "radio",
        defval: false
      },
      {
        name: "backgroundColor",
        label: "背景颜色",
        type: "color",
        defval: "#000000",
        show({ style }) {
          return style?.background
        }
      },
      {
        name: "backgroundPadding",
        label: "背景内边距",
        type: "number",
        step: 1,
        defval: 5,
        show({ style }) {
          return style?.background
        }
      },

      { name: "pixelOffsetX", label: "横向偏移像素", type: "number", step: 1, defval: 0.0 },
      { name: "pixelOffsetY", label: "纵向偏移像素", type: "number", step: 1, defval: 0.0 },

      {
        name: "scaleByDistance",
        label: "是否按视距缩放",
        type: "radio",
        defval: false
      },
      {
        name: "scaleByDistance_far",
        label: "上限",
        type: "number",
        step: 1,
        defval: 1000000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_farValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 0.1,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_near",
        label: "下限",
        type: "number",
        step: 1,
        defval: 1000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_nearValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        desc: `在贴地状态下无效`,
        defval: null, // 不能给值，会导致高度值无效
        show({ style }) {
          return !style?.clampToGround && style?.type === "label"
        }
      },
      // {
      //   name: "heightReference",
      //   label: "贴地方式",
      //   type: "combobox",
      //   valType: "number",
      //   defval: 0,
      //   data: [
      //     { label: "不贴地", value: 0 },
      //     { label: "同时贴地形和模型", value: 1 },
      //     { label: "仅贴地形", value: 3 },
      //     { label: "仅贴模型", value: 5 }
      //   ],
      //   show({style,  graphicType}) {
      //     return !style?.diffHeight || style?.diffHeight !== 0
      //   }
      // },

      { name: "visibleDepth", label: "是否被遮挡", type: "radio", defval: true },
      {
        name: "addHeight",
        label: "偏移高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ graphicType }) {
          return graphicType !== "point" && graphicType !== "billboard" && graphicType !== "label"
        }
      }
    ]
  },
  canvasLabel: {
    name: "Canvas文字",
    style: [
      { name: "text", label: "内容", type: "textarea", defval: "" },

      {
        name: "font_family",
        label: "字体",
        type: "combobox",
        defval: "微软雅黑",
        data: [
          { label: "微软雅黑", value: "微软雅黑" },
          { label: "宋体", value: "宋体" },
          { label: "楷体", value: "楷体" },
          { label: "隶书", value: "隶书" },
          { label: "黑体", value: "黑体" }
        ]
      },
      { name: "font_size", label: "字体大小", type: "number", min: 3, step: 1, defval: 30.0 },
      {
        name: "font_weight",
        label: "是否加粗",
        type: "combobox",
        defval: "normal",
        data: [
          { label: "是", value: "bold" },
          { label: "否", value: "normal" }
        ]
      },
      {
        name: "font_style",
        label: "是否斜体",
        type: "combobox",
        defval: "normal",
        data: [
          { label: "是", value: "italic" },
          { label: "否", value: "normal" }
        ]
      },

      { name: "color", label: "颜色", type: "color", defval: "rgba(255, 255, 255, 1)" },

      { name: "spacing", label: "字间距", type: "number", min: 0, step: 1, defval: 0 },
      { name: "spacingRow", label: "行间距", type: "number", min: 0, step: 1, defval: 2 },

      { name: "stroke", label: "是否衬色", type: "radio", defval: false },
      {
        name: "strokeColor",
        label: "衬色颜色",
        type: "color",
        defval: "#000000",
        show({ style }) {
          return style?.stroke
        }
      },
      {
        name: "strokeWidth",
        label: "衬色宽度",
        type: "number",
        min: 1,
        max: 5,
        step: 1,
        defval: 3.0,
        show({ style }) {
          return style?.stroke
        }
      },

      {
        name: "background",
        label: "是否背景",
        type: "radio",
        defval: false
      },
      {
        name: "backgroundColor",
        label: "背景颜色",
        type: "color",
        defval: "#000000",
        show({ style }) {
          return style?.background
        }
      },
      {
        name: "backgroundPadding",
        label: "背景内边距",
        type: "number",
        step: 1,
        defval: 5,
        show({ style }) {
          return style?.background
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ allStyle }) {
          return allStyle?.outline
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 1,
        max: 10,
        step: 1,
        defval: 4.0,
        show({ allStyle }) {
          return allStyle?.outline
        }
      },

      { name: "pixelOffsetX", label: "横向偏移像素", type: "number", step: 1, defval: 0.0 },
      { name: "pixelOffsetY", label: "纵向偏移像素", type: "number", step: 1, defval: 0.0 },

      {
        name: "scaleByDistance",
        label: "是否按视距缩放",
        type: "radio",
        defval: false
      },
      {
        name: "scaleByDistance_far",
        label: "上限",
        type: "number",
        step: 1,
        defval: 1000000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_farValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 0.1,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_near",
        label: "下限",
        type: "number",
        step: 1,
        defval: 1000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_nearValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        desc: `在贴地状态下无效`,
        defval: null, // 不能给值，会导致高度值无效
        show({ style }) {
          return !style?.clampToGround
        }
      },
      { name: "visibleDepth", label: "是否被遮挡", type: "radio", defval: true }
    ]
  },
  point: {
    name: "点标记",
    primitive: true,
    extends: ["pointMeasure"],
    style: [
      { name: "pixelSize", label: "像素大小", type: "number", step: 1, defval: 10.0 },
      { name: "color", label: "颜色", type: "color", defval: "rgba(255, 255, 255, 1)" },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ allStyle }) {
          return allStyle?.outline
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 0,
        show({ allStyle }) {
          return allStyle?.outline
        }
      },

      {
        name: "scaleByDistance",
        label: "是否按视距缩放",
        type: "radio",
        defval: false
      },
      {
        name: "scaleByDistance_far",
        label: "上限",
        type: "number",
        step: 1,
        defval: 1000000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_farValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 0.1,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_near",
        label: "下限",
        type: "number",
        step: 1,
        defval: 1000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_nearValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      { name: "visibleDepth", label: "是否被遮挡", type: "radio", defval: true },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        desc: `在贴地状态下无效`,
        defval: null, // 不能给值，会导致高度值无效
        show({ style }) {
          return !style?.clampToGround
        }
      }
    ]
  },
  billboard: {
    name: "图标点标记",
    primitive: true,
    extends: ["divBillboard", "divBillboardP", "canvasBillboard"],
    style: [
      {
        name: "image",
        label: "图标",
        type: "label",
        defval: "",
        show({ parentType }) {
          return !parentType
        }
      },
      {
        name: "image",
        label: "图标",
        type: "combobox",
        defval: "",
        data: [
          { label: "红色扩散点", value: "https://data.mars3d.cn/img/marker/point-red.png" },
          { label: "蓝色环绕", value: "https://data.mars3d.cn/img/marker/mark-blue.png" },
          { label: "蓝色人员", value: "https://data.mars3d.cn/img/marker/lace-blue.png" },
          { label: "黄色禁止", value: "https://data.mars3d.cn/img/marker/lace-yellow.png" }
        ],
        show({ parentType }) {
          return parentType
        }
      },
      { name: "opacity", label: "透明度", type: "slider", defval: 1.0, min: 0, max: 1, step: 0.01 },
      { name: "scale", label: "大小比例", type: "number", step: 1, defval: 1.0 },
      { name: "rotationDegree", label: "旋转角度", type: "number", step: 1, defval: 0.0 },

      {
        name: "horizontalOrigin",
        label: "横向对齐",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "左边", value: -1 },
          { label: "居中", value: 0 },
          { label: "右边", value: 1 }
        ]
      },
      {
        name: "verticalOrigin",
        label: "垂直对齐",
        type: "combobox",
        valType: "number",
        defval: 1,
        data: [
          { label: "顶部", value: -1 },
          { label: "居中", value: 0 },
          { label: "底部", value: 1 }
        ]
      },

      {
        name: "scaleByDistance",
        label: "是否按视距缩放",
        type: "radio",
        defval: false
      },
      {
        name: "scaleByDistance_far",
        label: "上限",
        type: "number",
        step: 1,
        defval: 1000000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_farValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 0.1,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_near",
        label: "下限",
        type: "number",
        step: 1,
        defval: 1000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_nearValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      { name: "visibleDepth", label: "是否被遮挡", type: "radio", defval: true },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        desc: `在贴地状态下无效`,
        defval: null, // 不能给值，会导致高度值无效
        show({ style }) {
          return !style?.clampToGround
        }
      },
      { name: "color", label: "叠加颜色", type: "color", defval: "rgba(255, 255, 255, 1)" }
    ]
  },
  div: {
    name: "DIV点标记",
    extends: ["divBoderLabel", "divLightPoint", "divUpLabel", "popup", "tooltip", "divIndicator"],
    style: [
      {
        name: "html",
        label: "Html文本",
        type: "label",
        defval: "",
        show({ graphicType }) {
          return graphicType === "div"
        }
      },
      {
        name: "text",
        label: "内容",
        type: "input",
        defval: "",
        show({ graphicType }) {
          return graphicType === "divUpLabel" || graphicType === "divBoderLabel"
        }
      },
      {
        name: "color",
        label: "颜色",
        type: "color",
        defval: "#00ffff",
        show({ graphicType }) {
          return graphicType === "divLightPoint" || graphicType === "divBoderLabel" || graphicType === "divUpLabel"
        }
      },
      {
        name: "boderColor",
        label: "边框颜色",
        type: "color",
        defval: "#00ffff",
        show({ graphicType }) {
          return graphicType === "divBoderLabel"
        }
      },
      {
        name: "size",
        label: "大小",
        type: "number",
        step: 1,
        defval: 10,
        show({ graphicType }) {
          return graphicType === "divLightPoint"
        }
      },

      {
        name: "horizontalOrigin",
        label: "横向定位",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "左边", value: 1 },
          { label: "居中", value: 0 },
          { label: "右边", value: -1 }
        ]
      },
      {
        name: "verticalOrigin",
        label: "垂直定位",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "顶部", value: -1 },
          { label: "居中", value: 0 },
          { label: "底部", value: 1 }
        ]
      },
      {
        name: "offsetX",
        label: "横向偏移",
        type: "number",
        step: 1,
        defval: 0
      },
      {
        name: "offsetY",
        label: "垂直偏移",
        type: "number",
        step: 1,
        defval: 0
      },
      { name: "scale", label: "缩放比例", type: "number", step: 1, defval: 1.0 },
      {
        name: "scaleByDistance",
        label: "是否按视距缩放",
        type: "radio",
        defval: false
      },
      {
        name: "scaleByDistance_far",
        label: "上限",
        type: "number",
        step: 1,
        defval: 1000000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_farValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 0.1,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_near",
        label: "下限",
        type: "number",
        step: 1,
        defval: 1000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_nearValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      { name: "testPoint", label: "是否显示测试点", type: "radio", defval: false },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        desc: `在贴地状态下无效`,
        defval: null, // 不能给值，会导致高度值无效
        show({ style }) {
          return !style?.clampToGround
        }
      }
    ]
  },
  divPlane: {
    name: "DIV三维平面",
    style: [
      { name: "html", label: "Html文本", type: "label", defval: "" },
      { name: "scale", label: "缩放比例", type: "number", step: 1, defval: 1.0 },
      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 90.0 },

      {
        name: "horizontalOrigin",
        label: "横向定位",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "左边", value: 1 },
          { label: "居中", value: 0 },
          { label: "右边", value: -1 }
        ]
      },
      {
        name: "verticalOrigin",
        label: "垂直定位",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "顶部", value: -1 },
          { label: "居中", value: 0 },
          { label: "底部", value: 1 }
        ]
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },

      { name: "testPoint", label: "是否显示测试点", type: "radio", defval: false }
    ]
  },
  divGif: {
    name: "动态图标",
    style: [
      {
        name: "image",
        label: "图标",
        type: "label"
      },
      {
        name: "width",
        label: "宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0
      },
      { name: "height", label: "高度", type: "number", min: 0, step: 1, defval: 0.0 }
    ]
  },
  billboardIndicator: {
    name: "可拖拽图标点",
    style: [
      { name: "opacity", label: "透明度", type: "slider", defval: 1.0, min: 0, max: 1, step: 0.01 },
      { name: "scale", label: "大小比例", type: "number", step: 1, min: 0, defval: 1.0 },

      {
        name: "scaleByDistance",
        label: "是否按视距缩放",
        type: "radio",
        defval: false
      },
      {
        name: "scaleByDistance_far",
        label: "上限",
        type: "number",
        step: 1,
        defval: 1000000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_farValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 0.1,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_near",
        label: "下限",
        type: "number",
        step: 1,
        defval: 1000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_nearValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      { name: "visibleDepth", label: "是否被遮挡", type: "radio", defval: true }
    ]
  },

  fontBillboard: {
    name: "字体点标记",
    style: [
      { name: "iconClass", label: "Class样式", type: "label", defval: "fa fa-automobile" },
      { name: "iconSize", label: "图标大小", type: "number", step: 1, defval: 50 },

      { name: "color", label: "颜色", type: "color", defval: "#00ffff" },

      {
        name: "horizontalOrigin",
        label: "横向对齐",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "左边", value: 1 },
          { label: "居中", value: 0 },
          { label: "右边", value: -1 }
        ]
      },
      {
        name: "verticalOrigin",
        label: "垂直对齐",
        type: "combobox",
        valType: "number",
        defval: 1,
        data: [
          { label: "顶部", value: -1 },
          { label: "居中", value: 0 },
          { label: "底部", value: 1 }
        ]
      },

      { name: "rotationDegree", label: "旋转角度", type: "number", step: 1, defval: 0.0 },

      {
        name: "scaleByDistance",
        label: "是否按视距缩放",
        type: "radio",
        defval: false
      },
      {
        name: "scaleByDistance_far",
        label: "上限",
        type: "number",
        step: 1,
        defval: 1000000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_farValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 0.1,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_near",
        label: "下限",
        type: "number",
        step: 1,
        defval: 1000.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },
      {
        name: "scaleByDistance_nearValue",
        label: "比例值",
        type: "number",
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.scaleByDistance
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        desc: `在贴地状态下无效`,
        defval: null, // 不能给值，会导致高度值无效
        show({ style }) {
          return !style?.clampToGround
        }
      },
      { name: "visibleDepth", label: "是否被遮挡", type: "radio", defval: true }
    ]
  },
  model: {
    name: "gltf模型",
    primitive: true,
    style: [
      {
        name: "url",
        label: "路径",
        type: "label",
        defval: "",
        show({ parentType }) {
          return !parentType
        }
      },
      {
        name: "url",
        label: "漫游模型",
        type: "combobox",
        defval: "",
        data: [
          { label: "行人", value: "https://data.mars3d.cn/gltf/mars/man/walk.gltf" },
          { label: "小汽车", value: "https://data.mars3d.cn/gltf/mars/qiche.gltf" },
          { label: "民航飞机", value: "https://data.mars3d.cn/gltf/mars/feiji.glb" },
          { label: "军用飞机", value: "https://data.mars3d.cn/gltf/mars/zhanji.glb" },
          { label: "航天卫星", value: "https://data.mars3d.cn/gltf/mars/weixin.gltf" }
        ],
        show({ parentType }) {
          return parentType
        }
      },

      { name: "scale", label: "比例", type: "number", step: 1, defval: 1.0 },
      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      { name: "minimumPixelSize", label: "最小像素大小", type: "number", step: 1, defval: 0.0 },

      {
        name: "opacity",
        label: "透明度",
        type: "slider",
        defval: 1.0,
        min: 0,
        max: 1,
        step: 0.01
      },
      {
        name: "lightColor",
        label: "明暗程度",
        type: "slider",
        defval: 1.0,
        min: 0.1,
        max: 10.0,
        step: 0.1
      },

      {
        name: "silhouette",
        label: "是否轮廓",
        type: "radio",
        defval: false
      },
      {
        name: "silhouetteColor",
        label: "轮廓颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ style }) {
          return style?.silhouette
        }
      },
      {
        name: "silhouetteSize",
        label: "轮廓宽度",
        type: "number",
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.silhouette
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      { name: "runAnimations", label: "是否动画", type: "radio", defval: true },

      { name: "hasShadows", label: "是否阴影", type: "radio", defval: true },
      {
        name: "flat",
        label: "不考虑光照",
        type: "radio",
        defval: false,
        show({ graphicType }) {
          return graphicType.endsWith("P")
        }
      },

      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      }
    ]
  },
  circle: {
    name: "圆",
    primitive: true,
    style: [
      { name: "radius", label: "半径", type: "number", step: 1, defval: 0.0 },
      {
        name: "height",
        label: "高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "diffHeight",
        label: "立体高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        step: 1,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: true
      },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color", defval: { color: "#fafafa" } },
          { label: "随机色", value: "RandomColor" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "图片2", value: "Image2", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" },

          { label: "放大扩散线", value: "ScanLine" },
          { label: "半径扫描", value: "CircleScan" },
          { label: "波纹扩散", value: "CircleWave" },
          { label: "雷达线", value: "RadarLine" },
          { label: "波纹雷达扫描", value: "RadarWave" }
        ],
        show({ style }) {
          return style?.fill !== false && (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
        }
      },
      {
        name: "stRotationDegree",
        label: "填充方向",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0,
        show({ style }) {
          return style?.fill !== false && style?.materialType !== "Color"
        }
      },
      {
        name: "rotationDegree",
        label: "旋转角度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.fill !== false
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.outline && !style?.diffHeight && typeof style?.outlineStyle?.width !== "number"
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineStyle?.color && !style?.outlineStyle?.materialType
        }
      },
      {
        name: "outlineStyle",
        next: "width",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        contant: "outlineWidth",
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineWidth && style?.outlineStyle?.width
        }
      },
      {
        name: "outlineStyle",
        next: "materialType",
        label: "边框材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color", contant: "outlineColor", defval: { color: "rgba(255, 255, 255, 1)" } },
          { label: "十字间隔", value: "LineCross" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "衬色线", value: "PolylineOutline" },
          { label: "光晕", value: "PolylineGlow" },
          { label: "泛光线", value: "LineBloom" },
          { label: "流动颜色", value: "LineFlowColor" },

          { label: "流动line", value: "LineFlow", defval: { image: "https://data.mars3d.cn/img/textures/fence-line.png", repeat_x: 10 } },
          { label: "流动fence", value: "LineFlow-1", defval: { image: "https://data.mars3d.cn/img/textures/fence.png", repeat_x: 10 } }
        ],
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineColor && (style?.outlineStyle?.color || style?.outlineStyle?.materialType)
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "flat",
        label: "不考虑光照",
        type: "radio",
        defval: false,
        show({ graphicType }) {
          return graphicType.endsWith("P")
        }
      },
      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "zIndex",
        label: "层级顺序",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.clampToGround
        }
      }
    ]
  },
  ellipse: {
    name: "椭圆",
    style: [
      { name: "semiMinorAxis", label: "短半径", type: "number", step: 1, defval: 0.0 },
      { name: "semiMajorAxis", label: "长半径", type: "number", step: 1, defval: 0.0 },
      {
        name: "diffHeight",
        label: "立体高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        step: 1,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: true
      },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color" },
          { label: "随机色", value: "RandomColor" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" },

          { label: "放大扩散线", value: "ScanLine" },
          { label: "半径扫描", value: "CircleScan" },
          { label: "波纹扩散", value: "CircleWave" },
          { label: "雷达线", value: "RadarLine" },
          { label: "波纹雷达扫描", value: "RadarWave" }
        ],
        show({ style }) {
          return style?.fill !== false && (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
        }
      },
      {
        name: "stRotationDegree",
        label: "填充方向",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0,
        show({ style }) {
          return style?.fill !== false && style?.materialType !== "Color"
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.outline && !style?.diffHeight && typeof style?.outlineStyle?.width !== "number"
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineStyle?.color && !style?.outlineStyle?.materialType
        }
      },
      {
        name: "outlineStyle",
        next: "width",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        contant: "outlineWidth",
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineWidth && style?.outlineStyle?.width
        }
      },
      {
        name: "outlineStyle",
        next: "materialType",
        label: "边框材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color", contant: "outlineColor", defval: { color: "rgba(255, 255, 255, 1)" } },
          { label: "十字间隔", value: "LineCross" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "衬色线", value: "PolylineOutline" },
          { label: "光晕", value: "PolylineGlow" },
          { label: "泛光线", value: "LineBloom" },
          { label: "流动颜色", value: "LineFlowColor" },

          { label: "流动line", value: "LineFlow", defval: { image: "https://data.mars3d.cn/img/textures/fence-line.png", repeat_x: 10 } },
          { label: "流动fence", value: "LineFlow-1", defval: { image: "https://data.mars3d.cn/img/textures/fence.png", repeat_x: 10 } }
        ],
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineColor && (style?.outlineStyle?.color || style?.outlineStyle?.materialType)
        }
      },

      { name: "rotationDegree", label: "旋转角度", type: "number", step: 1, defval: 0.0 },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "flat",
        label: "不考虑光照",
        type: "radio",
        defval: false,
        show({ graphicType }) {
          return graphicType.endsWith("P")
        }
      },
      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "zIndex",
        label: "层级顺序",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.clampToGround
        }
      }
    ]
  },
  cylinder: {
    name: "圆锥体",
    primitive: true,
    extends: ["coneTrack", "coneTrackP"],
    style: [
      { name: "topRadius", label: "顶部半径", type: "number", step: 1, defval: 0.0 },
      { name: "bottomRadius", label: "底部半径", type: "number", step: 1, defval: 100.0 },
      { name: "length", label: "锥体高度", type: "number", step: 1, defval: 100.0 },

      {
        name: "angle",
        label: "半场角度",
        type: "number",
        step: 0.01,
        defval: 0.01,
        min: 0.01,
        max: 89.99,
        show({ graphicType }) {
          return graphicType === "coneTrack" || graphicType === "coneTrackP"
        }
      },
      { name: "length", label: "锥体高度", type: "number", step: 1, defval: 0.0 },

      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color", defval: { color: "rgba(255, 255, 255, 1)" } },
          { label: "随机色", value: "RandomColor" },
          { label: "波纹扩散", value: "CircleWave" },
          { label: "条纹扩散", value: "CylinderWave" }
        ],
        show({ style }) {
          return style?.fill !== false && (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
        }
      },
      { name: "slices", label: "边线边数", type: "number", step: 1, defval: 128 },
      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      }
    ]
  },
  frustum: {
    name: "四棱椎体",
    style: [
      { name: "color", label: "颜色", type: "color", defval: "rgba(0,255,0,0.4)" },
      { name: "length", label: "长度", type: "number", min: 1.0, max: 999999999.0, step: 1.0, defval: 1.0 },
      { name: "angle", label: "夹角1", type: "slider", min: 0.1, max: 89.0, step: 0.01, defval: 1.0 },
      { name: "angle2", label: "夹角2", type: "slider", min: 0.1, max: 89.0, step: 0.01, defval: 1.0 },

      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ allStyle }) {
          return allStyle.outline
        }
      }
    ]
  },
  ellipsoid: {
    name: "球体",
    primitive: true,
    style: [
      { name: "radii_x", label: "X半径", type: "number", step: 1, min: 1, defval: 100.0 },
      { name: "radii_y", label: "Y半径", type: "number", step: 1, min: 1, defval: 100.0 },
      { name: "radii_z", label: "Z半径", type: "number", step: 1, min: 1, defval: 100 },

      { name: "innerRadii_x", label: "内部X半径", type: "number", step: 1, min: 1, defval: 0.0 },
      { name: "innerRadii_y", label: "内部Y半径", type: "number", step: 1, min: 1, defval: 0.0 },
      { name: "innerRadii_z", label: "内部Z半径", type: "number", step: 1, min: 1, defval: 0.0 },

      { name: "minimumClockDegree", label: "最小时钟角度", type: "number", defval: 0.0 },
      { name: "maximumClockDegree", label: "最大时钟角度", type: "number", defval: 360.0 },
      { name: "minimumConeDegree", label: "最小锥角", type: "number", defval: 0.0 },
      { name: "maximumConeDegree", label: "最大圆锥角", type: "number", defval: 180.0 },

      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: true
      },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color" },
          { label: "随机色", value: "RandomColor" },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "波纹", value: "EllipsoidWave" },
          { label: "电弧", value: "EllipsoidElectric" }
        ],
        show({ style }) {
          return style?.fill !== false && (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1,
        show({ allStyle }) {
          return allStyle.outline
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "#000000",
        show({ allStyle }) {
          return allStyle.outline
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      }
    ]
  },
  plane: {
    name: "平面",
    primitive: true,
    style: [
      { name: "dimensions_x", label: "长度", type: "number", step: 1, defval: 100.0 },
      { name: "dimensions_y", label: "宽度", type: "number", step: 1, defval: 100.0 },

      {
        name: "plane_normal",
        label: "方向",
        type: "combobox",
        defval: "z",
        data: [
          { label: "X轴", value: "x" },
          { label: "Y轴", value: "y" },
          { label: "Z轴", value: "z" }
        ]
      },
      { name: "plane_distance", label: "偏移距离", type: "number", step: 1, defval: 0.0 },

      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: true
      },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color" },
          { label: "随机色", value: "RandomColor" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" }
        ],
        show({ style }) {
          return style?.fill !== false && (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1,
        show({ allStyle }) {
          return allStyle.outline
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ allStyle }) {
          return allStyle.outline
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "flat",
        label: "不考虑光照",
        type: "radio",
        defval: false,
        show({ graphicType }) {
          return graphicType.endsWith("P")
        }
      }
    ]
  },
  viewShed: {
    name: "可视域",
    style: [
      { name: "angle", label: "水平角度", type: "slider", min: 1, max: 120, step: 0.1, defval: 60 },
      { name: "angle2", label: "垂直角度", type: "slider", min: 1, max: 120, step: 0.1, defval: 45 },
      { name: "distance", label: "投射距离", type: "number", min: 1, step: 0.1, defval: 100 },
      { name: "heading", label: "四周方向", type: "slider", min: 0, max: 360, step: 0.1, defval: 0 },
      { name: "pitch", label: "俯仰角度", type: "slider", min: -180, max: 180, step: 0.1, defval: 0 },
      { name: "opacity", label: "透明度", type: "slider", min: 0, max: 1, step: 0.1, defval: 1 },
      { name: "showFrustum", label: "显示视锥体", type: "radio", defval: false }
    ]
  },
  doubleSidedPlane: {
    name: "双面渲染图片平面",
    primitive: true,
    style: [
      { name: "image", label: "填充的图片", type: "label" },
      { name: "opacity", label: "透明度", type: "slider", min: 0, max: 1, step: 0.1, defval: 1 },
      { name: "noWhite", label: "不显示白色", type: "radio", defval: true },

      { name: "dimensions_x", label: "长度", type: "number", step: 1, defval: 100.0 },
      { name: "dimensions_y", label: "宽度", type: "number", step: 1, defval: 100.0 },

      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Image2",
        data: [
          { label: "图片", value: "Image2", defval: { image: "https://data.mars3d.cn/img/textures/wzplane.png" } },
          { label: "文本", value: "Text" },
          { label: "纯色", value: "Color" },
          { label: "网格", value: "Grid" },
          // { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" },

          { label: "渐变面", value: "PolyGradient" },
          { label: "双色渐变面", value: "PolyGradient2" },
          { label: "水面", value: "Water" },
          { label: "蓝光水面", value: "WaterLight" }
        ],
        show({ style, graphicType }) {
          return style?.fill !== false && graphicType !== "video2D"
        }
      }
    ]
  },
  box: {
    name: "盒子",
    primitive: true,
    style: [
      { name: "dimensions_x", label: "盒子长度", type: "number", step: 1, defval: 100.0 },
      { name: "dimensions_y", label: "盒子宽度", type: "number", step: 1, defval: 100.0 },
      { name: "dimensions_z", label: "盒子高度", type: "number", step: 1, defval: 100.0 },

      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: true
      },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color" },
          { label: "随机色", value: "RandomColor" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" }
        ],
        show({ style }) {
          return style?.fill !== false && (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
        }
      },
      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style) {
          return !!style?.outlineWidth || !!style?.outlineColor
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "#000000",
        show({ allStyle }) {
          return allStyle.outline
        }
      },
      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      }
    ]
  },
  particleSystem: {
    name: "粒子效果",
    style: [
      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      { name: "particleSize", label: "粒子大小", type: "slider", defval: 1.0, min: 0, max: 60.0, step: 1 },
      { name: "emissionRate", label: "发射速率", type: "slider", defval: 100.0, min: 0.0, max: 500.0, step: 1 },
      { name: "gravity", label: "重力因子", type: "slider", defval: 0.0, min: -20.0, max: 20.0, step: 0.1 },
      { name: "transX", label: "偏移值X", type: "slider", defval: 0.0, min: -50.0, max: 50.0, step: 0.1 },
      { name: "transY", label: "偏移值Y", type: "slider", defval: 0.0, min: -50.0, max: 50.0, step: 0.1 },
      { name: "transZ", label: "偏移值Z", type: "slider", defval: 0.0, min: -50.0, max: 50.0, step: 0.1 },

      { name: "startScale", label: "开始比例", type: "slider", defval: 1.0, min: 0.0, max: 10.0, step: 1 },
      { name: "endScale", label: "结束比例", type: "slider", defval: 1.0, min: 0.0, max: 10.0, step: 1 },
      { name: "minimumParticleLife", label: "最小寿命", type: "slider", defval: 3.0, min: 0.1, max: 30.0, step: 0.1 },
      { name: "maximumParticleLife", label: "最大寿命", type: "slider", defval: 6.0, min: 0.1, max: 30.0, step: 0.1 }
    ]
  },
  cloud: {
    name: "积云",
    style: [
      { name: "scaleX", label: "比例X", type: "number", step: 1, defval: 1.0 },
      { name: "scaleY", label: "比例Y", type: "number", step: 1, defval: 1.0 },
      { name: "maximumSizeX", label: "最大尺寸X", type: "number", step: 1, defval: 1.0 },
      { name: "maximumSizeY", label: "最大尺寸Y", type: "number", step: 1, defval: 1.0 },
      { name: "maximumSizeZ", label: "最大尺寸Z", type: "number", step: 1, defval: 1.0 },
      { name: "slice", label: "切片", type: "slider", defval: 1.0, min: 0, max: 1, step: 0.01 },
      { name: "brightness", label: "亮度", type: "slider", defval: 1.0, min: 0, max: 1, step: 0.01 }
    ]
  },
  lightCone: {
    name: "光锥体",
    style: [
      { name: "color", label: "颜色", type: "color", defval: "#00ffff" },
      { name: "radius", label: "底部半径", type: "number", min: 1.0, max: 999999999, step: 1, defval: 100.0 },
      { name: "height", label: "锥体高度", type: "number", min: 1.0, max: 999999999.0, step: 1, defval: 1000.0 },
      { name: "setHeight", label: "指定坐标高度", type: "number", min: 0.0, max: 999999999.0, step: 1, defval: 0.0 }
    ]
  },
  tetrahedron: {
    name: "四面体",
    style: [
      { name: "color", label: "颜色", type: "color", defval: "#000000" },
      { name: "width", label: "顶部大小", type: "number", step: 1, defval: 20.0 },
      { name: "height", label: "椎体高度", type: "number", step: 1, defval: 30.0 },

      { name: "animation", label: "是否动画", type: "radio", defval: true },
      {
        name: "moveHeight",
        label: "动画高度",
        type: "number",
        step: 1,
        defval: 30.0,
        show({ style }) {
          return style?.animation
        }
      },
      {
        name: "moveDuration",
        label: "动画时长",
        type: "number",
        min: 1.0,
        max: 999999999,
        step: 1,
        defval: 2.0,
        show({ style }) {
          return style?.animation
        }
      },
      {
        name: "rotationAngle",
        label: "旋转角度",
        type: "number",
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.animation
        }
      }
    ]
  },
  rectangularSensor: {
    name: "相控阵雷达范围",
    style: [
      { name: "color", label: "颜色", type: "color", defval: "rgba(0, 255, 0, 1)" },
      { name: "lineColor", label: "边线颜色", type: "color", defval: "rgba(255, 255, 255, 1)" },
      { name: "radius", label: "半径", type: "number", min: 1.0, max: 999999999, step: 1, defval: 1.0 },

      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      { name: "xHalfAngleDegree", label: "上下夹角", type: "slider", min: 0.0, max: 89.0, step: 0.01, defval: 1.0 },
      { name: "yHalfAngleDegree", label: "左右夹角", type: "slider", min: 0.0, max: 89.0, step: 0.01, defval: 1.0 },
      { name: "showScanPlane", label: "扫描面", type: "radio", defval: false },
      {
        name: "scanPlaneColor",
        label: "扫描面颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ style }) {
          return style?.showScanPlane
        }
      },
      {
        name: "scanPlaneMode",
        label: "扫描面方向",
        type: "combobox",
        defval: "vertical",
        data: [
          { label: "垂直方向", value: "vertical" },
          { label: "水平方向", value: "horizontal" }
        ],
        show({ style }) {
          return style?.showScanPlane
        }
      },
      {
        name: "scanPlaneRate",
        label: "扫描速率",
        type: "number",
        min: 1.0,
        max: 100,
        step: 0.1,
        defval: 3.0,
        show({ style }) {
          return style?.showScanPlane
        }
      }
    ]
  },
  camberRadar: {
    name: "双曲面雷达范围",
    style: [
      { name: "color", label: "颜色", type: "color", defval: "rgba(255, 255, 255, 1)" },
      { name: "radius", label: "内曲面半径", type: "number", min: 1.0, max: 999999999.0, step: 1, defval: 1.0, toFixed: 1 },
      {
        name: "startRadius",
        label: "外曲面半径",
        type: "number",
        min: 1.0,
        max: 999999999.0,
        step: 1,
        defval: 1.0,
        toFixed: 1
      },

      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      { name: "startFovH", label: "左横截面角度", type: "slider", min: -180.0, max: 180.0, step: 0.01, defval: 1.0 },
      { name: "endFovH", label: "右横截面角度", type: "slider", min: -180.0, max: 180.0, step: 0.01, defval: 1.0 },
      { name: "startFovV", label: "垂直起始角度", type: "slider", min: 0.0, max: 90.0, step: 0.01, defval: 1.0 },
      { name: "endFovV", label: "垂直结束角度", type: "slider", min: 0.0, max: 90.0, step: 0.01, defval: 1.0 }
    ]
  },

  jammingRadar: {
    name: "自定义干扰雷达",
    style: [
      { name: "scale", label: "大小比例", type: "slider", min: 0.1, max: 10.0, step: 0.1, defval: 1.0 },
      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "autoColor", label: "是否内置渐变色", type: "radio", defval: true },
      {
        name: "color",
        label: "颜色",
        type: "color",
        defval: "rgba(255,0,0,0.5)",
        show({ style }) {
          return !style?.autoColor
        }
      },
      {
        name: "outline",
        label: "是否显示边线",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineColor
        }
      },
      {
        name: "outlineColor",
        label: "边线颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ allStyle }) {
          return allStyle.outline
        }
      }
    ]
  },

  fixedJammingRadar: {
    name: "固定算法干扰雷达",
    style: [
      { name: "scale", label: "大小比例", type: "slider", min: 0.1, max: 10.0, step: 0.1, defval: 1.0 },
      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "autoColor", label: "是否内置渐变色", type: "radio", defval: true },
      {
        name: "color",
        label: "颜色",
        type: "color",
        defval: "rgba(255,0,0,0.5)",
        show({ style }) {
          return !style?.autoColor
        }
      },
      {
        name: "outline",
        label: "是否显示边线",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineColor
        }
      },
      {
        name: "outlineColor",
        label: "边线颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ allStyle }) {
          return allStyle.outline
        }
      },

      { name: "pt", label: "发射功率", type: "number", defval: 8e6 },
      { name: "gt", label: "天线主瓣增益", type: "number", defval: 500 },
      { name: "lambda", label: "信号波长(lambda)", type: "number", defval: 0.056 },
      { name: "sigma", label: "反射截面积(sigma)", type: "number", defval: 3 },
      { name: "n", label: "脉冲积累数", type: "number", defval: 16 },
      { name: "k", label: "玻尔兹曼常数", type: "number", defval: 138e-25 },
      { name: "t0", label: "绝对温度", type: "number", defval: 290 },
      { name: "bn", label: "接收机通频带宽度", type: "number", defval: 16e5 },
      { name: "fn", label: "接收机噪声系数", type: "number", defval: 5 },
      { name: "sn", label: "接收机最小可检测信噪比", type: "number", defval: 2 }
    ]
  },

  route: {
    name: "动态轨迹路线",
    primitive: false,
    extends: ["satellite", "fixedRoute"],
    style: [
      {
        name: "tle1",
        label: "tle1",
        type: "label",
        defval: "",
        show({ graphicType }) {
          return graphicType === "satellite"
        }
      },
      {
        name: "tle2",
        label: "tle2",
        type: "label",
        defval: "",
        show({ graphicType }) {
          return graphicType === "satellite"
        }
      }
    ]
  },
  conicSensor: {
    name: "卫星圆锥体",
    style: [
      { name: "color", label: "颜色", type: "color", defval: "rgba(255,0,0,0.4)" },
      { name: "length", label: "长度", type: "number", min: 1.0, max: 999999999.0, step: 1.0, defval: 1.0 },
      { name: "angle", label: "夹角", type: "slider", min: 1.0, max: 89.0, step: 0.01, defval: 1.0 },
      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      { name: "topShow", label: "显示顶盖", type: "radio", defval: true },
      { name: "shadowShow", label: "地面投影", type: "radio", defval: false }
    ]
  },
  rectSensor: {
    name: "卫星四棱椎体",
    style: [
      { name: "color", label: "颜色", type: "color", defval: "rgba(0,255,0,0.4)" },
      { name: "length", label: "长度", type: "number", min: 1.0, max: 999999999.0, step: 1.0, defval: 1.0 },
      { name: "angle1", label: "夹角1", type: "slider", min: 0.1, max: 89.0, step: 0.01, defval: 1.0 },
      { name: "angle2", label: "夹角2", type: "slider", min: 0.1, max: 89.0, step: 0.01, defval: 1.0 },

      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },

      { name: "topShow", label: "显示顶盖", type: "radio", defval: true },

      { name: "rayEllipsoid", label: "求交地球", type: "radio", defval: false }
    ]
  },
  pointLight: {
    name: "点光源",
    style: [
      { name: "color", label: "光颜色", type: "color", defval: "rgba(0,255,0,0.4)" },
      { name: "intensity", label: "光强度", type: "number", min: 1.0, max: 10000.0, step: 1, defval: 1.0 },
      { name: "radius", label: "点光源半径", type: "number", min: 1.0, max: 10000.0, step: 1, defval: 1.0 }
    ]
  },
  spotLight: {
    name: "聚光灯",
    style: [
      { name: "color", label: "光颜色", type: "color", defval: "rgba(0,255,0,0.4)" },
      { name: "intensity", label: "光强度", type: "number", min: 1.0, max: 999999999.0, step: 1, defval: 1.0 },
      { name: "radius", label: "聚光灯半径", type: "number", min: 1.0, max: 999999999.0, step: 1, defval: 1.0 },
      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "innerCone", label: "内圆锥角", type: "slider", min: 0.0, max: 45, step: 0.1, defval: 10.0 },
      { name: "outerCone", label: "外圆锥角", type: "slider", min: 0.0, max: 45, step: 0.1, defval: 10.0 }
    ]
  },

  pointVisibility: {
    name: "圆形可视域区域",
    style: [
      { name: "radius", label: "半径", type: "slider", min: 1.0, max: 3000.0, step: 1, defval: 1.0 },
      { name: "showFrustum", label: "视椎体框线", type: "radio", defval: false }
    ]
  },
  coneVisibility: {
    name: "扇形可视域区域",
    style: [
      { name: "radius", label: "半径", type: "slider", min: 1.0, max: 3000.0, step: 1, defval: 1.0 },
      { name: "heading", label: "方向角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "pitch", label: "俯仰角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "roll", label: "翻滚角", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      { name: "showFrustum", label: "视椎体框线", type: "radio", defval: false }
    ]
  },

  viewDome: {
    name: "开敞度分析球",
    style: [
      { name: "radius", label: "半径", type: "number", min: 1.0, max: 999999999.0, step: 1, defval: 1.0 },
      { name: "visibleColor", label: "可见区域颜色", type: "color", defval: "rgba(0,183,239, 0.5)" },
      { name: "hiddenColor", label: "不可见区域颜色", type: "color", defval: "rgba(227,108,9, 0.5)" }
    ]
  },

  // 线状
  polyline: {
    name: "线",
    primitive: true,
    extends: [
      "curve",
      "brushLine",
      "distanceMeasure",
      "heightMeasure",
      "distanceSurfaceMeasure",
      "distanceSurfaceMeasure",
      "heightTriangleMeasure",
      "angleMeasure",
      "sectionMeasure"
    ],
    style: [
      { name: "width", label: "线宽", type: "number", step: 1, defval: 4.0 },
      {
        name: "materialType",
        label: "线型",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "实线", value: "Color" },
          { label: "虚线", value: "PolylineDash" },
          { label: "虚线箭头", value: "LineDashArrow" },
          {
            label: "虚线箭头2",
            value: "LineFlow-dashArrow",
            defval: { image: "https://data.mars3d.cn/img/textures/line-dashArrow.png", speed: 0, repeat_x: 10 }
          },
          { label: "衬色线", value: "PolylineOutline" },
          { label: "箭头", value: "PolylineArrow" },
          { label: "光晕", value: "PolylineGlow" },

          { label: "OD线", value: "ODLine" },
          { label: "闪烁线", value: "LineFlicker" },
          { label: "轨迹线", value: "LineTrail" },
          { label: "泛光线", value: "LineBloom" },
          { label: "流动颜色", value: "LineFlowColor" },

          { label: "流动blue", value: "LineFlow", defval: { image: "https://data.mars3d.cn/img/textures/line-arrow-blue.png", repeat_x: 10 } },
          {
            label: "流动dovetail",
            value: "LineFlow-2",
            defval: { image: "https://data.mars3d.cn/img/textures/line-arrow-dovetail.png", repeat_x: 10 }
          },
          { label: "流动arrow", value: "LineFlow-3", defval: { image: "https://data.mars3d.cn/img/textures/line-arrow-right.png", repeat_x: 10 } },
          { label: "流动aqua", value: "LineFlow-4", defval: { image: "https://data.mars3d.cn/img/textures/line-color-aqua.png", repeat_x: 10 } },
          { label: "流动azure", value: "LineFlow-5", defval: { image: "https://data.mars3d.cn/img/textures/line-color-azure.png", repeat_x: 10 } },
          {
            label: "流动red",
            value: "LineFlow-6",
            defval: { image: "https://data.mars3d.cn/img/textures/line-color-red.png", color: "#ff0000", repeat_x: 10 }
          },
          {
            label: "流动yellow",
            value: "LineFlow-7",
            defval: { image: "https://data.mars3d.cn/img/textures/line-color-yellow.png", color: "rgba(255, 255, 0, 1)", repeat_x: 10 }
          },
          { label: "流动colour", value: "LineFlow-8", defval: { image: "https://data.mars3d.cn/img/textures/line-colour.png", repeat_x: 10 } },
          { label: "流动gradual", value: "LineFlow-9", defval: { image: "https://data.mars3d.cn/img/textures/line-gradual.png", repeat_x: 10 } },
          { label: "流动pulse", value: "LineFlow-10", defval: { image: "https://data.mars3d.cn/img/textures/line-pulse.png" } },
          { label: "流动sprite", value: "LineFlow-11", defval: { image: "https://data.mars3d.cn/img/textures/line-sprite.png", repeat_x: 10 } },
          { label: "流动tarans", value: "LineFlow-13", defval: { image: "https://data.mars3d.cn/img/textures/line-tarans.png" } },
          { label: "流动vertebral", value: "LineFlow-14", defval: { image: "https://data.mars3d.cn/img/textures/line-vertebral.png", repeat_x: 10 } },
          {
            label: "流动vertebral-blue",
            value: "LineFlow-15",
            defval: { image: "https://data.mars3d.cn/img/textures/line-vertebral-blue.png", repeat_x: 10 }
          },
          { label: "流动fence-line", value: "LineFlow-16", defval: { image: "https://data.mars3d.cn/img/textures/fence-line.png", repeat_x: 10 } },
          {
            label: "流动transarrow",
            value: "LineFlow-17",
            defval: { image: "https://data.mars3d.cn/img/textures/line-arrow-trans.png", repeat_x: 10 }
          },
          {
            label: "流动天青",
            value: "LineFlow-18",
            defval: { image: "https://data.mars3d.cn/img/textures/line-color-yellow.png", color: "#33e8df", repeat_x: 1 }
          },
          {
            label: "天青pulse",
            value: "LineFlow-19",
            defval: { image: "https://data.mars3d.cn/img/textures/line-color-yellow.png", color: "#33e8df", width: 8 }
          }
        ],
        show({ style }) {
          return style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true
        }
      },
      {
        name: "snakeAnimationDuration",
        label: "生长动画",
        type: "number",
        step: 1,
        defval: 0,
        show({ graphicType, parentType }) {
          return graphicType !== "polylineP" && parentType
        }
      },
      {
        name: "snakeAnimationDelay",
        label: "延迟执行动画",
        type: "number",
        step: 1,
        defval: 0,
        show({ graphicType, parentType }) {
          return graphicType !== "polylineP" && parentType
        }
      },
      { name: "closure", label: "是否闭合", type: "radio", defval: false },
      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      // {
      //   name: "arcType",
      //   label: "球面弧线规则",
      //   type: "combobox",
      //   valType: "number",
      //   defval: 0,
      //   data: [
      //     { label: "空间直线", value: 0 },
      //     { label: "大地弧线", value: 1 },
      //     { label: "方位线", value: 2 }
      //   ]
      // },

      {
        name: "zIndex",
        label: "层级顺序",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.clampToGround
        }
      },
      {
        name: "showAll",
        label: "展示所有坐标",
        type: "radio",
        defval: false,
        show({ style, parentType }) {
          // graphicType - 矢量对象类型，有可能作为参数传递；
          // parentType - 父类型，当矢量对象作为参数时才会有该参数
          return parentType && style?.show !== false && (parentType === "route" || parentType === "fixedRoute")
        }
      },
      {
        name: "maxDistance",
        label: "保留的轨迹长",
        type: "number",
        step: 1,
        defval: 9999,
        show({ parentType, allStyle, style }) {
          // graphicType - 矢量对象类型，有可能作为参数传递；
          // parentType - 父类型，当矢量对象作为参数时才会有该参数
          return parentType && style?.show !== false && !allStyle?.showAll && (parentType === "route" || parentType === "fixedRoute")
        }
      }
    ]
  },
  path: {
    name: "路径",
    style: [
      { name: "width", label: "线宽", type: "number", step: 1, defval: 1.0 },
      { name: "color", label: "颜色", type: "color", defval: "rgba(255, 255, 255, 1)" },
      { name: "leadTime", label: "轨迹前", type: "number", step: 1, defval: 9999 },
      { name: "trailTime", label: "轨迹保留", type: "number", step: 1, defval: 9999 }
    ]
  },
  polylineVolume: {
    name: "管道线",
    primitive: true,
    style: [
      {
        name: "shape",
        label: "形状",
        type: "combobox",
        defval: "pipeline",
        data: [
          { label: "空心管", value: "pipeline" },
          { label: "实心管", value: "circle" },
          { label: "星状管", value: "star" }
        ]
      },
      { name: "radius", label: "半径", type: "number", step: 1, defval: 10.0 },
      {
        name: "thicknes",
        label: "厚度",
        type: "number",
        step: 1,
        defval: 3.0,
        show({ style }) {
          return style?.shape === "pipeline"
        }
      },
      {
        name: "slices",
        label: "边线边数",
        type: "number",
        min: 1,
        max: 360,
        step: 1,
        defval: 90
      },

      {
        name: "startAngle",
        label: "开始角度",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.1,
        defval: 0.0
      },

      {
        name: "materialType",
        label: "材质类型",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "实线", value: "Color" },
          { label: "轨迹线", value: "LineTrail" },
          { label: "泛光线", value: "LineBloom" },
          { label: "流动颜色", value: "LineFlowColor" },

          { label: "流动line", value: "LineFlow", defval: { image: "https://data.mars3d.cn/img/textures/fence-line.png", repeat_x: 10 } },
          { label: "流动fence", value: "LineFlow-1", defval: { image: "https://data.mars3d.cn/img/textures/fence.png", repeat_x: 10 } }
        ],
        show({ style }) {
          return style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1,
        show({ allStyle }) {
          return allStyle?.outline
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "#000000",
        show({ allStyle }) {
          return allStyle?.outline
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      }
    ]
  },
  wall: {
    name: "墙体",
    primitive: true,
    style: [
      { name: "diffHeight", label: "墙高", type: "number", step: 1, defval: 100.0 },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        desc: `在贴地状态下无效`,
        defval: null, // 不能给值，会导致高度值无效
        show({ style }) {
          return !style?.clampToGround
        }
      },

      { name: "fill", label: "是否填充", type: "radio", defval: true },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color", defval: { color: "#fafafa" } },
          { label: "随机色", value: "RandomColor" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" },
          { label: "文本", value: "Text" },

          { label: "走马灯", value: "WallScroll" },

          { label: "流动arrow", value: "LineFlow", defval: { image: "https://data.mars3d.cn/img/textures/arrow.png", repeat_x: 10 } },
          { label: "流动arrowh", value: "LineFlow-1", defval: { image: "https://data.mars3d.cn/img/textures/arrow-h.png", repeat_x: 10 } },
          { label: "流动fence", value: "LineFlow-2", defval: { image: "https://data.mars3d.cn/img/textures/fence.png", axisY: true } },
          { label: "流动line", value: "LineFlow-3", defval: { image: "https://data.mars3d.cn/img/textures/fence-line.png", axisY: true } },
          {
            label: "图片fence",
            value: "LineFlow-4",
            defval: {
              image: "https://data.mars3d.cn/img/textures/fence.png",
              axisY: true,
              color: "#ff0000",
              image2: "https://data.mars3d.cn/img/textures/tanhao.png",
              color2: "rgba(255, 255, 0, 1)",
              outline: false,
              diffHeight: 1000,
              lastMaterialType: "Image"
            }
          }
        ],
        show({ style }) {
          return style?.fill !== false && (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
        }
      },

      { name: "closure", label: "是否闭合", type: "radio", defval: false },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        defval: 1.0,
        show({ allStyle }) {
          return allStyle?.outline
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ allStyle }) {
          return allStyle?.outline
        }
      },
      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false
      }
    ]
  },
  scrollWall: {
    name: "走马灯围墙",
    style: [
      { name: "diffHeight", label: "墙高", type: "number", step: 1, defval: 100.0 },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        desc: `在贴地状态下无效`,
        defval: null, // 不能给值，会导致高度值无效
        show({ style }) {
          return !style?.clampToGround
        }
      },
      { name: "color", label: "颜色", type: "color", defval: "rgba(51, 136, 255, 1)" },
      { name: "speed", label: "速度", type: "number", step: 1, defval: 10 },
      { name: "reverse", label: "方向", type: "radio", defval: true },
      {
        name: "style",
        label: "样式",
        type: "combobox",
        defval: 1,
        data: [
          { label: "样式1", value: 1 },
          { label: "样式2", value: 2 }
        ]
      }
    ]
  },
  thickWall: {
    name: "厚度墙",
    style: [
      { name: "diffHeight", label: "墙高", type: "number", step: 100, defval: 9999.0 },
      { name: "width", label: "墙厚度", type: "number", step: 1, defval: 9999.0 },
      { name: "color", label: "颜色", type: "color", defval: "rgba(51, 136, 255, 1)" },
      { name: "opacity", label: "透明度", type: "slider", min: 0, max: 1, step: 0.1, defval: 1 },
      { name: "closure", label: "是否闭合", type: "radio", defval: false },
      { name: "hasShadows", label: "是否阴影", type: "radio", defval: false }
    ]
  },
  diffuseWall: {
    name: "扩散围墙",
    style: [
      { name: "diffHeight", label: "墙高", type: "number", step: 1, defval: 100.0 },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        desc: `在贴地状态下无效`,
        defval: null, // 不能给值，会导致高度值无效
        show({ style }) {
          return !style?.clampToGround
        }
      },
      { name: "color", label: "颜色", type: "color", defval: "rgba(51, 136, 255, 1)" },
      { name: "speed", label: "速度", type: "number", min: 0, max: 999, step: 1, defval: 10 }
    ]
  },

  corridor: {
    name: "走廊",
    primitive: true,
    style: [
      {
        name: "height",
        label: "高程",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "diffHeight",
        label: "立体高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        step: 1,
        show({ style }) {
          return !style?.clampToGround
        }
      },

      { name: "width", label: "走廊宽度", type: "number", step: 1, defval: 100.0 },
      {
        name: "cornerType",
        label: "顶点样式",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "圆滑", value: 0 },
          { label: "斜接", value: 1 },
          { label: "斜切", value: 2 }
        ]
      },

      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color" },
          { label: "随机色", value: "RandomColor" },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },

          { label: "OD线", value: "ODLine" },
          { label: "闪烁线", value: "LineFlicker" },
          { label: "轨迹线", value: "LineTrail" },
          { label: "泛光线", value: "LineBloom" },
          { label: "流动颜色", value: "LineFlowColor" },

          { label: "流动blue", value: "LineFlow", defval: { image: "https://data.mars3d.cn/img/textures/line-arrow-blue.png", repeat_x: 10 } },
          {
            label: "流动dovetail",
            value: "LineFlow-2",
            defval: { image: "https://data.mars3d.cn/img/textures/line-arrow-dovetail.png", repeat_x: 10 }
          },
          { label: "流动arrow", value: "LineFlow-3", defval: { image: "https://data.mars3d.cn/img/textures/line-arrow-right.png", repeat_x: 10 } },
          { label: "流动aqua", value: "LineFlow-4", defval: { image: "https://data.mars3d.cn/img/textures/line-color-aqua.png", repeat_x: 10 } },
          { label: "流动azure", value: "LineFlow-5", defval: { image: "https://data.mars3d.cn/img/textures/line-color-azure.png", repeat_x: 10 } },
          {
            label: "流动red",
            value: "LineFlow-6",
            defval: { image: "https://data.mars3d.cn/img/textures/line-color-red.png", color: "#ff0000", repeat_x: 10 }
          },
          {
            label: "流动yellow",
            value: "LineFlow-7",
            defval: { image: "https://data.mars3d.cn/img/textures/line-color-yellow.png", color: "rgba(255, 255, 0, 1)", repeat_x: 10 }
          },
          { label: "流动colour", value: "LineFlow-8", defval: { image: "https://data.mars3d.cn/img/textures/line-colour.png", repeat_x: 10 } },
          { label: "流动gradual", value: "LineFlow-9", defval: { image: "https://data.mars3d.cn/img/textures/line-gradual.png", repeat_x: 10 } },
          { label: "流动pulse", value: "LineFlow-10", defval: { image: "https://data.mars3d.cn/img/textures/line-pulse.png" } },
          { label: "流动sprite", value: "LineFlow-11", defval: { image: "https://data.mars3d.cn/img/textures/line-sprite.png", repeat_x: 10 } },
          { label: "流动tarans", value: "LineFlow-13", defval: { image: "https://data.mars3d.cn/img/textures/line-tarans.png" } },
          { label: "流动vertebral", value: "LineFlow-14", defval: { image: "https://data.mars3d.cn/img/textures/line-vertebral.png", repeat_x: 10 } },
          {
            label: "流动vertebral-blue",
            value: "LineFlow-15",
            defval: { image: "https://data.mars3d.cn/img/textures/line-vertebral-blue.png", repeat_x: 10 }
          },
          { label: "流动fence-line", value: "LineFlow-16", defval: { image: "https://data.mars3d.cn/img/textures/fence-line.png", repeat_x: 10 } },
          {
            label: "流动transarrow",
            value: "LineFlow-17",
            defval: { image: "https://data.mars3d.cn/img/textures/line-arrow-trans.png", repeat_x: 10 }
          },
          {
            label: "流动天青",
            value: "LineFlow-18",
            defval: { image: "https://data.mars3d.cn/img/textures/line-color-yellow.png", color: "#33e8df", repeat_x: 1 }
          },
          {
            label: "天青pulse",
            value: "LineFlow-19",
            defval: { image: "https://data.mars3d.cn/img/textures/line-color-yellow.png", color: "#33e8df", width: 8 }
          }
        ],
        show({ style }) {
          return style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        },
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1,
        show({ allStyle }) {
          return allStyle?.outline
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ allStyle }) {
          return allStyle?.outline
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "zIndex",
        label: "层级顺序",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.clampToGround
        }
      }
    ]
  },
  road: {
    name: "道路",
    style: [
      { name: "opacity", label: "透明度", type: "slider", min: 0.0, max: 1.0, step: 0.1, defval: 1.0 },
      { name: "width", label: "路宽度", type: "number", min: 0.0, step: 1, defval: 1.0 },
      { name: "height", label: "路高度", type: "number", min: 0.0, step: 1, defval: 1.0 }
    ]
  },

  // 面状
  rectangle: {
    name: "矩形",
    primitive: true,
    style: [
      {
        name: "height",
        label: "高程",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "diffHeight",
        label: "立体高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        step: 1,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: true
      },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color" },
          { label: "随机色", value: "RandomColor" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" },

          { label: "文本", value: "Text" }
        ],
        show({ style }) {
          return style?.fill !== false && (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval({ style }) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.outline && !style?.diffHeight && typeof style?.outlineStyle?.width !== "number"
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineStyle?.color && !style?.outlineStyle?.materialType
        }
      },
      {
        name: "outlineStyle",
        next: "width",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        contant: "outlineWidth",
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineWidth && style?.outlineStyle?.width
        }
      },
      {
        name: "outlineStyle",
        next: "materialType",
        label: "边框材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color", contant: "outlineColor", defval: { color: "rgba(255, 255, 255, 1)" } },
          { label: "十字间隔", value: "LineCross" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "衬色线", value: "PolylineOutline" },
          { label: "光晕", value: "PolylineGlow" },
          { label: "泛光线", value: "LineBloom" },
          { label: "流动颜色", value: "LineFlowColor" },

          { label: "流动line", value: "LineFlow", defval: { image: "https://data.mars3d.cn/img/textures/fence-line.png", repeat_x: 10 } },
          { label: "流动fence", value: "LineFlow-1", defval: { image: "https://data.mars3d.cn/img/textures/fence.png", repeat_x: 10 } }
        ],
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineColor && (style?.outlineStyle?.color || style?.outlineStyle?.materialType)
        }
      },

      { name: "rotationDegree", label: "旋转角度", type: "number", step: 1, defval: 0.0 },
      {
        name: "stRotationDegree",
        label: "材质角度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.fill !== false && style?.materialType !== "Color"
        }
      },
      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "flat",
        label: "不考虑光照",
        type: "radio",
        defval: false,
        show({ graphicType }) {
          return graphicType.endsWith("P")
        }
      },
      {
        name: "zIndex",
        label: "层级顺序",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.clampToGround
        }
      }
    ]
  },
  polygon: {
    name: "面",
    primitive: true,
    extends: [
      // "video2D",
      "attackArrow",
      "attackArrowPW",
      "attackArrowYW",
      "closeVurve",
      "straightArrow",
      "doubleArrow",
      "fineArrow",
      "fineArrowYW",
      "areaMeasure",
      "gatheringPlace",
      "isosTriangle",
      "lune",
      "regular",
      "parallelogram",
      "areaSurfaceMeasure",
      "volumeMeasure",
      "volumeDepthMeasure"
    ],
    style: [
      {
        name: "diffHeight",
        label: "立体高度",
        type: "number",
        step: 1,
        defval: 0.0,
        max: 10000000,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "radius",
        label: "区域半径",
        type: "number",
        step: 1,
        show({ graphicType }) {
          return graphicType === "regular"
        }
      },

      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        step: 1,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: true
      },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color" },
          { label: "随机色", value: "RandomColor" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" },
          { label: "文本", value: "Text" },

          { label: "波纹扩散", value: "CircleWave" },
          { label: "渐变面", value: "PolyGradient" },
          { label: "双色渐变面", value: "PolyGradient2" },
          { label: "水面", value: "Water" },
          { label: "蓝光水面", value: "WaterLight" }
        ],
        show({ style, graphicType }) {
          return (
            style?.fill !== false &&
            graphicType !== "video2D" &&
            (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
          )
        }
      },
      {
        name: "url",
        label: "视频链接",
        type: "label",
        defval: "",
        show({ graphicType }) {
          return graphicType === "video2D"
        }
      },

      {
        name: "stRotationDegree",
        label: "填充方向",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0,
        show({ style }) {
          return style?.fill !== false && style?.materialType !== "Color"
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.outline && !style?.diffHeight && typeof style?.outlineStyle?.width !== "number"
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ style }) {
          return style?.outline && !style?.outlineStyle?.color && !style?.outlineStyle?.materialType
        }
      },
      {
        name: "outlineStyle",
        next: "width",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        contant: "outlineWidth",
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineWidth && style?.outlineStyle?.width
        }
      },
      {
        name: "outlineStyle",
        next: "materialType",
        label: "边框材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color", contant: "outlineColor", defval: { color: "rgba(255, 255, 255, 1)" } },
          { label: "十字间隔", value: "LineCross" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "衬色线", value: "PolylineOutline" },
          { label: "光晕", value: "PolylineGlow" },
          { label: "泛光线", value: "LineBloom" },
          { label: "流动颜色", value: "LineFlowColor" },

          { label: "流动line", value: "LineFlow", defval: { image: "https://data.mars3d.cn/img/textures/fence-line.png", repeat_x: 10 } },
          { label: "流动fence", value: "LineFlow-1", defval: { image: "https://data.mars3d.cn/img/textures/fence.png", repeat_x: 10 } }
        ],
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineColor && (style?.outlineStyle?.color || style?.outlineStyle?.materialType)
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "flat",
        label: "不考虑光照",
        type: "radio",
        defval: false,
        show({ graphicType }) {
          return graphicType.endsWith("P")
        }
      },
      // {
      //   name: "arcType",
      //   label: "球面弧线规则",
      //   type: "combobox",
      //   valType: "number",
      //   defval: 0,
      //   data: [
      //     { label: "空间直线", value: 0 },
      //     { label: "大地弧线", value: 1 },
      //     { label: "方位线", value: 2 }
      //   ]
      // },

      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "zIndex",
        label: "层级顺序",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.clampToGround
        }
      }
    ]
  },
  sector: {
    name: "扇形面",
    style: [
      {
        name: "radius",
        label: "半径",
        type: "number",
        toFixed: 2,
        step: 0.1,
        defval: 0.0
      },
      {
        name: "startAngle",
        label: "开始角度",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0
      },
      {
        name: "endAngle",
        label: "结束角度",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0
      },
      {
        name: "noCenter",
        label: "不连中心点",
        type: "radio",
        defval: false
      },

      {
        name: "diffHeight",
        label: "立体高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        step: 1,
        show({ style }) {
          return !style?.clampToGround
        }
      },

      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: true
      },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color" },
          { label: "随机色", value: "RandomColor" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" },
          { label: "文本", value: "Text" },

          { label: "渐变面", value: "PolyGradient" },
          { label: "双色渐变面", value: "PolyGradient2" },
          { label: "水面", value: "Water" },
          { label: "蓝光水面", value: "WaterLight" }
        ],
        show({ style }) {
          return style?.fill !== false && (style?.materialType ? this.data?.some((item) => item.value === style?.materialType) : true)
        }
      },

      {
        name: "stRotationDegree",
        label: "填充方向",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0,
        show({ style }) {
          return style?.fill !== false && style?.materialType !== "Color"
        }
      },

      {
        name: "outline",
        label: "是否边框",
        type: "radio",
        defval(style, graphicType) {
          return !!style?.outlineWidth || !!style?.outlineColor || !!style?.outlineStyle
        }
      },
      {
        name: "outlineWidth",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        show({ style }) {
          return style?.outline && !style?.diffHeight && typeof style?.outlineStyle?.width !== "number"
        }
      },
      {
        name: "outlineColor",
        label: "边框颜色",
        type: "color",
        defval: "rgba(255, 255, 255, 1)",
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineStyle?.color && !style?.outlineStyle?.materialType
        }
      },
      {
        name: "outlineStyle",
        next: "width",
        label: "边框宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 1.0,
        contant: "outlineWidth",
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineWidth && style?.outlineStyle?.width
        }
      },
      {
        name: "outlineStyle",
        next: "materialType",
        label: "边框材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color", contant: "outlineColor", defval: { color: "rgba(255, 255, 255, 1)" } },
          { label: "十字间隔", value: "LineCross" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "衬色线", value: "PolylineOutline" },
          { label: "光晕", value: "PolylineGlow" },
          { label: "泛光线", value: "LineBloom" },
          { label: "流动颜色", value: "LineFlowColor" },

          { label: "流动line", value: "LineFlow", defval: { image: "https://data.mars3d.cn/img/textures/fence-line.png", repeat_x: 10 } },
          { label: "流动fence", value: "LineFlow-1", defval: { image: "https://data.mars3d.cn/img/textures/fence.png", repeat_x: 10 } }
        ],
        show({ style }) {
          return style?.outline && !style?.diffHeight && !style?.outlineColor && (style?.outlineStyle?.color || style?.outlineStyle?.materialType)
        }
      },

      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },

      {
        name: "hasShadows",
        label: "是否阴影",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "flat",
        label: "不考虑光照",
        type: "radio",
        defval: false,
        show({ graphicType }) {
          return graphicType.endsWith("P")
        }
      },

      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "zIndex",
        label: "层级顺序",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.clampToGround
        }
      }
    ]
  },
  pit: {
    name: "井",
    style: [
      { name: "diffHeight", label: "井深度", type: "number", step: 1, defval: 0.0 },
      { name: "splitNum", label: "插值数", type: "number", step: 1, defval: 50 },
      { name: "image", label: "墙面贴图", type: "label" },
      { name: "imageBottom", label: "底面贴图", type: "label" }
    ]
  },
  water: {
    name: "水面",
    style: [
      { name: "opacity", label: "透明度", type: "slider", min: 0.0, max: 1.0, step: 0.1, defval: 1.0 },

      { name: "baseWaterColor", label: "基础颜色", type: "color", defval: "#123e59" },
      { name: "blendColor", label: "混合颜色", type: "color", defval: "#123e59" },
      { name: "normalMap", label: "反射图片", type: "label", defval: "https://data.mars3d.cn/img/textures/waterNormals.jpg" },
      { name: "frequency", label: "波数", type: "number", min: 1, max: 100000, step: 1, defval: 9000 },
      { name: "amplitude", label: "水波振幅", type: "number", min: 0, max: 100, step: 1, defval: 5.0 },
      { name: "animationSpeed", label: "动画速度", type: "slider", min: 0, max: 1, step: 0.01, defval: 0.03 },
      { name: "specularIntensity", label: "反射强度", type: "slider", min: 0, max: 1, step: 0.1, defval: 0.5 },

      {
        name: "stRotationDegree",
        label: "填充方向",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0,
        show({ style }) {
          return style?.fill !== false && style?.materialType !== "Color"
        }
      },

      {
        name: "offsetHeight",
        label: "偏移高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "diffHeight",
        label: "立体高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        step: 1,
        show({ style }) {
          return !style?.clampToGround
        }
      },

      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "zIndex",
        label: "层级顺序",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.clampToGround
        }
      }
    ]
  },
  video: {
    name: "视频",
    style: [
      {
        name: "diffHeight",
        label: "立体高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ graphicType }) {
          return graphicType === "wall"
        }
      },
      {
        name: "setHeight",
        label: "指定坐标高度",
        type: "number",
        step: 1,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "stRotationDegree",
        label: "填充方向",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0,
        show({ style }) {
          return style?.fill !== false && !style?.diffHeight
        }
      },
      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "clampToGround",
        label: "是否贴地",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.diffHeight || style?.diffHeight !== 0
        }
      },
      {
        name: "zIndex",
        label: "层级顺序",
        type: "number",
        min: 0,
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.clampToGround
        }
      }
    ]
  },
  reflectionWater: {
    name: "反射水面",
    style: [
      { name: "color", label: "水面颜色", type: "color", defval: "#7badd0" },
      { name: "opacity", label: "透明度", type: "slider", min: 0.0, max: 1.0, step: 0.1, defval: 0.9 },
      { name: "normalMap", label: "水扰动的法线图", type: "label", defval: "https://data.mars3d.cn/img/textures/waterNormals.jpg" },
      { name: "reflectivity", label: "反射率", type: "slider", min: 0.0, max: 1.0, step: 0.1, defval: 0.5 },
      { name: "ripple", label: "波纹大小", type: "number", min: 0.0, max: 1000.0, step: 1, defval: 50.0 },
      { name: "shiny", label: "光照强度", type: "number", min: 1.0, max: 1000.0, step: 1.0, defval: 100.0 },
      { name: "animationSpeed", label: "动画速度", type: "number", min: 0.1, max: 10.0, step: 0.1, defval: 1.0 },
      { name: "specularIntensity", label: "反射强度", type: "slider", min: 0.0, max: 0.9, step: 0.01, defval: 0.3 },
      { name: "distortion", label: "倒影扭曲程度", type: "number", min: 0.0, max: 10.0, step: 0.1, defval: 3.7 },

      { name: "farDistance", label: "远距离", type: "number", step: 1, defval: 10000 },
      { name: "farColor", label: "远距离颜色", type: "color", defval: "#91B3FF" }
    ]
  },
  dynamicRiver: {
    name: "动态河流",
    primitive: false,
    style: [
      {
        name: "image",
        label: "图片",
        type: "label",
        defval: ""
      },

      { name: "opacity", label: "透明度", type: "slider", step: 0.1, min: 0.0, max: 1.0, defval: 1.0 },
      {
        name: "width",
        label: "宽度",
        type: "number",
        min: 0,
        step: 1,
        defval: 20.0
      },
      { name: "height", label: "高度", type: "number", min: 0, step: 1, defval: 0.0 },
      { name: "speed", label: "速度", type: "number", min: 0, max: 999, step: 1, defval: 10 },
      { name: "axisY", label: "Y轴朝上", type: "radio", defval: false },
      { name: "move", label: "开启流动", type: "radio", defval: false },
      { name: "direction", label: "流动方向", type: "radio", defval: false }
    ]
  },
  videoP: {
    name: "视频面",
    primitive: true,
    style: [
      {
        name: "url",
        label: "视频地址",
        type: "dataSelect",
        desc: "视频url",
        props: {
          dataType: "video",
          bizType: "matfile"
        }
      },
      { name: "opacity", label: "透明度", type: "slider", step: 0.1, min: 0.0, max: 1.0, defval: 1.0 },
      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      }
    ]
  },
  video3D: {
    name: "视频面",
    primitive: false,
    style: [
      {
        name: "url",
        label: "视频地址",
        type: "dataSelect",
        desc: "视频url",
        props: {
          dataType: "video",
          bizType: "matfile"
        }
      },
      {
        name: "maskImage",
        label: "遮盖融合图片",
        type: "dataSelect",
        desc: "遮盖融合图片url",
        props: {
          dataType: "img",
          bizType: "matfile",
          // purpose: "none"
          purpose: "texture"
        }
      },

      {
        name: "angle",
        label: "夹角1",
        type: "slider",
        min: 0.1,
        max: 89.0,
        step: 0.01,
        defval: 1.0
      },
      {
        name: "angle2",
        label: "夹角2",
        type: "slider",
        min: 0.1,
        max: 89.0,
        step: 0.01,
        defval: 1.0
      },
      {
        name: "distance",
        label: "投射距离",
        type: "number",
        min: 0,
        step: 1
      },

      {
        name: "heading",
        label: "方向角",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0
      },
      {
        name: "pitch",
        label: "俯仰角",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0
      },
      {
        name: "roll",
        label: "翻滚角",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0
      },
      { name: "opacity", label: "透明度", type: "slider", step: 0.1, min: 0.0, max: 1.0, defval: 1.0 },
      {
        name: "flipx",
        label: "x方向旋转图片",
        type: "radio",
        defval: false
      },
      {
        name: "flipy",
        label: "y方向旋转图片",
        type: "radio",
        defval: false
      },

      { name: "hiddenAreaColor", label: "颜色", type: "color", defval: "" },
      {
        name: "showFrustum",
        label: "视锥体框线",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      }
    ]
  },
  video2D: {
    name: "2D平面投射视频",
    primitive: false,
    style: [
      {
        name: "url",
        label: "视频地址",
        type: "dataSelect",
        desc: "视频url",
        props: {
          dataType: "video",
          bizType: "matfile"
        }
      },

      {
        name: "angle",
        label: "夹角1",
        type: "slider",
        min: 0.1,
        max: 89.0,
        step: 0.01,
        defval: 1.0
      },
      {
        name: "angle2",
        label: "夹角2",
        type: "slider",
        min: 0.1,
        max: 89.0,
        step: 0.01,
        defval: 1.0
      },
      {
        name: "distance",
        label: "投射距离",
        type: "number",
        min: 0,
        step: 1
      },

      {
        name: "heading",
        label: "方向角",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0
      },
      {
        name: "pitch",
        label: "俯仰角",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0
      },
      {
        name: "roll",
        label: "翻滚角",
        type: "slider",
        min: 0.0,
        max: 360.0,
        step: 0.01,
        defval: 0.0
      },
      { name: "opacity", label: "透明度", type: "slider", step: 0.1, min: 0.0, max: 1.0, defval: 1.0 },
      {
        name: "flipx",
        label: "x方向旋转",
        type: "radio",
        defval: false
      },
      {
        name: "flipy",
        label: "y方向旋转",
        type: "radio",
        defval: false
      },
      {
        name: "showFrustum",
        label: "视锥体框线",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition",
        label: "是否按视距显示",
        type: "radio",
        defval: false
      },
      {
        name: "distanceDisplayCondition_far",
        label: "最大距离",
        type: "number",
        step: 1,
        defval: 100000.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      },
      {
        name: "distanceDisplayCondition_near",
        label: "最小距离",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return style?.distanceDisplayCondition
        }
      }
    ]
  },

  pointPlot: {
    name: "点状军标",
    style: [
      {
        name: "color",
        label: "颜色",
        type: "color",
        defval: "#ff0000",
        show({ style }) {
          return style?.hasColor ?? true
        }
      },

      { name: "width", label: "宽度", type: "number", min: 1, max: 200, step: 1, defval: 30 },
      { name: "lockRatio", label: "等比高度", type: "radio", defval: true },
      {
        name: "height",
        label: "高度",
        type: "number",
        min: 1,
        max: 200,
        step: 1,
        defval: 30,
        show({ style }) {
          return !style?.lockRatio
        }
      },
      { name: "scale", label: "大小比例", type: "number", min: 0.1, max: 1000, step: 0.1, defval: 1.0 },

      { name: "angle", label: "角度", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      {
        name: "mirror",
        label: "镜像",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "不镜像", value: 0 },
          { label: "水平镜像", value: 1 },
          { label: "垂直镜像", value: 2 },
          { label: "水平+垂直镜像", value: 3 }
        ]
      },

      {
        name: "intext",
        label: "内联文本",
        type: "textarea",
        defval: "",
        show({ style, graphicType }) {
          return style?.hasIntext ?? true
        }
      },
      {
        name: "intext_color",
        label: "内联文本颜色",
        type: "color",
        defval: "#000000",
        show({ style }) {
          return style?.intext
        }
      },

      { name: "lineWidth", label: "线宽", type: "number", min: 0.1, max: 50.0, step: 0.1, defval: 0.5 },
      {
        name: "lineType",
        label: "线型",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "实线", value: 0 },
          { label: "虚线", value: 1 }
        ]
      },
      {
        name: "dashLength",
        label: "虚线间长",
        type: "number",
        min: 1,
        step: 1,
        defval: 10,
        show({ style }) {
          return style?.lineType === 1
        }
      },

      { name: "serif", label: "是否衬线", type: "radio", defval: false },
      {
        name: "serifDirect",
        label: "衬线类型",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "无衬线", value: -1 },
          { label: "内衬", value: 0 },
          { label: "外衬", value: 1 },
          { label: "双衬", value: 2 }
        ],
        show({ style }) {
          return style?.serif
        }
      },
      {
        name: "serifColor",
        label: "衬线颜色",
        type: "color",
        defval: "rgba(255, 255, 0, 1)",
        show({ style }) {
          return style?.serif
        }
      },
      {
        name: "serifWidth",
        label: "衬线线宽",
        type: "number",
        min: 1.0,
        max: 50.0,
        step: 0.1,
        defval: 1.0,
        show({ style }) {
          return style?.serif
        }
      },

      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: false,
        show({ style, graphicType }) {
          return style?.hasFill ?? true
        }
      },
      {
        name: "fillColor",
        label: "填充颜色",
        type: "color",
        defval: "#ff0000",
        show({ style }) {
          return style?.fill
        }
      },
      { name: "lockZoom", label: "是否随图缩放", type: "radio", defval: true },

      { name: "flat", label: "是否平放", type: "radio", defval: false },
      { name: "rotate", label: "竖直旋转角度", type: "slider", min: 0.0, max: 360.0, step: 0.01, defval: 0.0 },
      {
        name: "diffHeight",
        label: "拉伸厚度",
        type: "number",
        min: 0,
        max: 100000,
        step: 1,
        defval: 0,
        show({ style }) {
          return style?.rotate > 0
        }
      },

      { name: "text", label: "注记文本", type: "textarea", defval: "" },
      { name: "text_color", label: "注记颜色", type: "color", defval: "#000000" },
      { name: "text_font_size", label: "注记大小", type: "number", min: 1, max: 200, step: 1, defval: 30 },
      {
        name: "text_direction",
        label: "注记位置",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "正下方", value: 0 },
          { label: "正右方", value: 1 },
          { label: "正上方", value: 2 },
          { label: "正左方", value: 3 },
          { label: "右上", value: 4 },
          { label: "右下", value: 5 },
          { label: "左上", value: 6 },
          { label: "左下", value: 7 },
          { label: "居中", value: 8 }
        ]
      }
    ]
  },

  polyPlot: {
    name: "线面军标",
    style: [
      {
        name: "color",
        label: "颜色",
        type: "color",
        defval: "#ff0000",
        show({ style }) {
          return style?.hasColor ?? true
        }
      },
      { name: "lineWidth", label: "边线线宽", type: "number", min: 0.1, max: 50.0, step: 0.1, defval: 0.5 },
      {
        name: "lineType",
        label: "边线线型",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "实线", value: 0 },
          { label: "虚线", value: 1 }
        ]
      },
      {
        name: "dashLength",
        label: "虚线间长",
        type: "number",
        min: 1,
        step: 1,
        defval: 10,
        show({ style }) {
          return style?.lineType === 1
        }
      },

      { name: "serif", label: "是否衬线", type: "radio", defval: false },
      {
        name: "serifDirect",
        label: "衬线类型",
        type: "combobox",
        valType: "number",
        defval: 0,
        data: [
          { label: "无衬线", value: -1 },
          { label: "内衬", value: 0 },
          { label: "外衬", value: 1 },
          { label: "双衬", value: 2 }
        ],
        show({ style }) {
          return style?.serif
        }
      },
      {
        name: "serifColor",
        label: "衬线颜色",
        type: "color",
        defval: "rgba(255, 255, 0, 1)",
        show({ style }) {
          return style?.serif
        }
      },
      {
        name: "serifWidth",
        label: "衬线线宽",
        type: "number",
        min: 0.1,
        max: 50.0,
        step: 0.1,
        defval: 1.0,
        show({ style }) {
          return style?.serif
        }
      },

      {
        name: "fill",
        label: "是否填充",
        type: "radio",
        defval: false,
        show({ style }) {
          return style?.hasFill ?? true
        }
      },
      {
        name: "materialType",
        label: "填充材质",
        type: "combobox",
        defval: "Color",
        data: [
          { label: "纯色", value: "Color" },
          { label: "图片", value: "Image", defval: { image: "https://data.mars3d.cn/img/map/gugong.jpg" } },
          { label: "网格", value: "Grid" },
          { label: "条纹", value: "Stripe" },
          { label: "棋盘", value: "Checkerboard" },
          { label: "文本", value: "Text" },

          { label: "渐变面", value: "PolyGradient" },
          { label: "双色渐变面", value: "PolyGradient2" },
          { label: "水面", value: "Water" },
          { label: "蓝光水面", value: "WaterLight" }
        ],
        show({ style }) {
          return style?.hasFill !== false && style?.fill
        }
      },

      { name: "clampToGround", label: "是否贴地", type: "radio", defval: false },
      {
        name: "height",
        label: "高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "diffHeight",
        label: "立体高度",
        type: "number",
        step: 1,
        defval: 0.0,
        show({ style }) {
          return !style?.clampToGround
        }
      },

      {
        name: "wall",
        label: "是否显示墙",
        type: "radio",
        defval: false,
        show({ style }) {
          return !style?.clampToGround
        }
      },
      {
        name: "wallOpacity",
        label: "墙透明度",
        type: "slider",
        min: 0.0,
        max: 1.0,
        step: 0.1,
        defval: 0.4,
        show({ style }) {
          return !style?.clampToGround && style?.wall
        }
      },

      {
        name: "subCode",
        label: "子标号编码",
        type: "textarea",
        defval: "",
        show({ style }) {
          return style?.hasSubCode
        }
      },
      {
        name: "subColor",
        label: "子标号颜色",
        type: "color",
        defval: "#ff0000",
        show({ style }) {
          return style?.hasSubCode
        }
      }
    ]
  }
}

// 部分矢量对应与其他基本类型完全相同配置时，复制配置
for (const key in styleConfig) {
  styleConfig[key].type = key // 标识类型
  if (styleConfig[key].primitive) {
    styleConfig[key + "P"] = styleConfig[key]
  }
  if (styleConfig[key].extends) {
    styleConfig[key].extends.forEach((element) => {
      styleConfig[element] = styleConfig[key]
    })
  }
}

// window.styleConfig = styleConfig
export default styleConfig
