// import { moduleData } from "@mars/widgets/fx/toolBox/moduleData"
// TODO(P4-tools): 等 widgets/analysis/toolbox/moduleData.ts 迁移后,改为
//   import { moduleData } from "@mars/widgets/analysis/toolbox/moduleData"

/**
 * 根据 analysisType 从 moduleData 中查找对应的 name。
 * P1-1 阶段 moduleData 尚未迁移,此处暂直接返回 analysisType。
 * P4 完成后恢复完整查找逻辑。
 */
export const findNameByAnalysisType = (analysisType: string): string => {
  // TODO(P4-tools): restore lookup once moduleData is migrated
  // for (const module of moduleData) {
  //   if (module.children) {
  //     const found = module.children.find((child: any) => child.analysisType === analysisType)
  //     if (found) {
  //       return found.name
  //     }
  //   }
  // }
  return analysisType
}
