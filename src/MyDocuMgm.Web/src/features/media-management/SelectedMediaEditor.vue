<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { MediaItem } from '../../shared/types'
import { cloneValue } from '../../shared/utils/clone'

const props = defineProps<{ item?: MediaItem }>()
const emit = defineEmits<{ save: [value: MediaItem] }>()
const form = reactive<MediaItem | Record<string, never>>({})
watch(() => props.item, (value) => {
  Object.keys(form).forEach((key) => delete (form as Record<string, unknown>)[key])
  if (value) Object.assign(form, cloneValue(value))
}, { immediate: true })
function save() { emit('save', cloneValue(form as MediaItem)) }
</script>

<template>
  <aside class="media-editor">
    <div v-if="!item" class="empty-compact">썸네일 한 장을 선택하면 여기에서 편집할 수 있습니다.</div>
    <template v-else>
      <div class="editor-preview" :style="{ '--tone': String((item.sortOrder * 37) % 240) }">{{ item.sortOrder }}</div>
      <h3>선택 이미지 편집</h3>
      <p>{{ item.originalFileName }}</p>
      <label>단계 순서<input type="number" min="1" :value="item.sortOrder"></label>
      <label>단계 제목<input value="새우 손질"></label>
      <label>설명<textarea v-model="(form as MediaItem).description" rows="4"></textarea></label>
      <label class="check-row"><input v-model="(form as MediaItem).isPublicAllowed" type="checkbox"> 블로그 공개 허용</label>
      <button class="button primary full" @click="save">이미지 설정 저장</button>
    </template>
  </aside>
</template>
