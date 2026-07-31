<script setup lang="ts">
defineProps<{ name: 'edit' | 'delete' | 'close' }>()
</script>

<template>
  <svg class="app-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    <path v-if="name === 'edit'" d="M4 16.5V20h3.5L18.35 9.15l-3.5-3.5L4 16.5Zm16.7-10.05a1 1 0 0 0 0-1.4l-1.75-1.75a1 1 0 0 0-1.4 0l-1.37 1.37 3.5 3.5 1.02-1.72Z" />
    <path v-else-if="name === 'delete'" d="M7 20a2 2 0 0 1-2-2V7h14v11a2 2 0 0 1-2 2H7Zm9-16 1 1H7l1-1h3V3h2v1h3Z" />
    <path v-else d="m6.4 5 5.6 5.6L17.6 5 19 6.4 13.4 12l5.6 5.6-1.4 1.4-5.6-5.6L6.4 19 5 17.6l5.6-5.6L5 6.4 6.4 5Z" />
  </svg>
</template>
