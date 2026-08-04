<script setup lang="ts">
defineProps<{ empty?: boolean }>()
const emit = defineEmits<{ dirty: [] }>()
</script>

<template>
  <section class="surface form-stack">
    <div class="section-title"><div><h2>원본 URL</h2><p>나중에 다시 찾을 수 있도록 출처를 기록합니다.</p></div></div>
    <label>URL<input :value="empty ? '' : 'https://example.com/recipe/shrimp-toast'" @input="emit('dirty')"></label>
    <label>메모<textarea :value="empty ? '' : '가족 간식으로 다시 만들어 보기'" rows="5" @input="emit('dirty')" /></label>
  </section>
</template>
