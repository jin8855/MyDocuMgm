<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { MediaItem } from '../../shared/types'
import { cloneValue } from '../../shared/utils/clone'

const props = defineProps<{ item?: MediaItem }>()
const emit = defineEmits<{
  save: [value: MediaItem]
  delete: [value: MediaItem]
  restore: [value: MediaItem]
  move: [value: MediaItem, direction: -1 | 1]
}>()
const form = reactive<MediaItem | Record<string, never>>({})
const confirmDelete = ref(false)
const previewFailed = ref(false)
watch(() => props.item, (value) => {
  Object.keys(form).forEach((key) => delete (form as Record<string, unknown>)[key])
  if (value) Object.assign(form, cloneValue(value))
  confirmDelete.value = false
  previewFailed.value = false
}, { immediate: true })
const originalUrl = computed(() => props.item?.thumbnailUrl.replace(/\/thumbnail(?:\?.*)?$/, '/file'))
function save() { emit('save', cloneValue(form as MediaItem)) }
</script>

<template>
  <aside class="media-editor">
    <div v-if="!item" class="empty-compact">썸네일을 선택하면 이미지 설정을 편집할 수 있습니다.</div>
    <template v-else>
      <div class="editor-preview">
        <img
          v-if="!item.isDeleted && item.thumbnailUrl && !previewFailed"
          :src="item.thumbnailUrl"
          :alt="item.originalFileName"
          @error="previewFailed = true"
        >
        <span v-else>{{ item.isDeleted ? '삭제된 이미지' : '미리보기 없음' }}</span>
      </div>
      <h3>{{ item.isDeleted ? '삭제된 이미지' : '선택 이미지 편집' }}</h3>
      <p class="media-file-name">{{ item.originalFileName }}</p>
      <p class="media-dimensions">{{ item.width }}×{{ item.height }} · {{ Math.ceil(item.sizeBytes / 1024) }}KB</p>

      <template v-if="item.isDeleted">
        <p class="media-warning">원본 무결성 검증을 통과한 경우에만 복원됩니다.</p>
        <button class="button primary full" type="button" @click="emit('restore', item)">이미지 복원</button>
      </template>
      <template v-else>
        <div class="media-order-actions" aria-label="이미지 표시 순서">
          <button class="button" type="button" @click="emit('move', item, -1)">앞으로</button>
          <span>순서 {{ item.sortOrder }}</span>
          <button class="button" type="button" @click="emit('move', item, 1)">뒤로</button>
        </div>
        <label>설명<textarea v-model="(form as MediaItem).description" rows="4"></textarea></label>
        <label class="check-row"><input v-model="(form as MediaItem).isPublicAllowed" type="checkbox"> 공개 파생 이미지 사용 허용</label>
        <a v-if="originalUrl" class="button full" :href="originalUrl" target="_blank" rel="noopener">원본 열기</a>
        <button class="button primary full" type="button" @click="save">이미지 설정 저장</button>
        <button v-if="!confirmDelete" class="button danger-quiet full" type="button" @click="confirmDelete = true">삭제</button>
        <div v-else class="media-delete-confirm" role="alert">
          <p>원본은 유지하고 30일 복구 대기 상태로 전환합니다.</p>
          <button class="button" type="button" @click="confirmDelete = false">취소</button>
          <button class="button danger-quiet" type="button" @click="emit('delete', item)">삭제 확인</button>
        </div>
      </template>
    </template>
  </aside>
</template>
