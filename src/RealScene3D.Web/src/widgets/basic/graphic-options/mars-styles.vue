<template>
  <a-collapse
    v-model:activeKey="styleCollapse"
    expandIconPosition="end"
  >
    <a-collapse-panel
      v-if="viewStyles"
      key="1"
      :header="getCollapseName()"
    >
      <a-row :gutter="[0, 10]">
        <AttrComp
          v-if="props.parentType"
          label="是否配置"
        >
          <template #right>
            <mars-switch
              v-model:checked="styleValue.show"
              @change="changeSwitch"
            />
          </template>
        </AttrComp>
        <template v-else>
          <AttrComp label="标号类型">
            <template #right>
              {{ props.graphicType }}
            </template>
          </AttrComp>
          <AttrComp
            v-if="props.graphicType !== styleType"
            label="样式类型"
          >
            <template #right>
              {{ styleType || "未配置" }}
            </template>
          </AttrComp>
        </template>

        <template v-if="stylePanIsHidden()">
          <template
            v-for="(item, i) in viewStyles"
            :key="i"
          >
            <!-- 不是hidden类型 -->
            <template v-if="item.type !== 'hidden'">
              <!-- 含有下一级参数 -->
              <template v-if="item.next">
                <AttrComp
                  v-if="item.show({ allStyle: styleValue, style: props.style, graphicType: props.graphicType, parentType: props.parentType })"
                  :label="item.label"
                  :type="item.type"
                  size="small"
                  v-model:value="styleValue[item.name][item.next]"
                  :min="item.min || item.min === 0 ? item.min : -Infinity"
                  :max="item.max || item.max === 0 ? item.max : Infinity"
                  :step="item.step || 0.1"
                  :options="item.data || []"
                  :tofixed="item.toFixed"
                  @change="unionChange(item, item.data)"
                >
                </AttrComp>
              </template>
              <template v-else>
                <AttrComp
                  v-if="item.show({ allStyle: styleValue, style: props.style, graphicType: props.graphicType, parentType: props.parentType })"
                  v-model:value="styleValue[item.name]"
                  :label="item.label"
                  :type="item.type"
                  size="small"
                  :min="item.min || item.min === 0 ? item.min : -Infinity"
                  :max="item.max || item.max === 0 ? item.max : Infinity"
                  :step="item.step || 0.1"
                  :options="item.data || []"
                  @change="unionChange(item, item.data)"
                  :tofixed="item.toFixed"
                >
                </AttrComp>
              </template>

              <!-- 材质相关属性 -->
              <template
                v-if="item.name === 'materialType'
                  && viewMaterials
                  && styleValue.materialOptions
                  && item.show({ allStyle: styleValue, style: props.style, graphicType: props.graphicType, parentType: props.parentType })"
              >
                <template
                  v-for="(material, mi) in viewMaterials"
                  :key="mi"
                >
                  <AttrComp
                    v-if="material.show({ allStyle: styleValue, style: props.style, graphicType: props.graphicType, parentType: props.parentType })"
                    :label="material.label"
                    :type="material.type"
                    size="small"
                    v-model:value="styleValue.materialOptions[material.name]"
                    :min="material.min || material.min === 0 ? material.min : -Infinity"
                    :max="material.max || material.max === 0 ? material.max : Infinity"
                    :step="material.step || 0.1"
                    :options="material.data || []"
                    @change="materialChange(material)"
                  >
                  </AttrComp>
                </template>

              </template>

              <!-- 子级参数的材质相关属性 -->
              <template v-if="item.next === 'materialType'
                && nextViewMaterials
                && styleValue[item.name].materialOptions
                && item.show({
                  allStyle: styleValue, style: props.style, graphicType: props.graphicType, parentType: props.parentType
                })">
                <template
                  v-for="(nextmaterial, nextmi) in nextViewMaterials"
                  :key="nextmi"
                >
                  <AttrComp
                    v-if="nextmaterial.show({
                      allStyle: styleValue, style: props.style[item.name], graphicType: props.graphicType, parentType: props.parentType
                    })"
                    :label="nextmaterial.label"
                    :type="nextmaterial.type"
                    size="small"
                    v-model:value="styleValue[item.name].materialOptions[nextmaterial.name]"
                    :min="nextmaterial.min || nextmaterial.min === 0 ? nextmaterial.min : -Infinity"
                    :max="nextmaterial.max || nextmaterial.max === 0 ? nextmaterial.max : Infinity"
                    :step="nextmaterial.step || 0.1"
                    :options="nextmaterial.data || []"
                    @change="nextMaterialChange(nextmaterial, item)"
                  >
                  </AttrComp>
                </template>
              </template>
            </template>
          </template>
        </template>
      </a-row>
    </a-collapse-panel>

    <a-collapse-panel
      v-if="hasLabel"
      key="2"
      header="注记信息"
    >
      <a-row :gutter="[0, 10]">
        <template
          v-for="(item, i) in viewLabels"
          :key="i"
        >
          <AttrComp
            v-if="item.show({ style: props.style?.label, allStyle: styleValue.label, graphicType: 'label', })"
            :label="item.label"
            :type="item.type"
            size="small"
            v-model:value="styleValue.label[item.name]"
            :min="item.min || item.min === 0 ? item.min : -Infinity"
            :max="item.max || item.max === 0 ? item.max : Infinity"
            :step="item.step || 0.1"
            :options="item.data || []"
            @change="labelChange(item)"
          >
          </AttrComp>
        </template>
      </a-row>
    </a-collapse-panel>
  </a-collapse>
</template>

<script lang="ts" setup>
import { nextTick, ref, watch, toRaw } from "vue"
import { $message } from "@mars/components/mars-ui/index"
import AttrComp from "./component/attr-comp.vue"
import styleConfigAll from "./config/style.js"
import materialConfig from "./config/material.js"

const props = withDefaults(defineProps<{
  graphicType: string
  customType?: string
  style: any
  parentType?: string
  isParent?: boolean
}>(), {
  parentType: null,
  isParent: false
})

const emit = defineEmits(["styleChange", "update:style"])

// 样式和配置
const styleValue = ref<any>({})
const viewStyles = ref<any[]>([])

// 材质和配置
const viewMaterials = ref<any[]>([])
const nextViewMaterials = ref<any[]>([])

// label配置
const viewLabels = ref<any[]>([])

// 类型
const styleType = ref("")

// 缓存 diffHeight 值：用于在 clampToGround 切换时保留立体高度
let cachedDiffHeight: number | null = null


const hasLabel = ref(false) // 是否存在注记
const hasLabelGraphicType = ["billboard", "billboardP"]

watch(
  () => props.graphicType,
  () => {
    if (!props.graphicType) {
      return
    }
    if (props.parentType) {
      setDefault({ show: !!props.style, ...props.style }) // 处理初始化默认值（不做任何修改之前的状态）
    } else if (props.style) {
      setDefault({ ...(props.style) }) // 处理初始化默认值（不做任何修改之前的状态）
    }

    hasLabel.value = (hasLabelGraphicType.includes(props.graphicType) || props.style?.label) && !props.isParent

    if (hasLabel.value) {
      setLabelDefault({ ...props.style?.label })
    }
  },
  {
    immediate: true
  }
)

const styleCollapse = ref(["1", "2"])
function getCollapseName() {
  if (props.parentType) {
    const type = props.graphicType
    return `${styleConfigAll[type]?.name}样式`

  }
  return "样式信息"
}
function stylePanIsHidden() {
  if (props.parentType) {
    return styleValue.value.show ?? !!styleValue.value
  }
  return viewStyles.value
}

function getViewShow(config) {
  if (typeof config.show === "function") {
    return config.show
  } else {
    return () => config?.show ?? false
  }
}

function getViewDefval(config, styleOptions) {
  if (typeof config.defval === "function") {
    return config.defval(props.style, props.graphicType)
  } else {
    return config.defval
  }
}
// ************************************************ 初始化
// 设置初始化的默认值
function setDefault(styleOptions) {
  styleValue.value = { ...styleOptions }

  const styleConfig = styleConfigAll[props.customType] ?? styleConfigAll[props.graphicType]
  if (!styleConfig) {
    return
  }

  styleType.value = styleConfig.type
  viewStyles.value = [...styleConfig.style]

  let materialTypeOption

  viewStyles.value?.forEach((item: any) => {
    item.show = getViewShow(item) // 统一将 show 处理为函数

    if (item.next) {
      if (!styleValue.value[item.name]) {
        styleValue.value[item.name] = {}
      }
      let val = null
      if (item.contant && styleOptions[item.contant]) {
        val = styleOptions[item.contant]
      }
      styleValue.value[item.name][item.next] = styleValue.value[item.name][item.next] ?? val ?? getViewDefval(item, styleValue.value) // 数据中没有的地方使用默认值

      if (item.next === "materialType") {
        const nextViewMaterialsConfig = setMaterial(styleValue.value[item.name], item)
        nextViewMaterials.value = nextViewMaterialsConfig
      }
    } else {
      // diffHeight 字段特殊处理：如果在贴地模式下值丢失（为 undefined/0），从缓存中恢复
      if (item.name === "diffHeight" && (styleValue.value[item.name] == null || styleValue.value[item.name] === 0) && cachedDiffHeight != null && cachedDiffHeight > 0) {
        styleValue.value[item.name] = cachedDiffHeight
      } else {
        styleValue.value[item.name] = styleValue.value[item.name] ?? getViewDefval(item, styleValue.value) // 数据中没有的地方使用默认值
      }
      if (item.name === "materialType") {
        materialTypeOption = item
      }
    }
  })

  const viewMaterialsConfig = setMaterial(styleValue.value, materialTypeOption)
  viewMaterials.value = viewMaterialsConfig
}

function setLabelDefault(styleOptions) {
  styleValue.value.label = { ...styleOptions }

  viewLabels.value = [...styleConfigAll?.label?.style]

  if (viewLabels.value) {
    viewLabels.value.forEach((item: any) => {
      item.show = getViewShow(item) // 统一将 show 处理为函数
      styleValue.value.label[item.name] = styleValue.value.label[item.name] ?? getViewDefval(item, styleValue.value.label) // 数据中没有的地方使用默认值
    })
  }
}

function setMaterial(dataRef: any, materialTypeOption: any) {
  /**
   * 设置材质,没有材质但有color值时，默认是Color，都没有值时，默认为null
   * 材质同一类但有多个不同参数设置时，根据-*区分的(如LineFlow-2)，使用workMaterialType记录下，便于业务区分
   */
  function getMaterialType() {
    if (dataRef._workMaterialType) {
      dataRef.materialType = dataRef._workMaterialType
      return dataRef.materialType
    } else if (dataRef.materialType) {
      return dataRef.materialType
    } else {
      return "Color"
    }
  }
  const materialType = getMaterialType()


  if (materialType && materialTypeOption) {
    if (!dataRef.materialOptions) {
      dataRef.materialOptions = {}
    }

    const realyMaterialType = materialType.split("-")[0]
    const materialResult = materialTypeOption.data.find(item => item.value === realyMaterialType)
    const defval = materialResult?.defval ?? {}
    const viewMaterialsConfig = [...(materialConfig[realyMaterialType] ?? [])]

    viewMaterialsConfig.forEach((p) => {
      const val = dataRef.materialOptions[p.name]
      // 初始化进入默认值的取值顺序 1. 本身属性 2. style中的属性 3. style.js 材质默认值 4. material.js 的默认值
      dataRef.materialOptions[p.name] =
        val ?? dataRef[p.name] ?? defval[p.name] ?? getViewDefval(p, dataRef.materialOptions)

      // 纯色材质特殊处理下
      if (materialType === "Color") {
        dataRef[p.name] = dataRef.materialOptions[p.name]
      }

      p.show = getViewShow(p)
    })

    return viewMaterialsConfig
  }
  return null
}

// ************************************************ 修改、更新
// 多个类型作为参数
const changeSwitch = (value) => {
  updateValue({ show: value })
  changeValue({ show: value }, props.graphicType)
}

// 非材质属性改变
function unionChange(item: any, selectOptions?: any[]) {
  const name = item.name

  // clampToGround 切换时缓存/恢复 diffHeight
  if (name === "clampToGround") {
    if (styleValue.value.clampToGround === true) {
      // 勾选贴地前，缓存当前的 diffHeight 值
      if (styleValue.value.diffHeight != null && styleValue.value.diffHeight !== 0) {
        cachedDiffHeight = styleValue.value.diffHeight
      }
    } else {
      // 取消贴地时，恢复之前缓存的 diffHeight
      if (cachedDiffHeight != null && cachedDiffHeight > 0) {
        styleValue.value.diffHeight = cachedDiffHeight
      }
    }
  }

  if (name === "fill" || name === "outline") {
    if (styleValue.value.fill === false && styleValue.value.outline === false) {
      $message("填充和边框不能同时为否")
      nextTick(() => {
        styleValue.value[name] = true
      })
      return
    }
  }

  // 材质类型 materialType 改变时的特殊处理
  if (name === "materialType") {
    // 设置材质属性的默认值
    let defval = {}
    if (selectOptions) {
      const mOp = selectOptions.find((item) => item.value === styleValue.value.materialType) // 当前选项的材质属性配置
      if (mOp) {
        defval = mOp.defval ?? {}
      }
    }
    const materialType = styleValue.value.materialType.split("-")[0]
    const materialOptions = {}

    const viewMaterialsConfig = [...(materialConfig[materialType] ?? [])]
    viewMaterialsConfig.forEach((p) => {
      // 更新时的默认值的取值顺序 1. style.js 材质默认值 2. material.js 的默认值
      materialOptions[p.name] = defval[p.name] ?? getViewDefval(p, materialOptions)

      p.show = getViewShow(p)
    })

    viewMaterials.value = viewMaterialsConfig
    styleValue.value.materialOptions = materialOptions
  }
  if (item.next && item.next === "materialType") {
    // 设置材质属性的默认值
    let defval = {}
    if (selectOptions) {
      const mOp = selectOptions.find((item) => item.value === styleValue.value[name].materialType) // 当前选项的材质属性配置
      if (mOp) {
        defval = mOp.defval ?? {}
      }
      const materialType = styleValue.value[name].materialType.split("-")[0]
      const materialOptions = {}

      const nextViewMaterialsConfig = [...(materialConfig[materialType] ?? [])]
      nextViewMaterialsConfig.forEach((p) => {
        // 更新时的默认值的取值顺序 1. style.js 材质默认值 2. material.js 的默认值
        materialOptions[p.name] = defval[p.name] ?? getViewDefval(p, materialOptions)

        p.show = getViewShow(p)
      })

      nextViewMaterials.value = nextViewMaterialsConfig
      styleValue.value[name].materialOptions = materialOptions
    }
  }

  if (item.next) {
    updateNextStyle(item.name, item.next)
    return
  }
  const val = styleValue.value[item.name]

  const data: Record<string, any> = {
    [item.name]: item.name === "materialType" ? val.split("-")[0] : val
  }

  // 取消贴地时，同时恢复缓存的 diffHeight 到 graphic 对象
  if (name === "clampToGround" && val === false && cachedDiffHeight != null && cachedDiffHeight > 0) {
    data.diffHeight = cachedDiffHeight
  }

  // 材质类型 materialType 改变时的特殊处理
  if (item.name === "materialType") {
    data.materialOptions = { ...styleValue.value.materialOptions }
    // 材质同一类但有多个不同参数设置时，根据-*区分的(如LineFlow-2)，使用workMaterialType记录下，便于业务区分
    data._workMaterialType = val
  }

  // console.log("修改了普通参数", data)
  updateValue({ ...data })
  changeValue({ ...data })
}



function materialChange(item) {
  // console.log("更换了材质类型", item)
  const styleValues = { ...styleValue.value }


  // 对材质中 含有image2字段 进行特殊处理
  if (styleValues?.image2) {
    styleValues.materialOptions.image2 = styleValues.image2
  }


  const changeVal = { materialOptions: { ...styleValues.materialOptions } }

  // TODO: 不要下面这样写，示例项目不用为了兼容1个问题，造成更多bug问题,项目中有什么属性就配置什么属性（color或materialOptions.color）。
  // let changeVal = null
  // 纯色样式 - 直接改style内参数，否则面板会以style内color值为主，mars3d的效果以materialOptions内color值为主，导致不一致
  // if (!styleValues.materialType || styleValues.materialType === "Color") {
  //   changeVal = { color: styleValues.materialOptions.color, materialOptions: { ...styleValues.materialOptions } }
  // } else {
  // changeVal = { materialOptions: { ...styleValues.materialOptions } }
  // }

  updateValue({ ...changeVal })
  changeValue(changeVal)
}

// ************************************************ 子级参数
function updateNextStyle(parent: string, param: string) {
  const val = styleValue.value[parent][param]

  const data: Record<string, any> = {
    [param]: param === "materialType" ? val.split("-")[0] : toRaw(val)
  }

  // 材质类型 materialType 改变时的特殊处理
  if (param === "materialType") {
    data.materialOptions = { ...styleValue.value[parent].materialOptions }
    // 材质同一类但有多个不同参数设置时，根据-*区分的(如LineFlow-2)，使用workMaterialType记录下，便于业务区分
    data._workMaterialType = val
  }

  // console.log("修改 updateNextStyle 子级普通参数", data)

  updateValue({ [parent]: { ...props.style[parent], ...data } })
  changeValue({ [parent]: data })
}

function nextMaterialChange(_material: any, item: any) {
  const styleValues = { ...styleValue.value }

  let changeVal = null
  // 纯色样式 - 直接改style内参数，否则面板会以style内color值为主，mars3d的效果以materialOptions内color值为主，导致不一致
  if (!styleValues[item.name].materialType || styleValues[item.name].materialType === "Color") {
    changeVal = {
      [item.name]: {
        ...styleValues[item.name],
        materialOptions: { ...styleValues[item.name].materialOptions },
        color: styleValues[item.name].materialOptions.color
      }
    }
  } else {
    changeVal = { [item.name]: { ...styleValues[item.name], materialOptions: { ...styleValues[item.name].materialOptions } } }
  }

  // console.log("nextMaterialChange 修改子级材质 changeVal", item, changeVal)
  updateValue({ ...changeVal })
  changeValue(changeVal)
}
// ************************************************ label相关处理
function labelChange(item: any) {
  const name = item.name
  const label: Record<string, any> = {
    [name]: styleValue.value.label[name]
  }


  updateValue({ label: { ...props.style?.label, ...label } })
  changeValue({ label })
}


function updateValue(obj) {
  // console.log("更新的操作", { ...props.style, ...obj })
  emit("update:style", { ...props.style, ...obj })
}
function changeValue(changeVal, key?) {
  let obj = changeVal
  if (props.parentType) {
    obj = { [props.graphicType]: changeVal }
  }

  // console.log("修改的操作", { ...obj }, key)
  emit("styleChange", obj, key)
}
</script>
