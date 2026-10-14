import * as mars3d from "mars3d"

export class RecorderManager {
  private map: mars3d.Map
  private mediaRecorder: MediaRecorder | null = null
  private recordedChunks: Blob[] = []
  private isRecording: boolean = false

  constructor(map: mars3d.Map) {
    this.map = map
  }

  startRecording() {
    if (this.isRecording) {
      return
    }

    const canvas = this.map.viewer.scene.canvas
    const stream = (canvas as any).captureStream(30)

    this.recordedChunks = []
    this.mediaRecorder = new MediaRecorder(stream, {
      mimeType: "video/webm;codecs=vp9"
    })

    this.mediaRecorder.ondataavailable = (event) => {
      if (event.data.size > 0) {
        this.recordedChunks.push(event.data)
      }
    }

    this.mediaRecorder.onstop = () => {
      const blob = new Blob(this.recordedChunks, {
        type: "video/webm"
      })
      const url = URL.createObjectURL(blob)
      const a = document.createElement("a")
      a.style.display = "none"
      a.href = url
      a.download = `roam_video_${Date.now()}.webm`
      document.body.appendChild(a)
      a.click()
      setTimeout(() => {
        document.body.removeChild(a)
        window.URL.revokeObjectURL(url)
      }, 100)
    }

    this.mediaRecorder.start()
    this.isRecording = true
    console.log("Recording started using MediaRecorder API")
  }

  async stopRecording() {
    if (!this.isRecording || !this.mediaRecorder) {
      return
    }

    this.mediaRecorder.stop()
    this.isRecording = false
    console.log("Recording stopped")
  }

  getRecordingState() {
    return this.isRecording
  }
}
