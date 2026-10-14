const baseConfig = [
  {
    name: "type",
    type: "label",
    label: "矢量类型",
    defval: ""
  },
  {
    name: "name",
    type: "input",
    label: "名称",
    defval: "用于区别矢量对象，请填写有意义的标题名称，如矢量对象树、时序面板"
  },
  {
    name: "id",
    type: "label",
    label: "ID唯一标识",
    defval: ""
  },
  // {
  //   name: "pid",
  //   type: "label",
  //   label: "父级ID",
  //   defval: ""
  // },
  {
    name: "show",
    type: "radio",
    label: "显示",
    defval: true
  }
]

// window.baseConfig = baseConfig
export default baseConfig
