<script setup lang="ts">
defineProps<{ kind: 'loading' | 'empty' | 'error'; title: string; detail?: string }>()
</script>

<template>
  <section class="state-panel" :class="kind" role="status">
    <span class="state-symbol" aria-hidden="true">{{ kind === 'loading' ? '…' : kind === 'empty' ? '○' : '!' }}</span>
    <div><strong>{{ title }}</strong><p v-if="detail">{{ detail }}</p></div>
    <slot />
  </section>
</template>
