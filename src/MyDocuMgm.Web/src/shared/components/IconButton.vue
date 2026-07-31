<script setup lang="ts">
import AppIcon from './AppIcon.vue'

withDefaults(defineProps<{
  icon: 'edit' | 'delete' | 'close'
  label: string
  tone?: 'default' | 'danger'
}>(), { tone: 'default' })

const emit = defineEmits<{ click: [] }>()
</script>

<template>
  <button
    type="button"
    class="icon-button"
    :class="{ 'danger-icon': tone === 'danger' }"
    :aria-label="label"
    :title="label"
    @click="emit('click')"
  >
    <AppIcon :name="icon" />
  </button>
</template>
