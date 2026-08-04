<script setup lang="ts">
import { ref } from 'vue'
import type { MediaItem } from '../../shared/types'

defineProps<{ items: MediaItem[]; activeId?: string }>()
const emit = defineEmits<{ select: [value: MediaItem]; toggle: [value: MediaItem] }>()
const broken = ref(new Set<string>())

function markBroken(id: string) {
  broken.value = new Set(broken.value).add(id)
}
</script>

<template>
  <div class="thumbnail-grid" aria-label="이미지 썸네일 목록">
    <article
      v-for="item in items"
      :key="item.id"
      :class="{ active: activeId === item.id, selected: item.isSelected, deleted: item.isDeleted }"
      @click="emit('select', item)"
    >
      <div class="thumbnail-image">
        <img
          v-if="item.thumbnailUrl && !item.isDeleted && !broken.has(item.id)"
          :src="item.thumbnailUrl"
          :alt="item.originalFileName"
          loading="lazy"
          @error="markBroken(item.id)"
        >
        <span v-else>{{ item.isDeleted ? '삭제됨' : '미리보기 없음' }}</span>
        <button
          v-if="!item.isDeleted"
          class="select-toggle"
          :title="item.isSelected ? '선택 해제' : '선택'"
          :aria-label="`${item.originalFileName} ${item.isSelected ? '선택 해제' : '선택'}`"
          @click.stop="emit('toggle', item)"
        >{{ item.isSelected ? '✓' : '+' }}</button>
      </div>
      <strong>{{ item.originalFileName }}</strong>
      <small>{{ Math.floor((item.sourceTimestampMs ?? 0) / 1000) }}초 · {{ Math.ceil(item.sizeBytes / 1024) }}KB</small>
    </article>
  </div>
</template>
