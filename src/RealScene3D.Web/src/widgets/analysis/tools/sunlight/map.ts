import { shadows } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"
export function stopPlay() {
  if (shadows && shadows.isStart) {
    shadows.pause()
  }
}


/**
 * 开始播放
 *
 * @export
 * @param {*} date 年月日
 * @param {number} hours 小时
 * @param {number} minutes 分钟
 */
export function startPlay(date, hours, minutes) {
  const currentTime = setShadows(date, hours, minutes)
  const startDate = new Date(date + " 07:00:00")
  const endDate = new Date(date + " 18:00:00")

  shadows.multiplier = 1600
  shadows.start(startDate, endDate, currentTime)
}

/**
 * 修改shadows 当前时间
 *
 * @export
 * @param {*} date 年月日
 * @param {number} hours 小时
 * @param {number} minutes 分钟
 */
export function setShadows(date, hours, minutes) {
  const dateTime = new Date(`${date} ${hours}:${minutes}:00`)
  shadows.time = dateTime

  return dateTime
}

