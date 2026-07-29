<script setup lang="ts">
import type { MediaFilter, MediaSort } from '../../shared/types'

defineProps<{ filter: MediaFilter; sort: MediaSort; pageSize: number; total: number; selected: number; duplicates: number }>()
const emit = defineEmits<{ filter: [value: MediaFilter]; sort: [value: MediaSort]; pageSize: [value: number] }>()
</script>

<template>
  <section class="media-toolbar">
    <div class="media-counts">
      <button :class="{ active: filter === 'ALL' }" @click="emit('filter', 'ALL')"><strong>{{ total }}</strong><span>전체</span></button>
      <button :class="{ active: filter === 'SELECTED' }" @click="emit('filter', 'SELECTED')"><strong>{{ selected }}</strong><span>선택</span></button>
      <button :class="{ active: filter === 'DUPLICATE' }" @click="emit('filter', 'DUPLICATE')"><strong>{{ duplicates }}</strong><span>중복 후보</span></button>
    </div>
    <div class="media-options">
      <label>정렬<select :value="sort" @change="emit('sort', ($event.target as HTMLSelectElement).value as MediaSort)"><option value="TIME_ASC">시간순 ↑</option><option value="TIME_DESC">시간순 ↓</option></select></label>
      <label>페이지 크기<select :value="pageSize" @change="emit('pageSize', Number(($event.target as HTMLSelectElement).value))"><option :value="24">24</option><option :value="48">48</option><option :value="96">96</option></select></label>
    </div>
  </section>
</template>
