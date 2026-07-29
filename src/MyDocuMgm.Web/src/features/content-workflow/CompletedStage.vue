<script setup lang="ts">
const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <section class="surface completion-card">
    <div class="completion-mark">✓</div><h2>생활 기록 정리가 끝났습니다</h2>
    <p>저장된 기록은 작업목록과 모바일 읽기 화면에서 다시 볼 수 있습니다.</p>
    <button class="button primary" @click="emit('close')">작업목록으로</button>
  </section>
</template>
