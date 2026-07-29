<script setup lang="ts">
defineProps<{ page: number; totalPages: number }>()
const emit = defineEmits<{ page: [value: number] }>()
</script>

<template>
  <nav class="pagination" aria-label="이미지 페이지 이동">
    <button class="button" :disabled="page <= 1" @click="emit('page', page - 1)">← 이전</button>
    <span><strong>{{ page }}</strong> / {{ totalPages }}</span>
    <button class="button" :disabled="page >= totalPages" @click="emit('page', page + 1)">다음 →</button>
  </nav>
</template>
