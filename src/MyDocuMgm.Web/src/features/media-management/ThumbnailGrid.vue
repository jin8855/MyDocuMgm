<script setup lang="ts">
import type { MediaItem } from '../../shared/types'

defineProps<{ items: MediaItem[]; activeId?: string }>()
const emit = defineEmits<{ select: [value: MediaItem]; toggle: [value: MediaItem] }>()
</script>

<template>
  <div class="thumbnail-grid" aria-label="이미지 썸네일 목록">
    <article v-for="item in items" :key="item.id" :class="{ active: activeId === item.id, selected: item.isSelected }" @click="emit('select', item)">
      <div class="thumbnail-image" :style="{ '--tone': String((item.sortOrder * 37) % 240) }">
        <span>{{ item.sortOrder }}</span>
        <button
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
