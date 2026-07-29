<script setup lang="ts">
defineProps<{ previousLabel?: string; nextLabel?: string; saveLabel?: string }>()
const emit = defineEmits<{ previous: []; save: []; next: [] }>()
</script>

<template>
  <footer class="workflow-footer">
    <button v-if="previousLabel" class="button" @click="emit('previous')">← 이전 · {{ previousLabel }}</button>
    <span v-else />
    <div>
      <button class="button" @click="emit('save')">{{ saveLabel ?? '임시저장' }}</button>
      <button v-if="nextLabel" class="button primary" @click="emit('next')">다음 · {{ nextLabel }} →</button>
    </div>
  </footer>
</template>
