<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '../api'
import StatePanel from '../components/StatePanel.vue'
import type { MediaItem } from '../types'

const props = defineProps<{ id: string }>()
const items = ref<MediaItem[]>([])
const loading = ref(true)
const uploading = ref(false)
const error = ref('')
async function load() {
  try { items.value = await api.media(props.id) }
  catch (reason) { error.value = reason instanceof Error ? reason.message : '이미지를 불러오지 못했습니다.' }
  finally { loading.value = false }
}
async function upload(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  uploading.value = true
  error.value = ''
  try { items.value.push(await api.upload(props.id, file)) }
  catch (reason) { error.value = reason instanceof Error ? reason.message : '이미지를 등록하지 못했습니다.' }
  finally { uploading.value = false }
}
onMounted(load)
</script>

<template>
  <div class="page narrow">
    <div class="page-heading"><div><p class="eyebrow">자료 정리</p><h1>이미지 관리</h1><p>JPG, PNG, WebP · 파일당 최대 20MB · 기본 비공개</p></div><RouterLink class="button" :to="`/contents/${id}`">기록으로</RouterLink></div>
    <label class="drop-zone"><input type="file" accept="image/jpeg,image/png,image/webp" :disabled="uploading" @change="upload"><span class="drop-icon">＋</span><strong>{{ uploading ? '안전하게 저장하는 중…' : '이미지 선택' }}</strong><small>원본 이름은 메타데이터로만 보관하고 안전한 이름으로 저장합니다.</small></label>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <StatePanel v-if="loading" kind="loading" title="이미지 목록을 확인하는 중입니다" />
    <StatePanel v-else-if="!items.length" kind="empty" title="등록된 이미지가 없습니다" detail="이 기록을 떠올리기 쉬운 사진을 추가해 보세요." />
    <div v-else class="media-list"><article v-for="item in items" :key="item.id"><div class="media-placeholder">IMG</div><div><strong>{{ item.originalFileName }}</strong><p>{{ item.width }} × {{ item.height }} · {{ Math.ceil(item.sizeBytes / 1024) }}KB</p></div><span class="status">{{ item.storageStatus }}</span></article></div>
  </div>
</template>
