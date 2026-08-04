<script setup lang="ts">
import { ref } from 'vue'

defineProps<{
  previewUrl: string
  fileName?: string
  progress: number
  uploading: boolean
  result: string
  error: string
}>()

const emit = defineEmits<{
  upload: [file: File]
  cancel: []
  clear: []
}>()

const input = ref<HTMLInputElement>()
const dragging = ref(false)

function choose() {
  input.value?.click()
}

function accept(files: FileList | null) {
  const file = files?.item(0)
  if (file) emit('upload', file)
  if (input.value) input.value.value = ''
}

function drop(event: DragEvent) {
  dragging.value = false
  accept(event.dataTransfer?.files ?? null)
}
</script>

<template>
  <section class="media-upload-panel surface" aria-labelledby="media-upload-title">
    <div
      class="media-drop-zone"
      :class="{ dragging }"
      @dragenter.prevent="dragging = true"
      @dragover.prevent
      @dragleave.prevent="dragging = false"
      @drop.prevent="drop"
    >
      <img v-if="previewUrl" :src="previewUrl" alt="등록할 이미지 미리보기">
      <div v-else>
        <strong id="media-upload-title">로컬 이미지 등록</strong>
        <p>JPEG, PNG, WebP · 최대 20 MiB · 최대 8192px · 4천만 픽셀</p>
      </div>
      <input
        ref="input"
        class="visually-hidden"
        type="file"
        accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
        @change="accept(($event.target as HTMLInputElement).files)"
      >
      <button class="button" type="button" :disabled="uploading" @click="choose">파일 선택</button>
    </div>

    <div v-if="fileName || uploading" class="upload-status" aria-live="polite">
      <span>{{ fileName }}</span>
      <progress v-if="uploading" :value="progress" max="100">{{ progress }}%</progress>
      <strong v-if="uploading">{{ progress }}%</strong>
      <button v-if="uploading" class="button danger-quiet" type="button" @click="emit('cancel')">취소</button>
      <button v-else-if="previewUrl" class="button" type="button" @click="emit('clear')">미리보기 닫기</button>
    </div>
    <p v-if="result" class="media-success" role="status">{{ result }}</p>
    <p v-if="error" class="media-error" role="alert">{{ error }}</p>
  </section>
</template>
