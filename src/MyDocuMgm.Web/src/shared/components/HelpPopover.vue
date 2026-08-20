<script lang="ts">
import { ref as moduleRef } from 'vue'

let helpSequence = 0
const activeHelpId = moduleRef<string | null>(null)
</script>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'

const props = defineProps<{ label: string }>()

const id = `help-popover-${++helpSequence}`
const root = ref<HTMLElement>()
const trigger = ref<HTMLButtonElement>()
const panel = ref<HTMLElement>()
const panelStyle = ref<Record<string, string>>({})
const isOpen = computed(() => activeHelpId.value === id)

function positionPanel() {
  if (!trigger.value || !panel.value) return
  const inset = 16
  const gap = 8
  const triggerRect = trigger.value.getBoundingClientRect()
  const panelRect = panel.value.getBoundingClientRect()
  const width = Math.min(380, Math.max(0, window.innerWidth - inset * 2))
  const maxLeft = Math.max(inset, window.innerWidth - width - inset)
  const left = Math.min(Math.max(inset, triggerRect.left), maxLeft)
  const below = triggerRect.bottom + gap
  const above = triggerRect.top - panelRect.height - gap
  const top = below + panelRect.height <= window.innerHeight - inset || above < inset ? below : above
  panelStyle.value = {
    top: `${Math.round(top)}px`,
    left: `${Math.round(left)}px`,
    width: `${Math.round(width)}px`,
  }
}

async function open() {
  activeHelpId.value = id
  await nextTick()
  positionPanel()
  panel.value?.focus()
}

function close(returnFocus = false) {
  if (!isOpen.value) return
  activeHelpId.value = null
  if (returnFocus) void nextTick(() => trigger.value?.focus())
}

function toggle() {
  if (isOpen.value) close(true)
  else void open()
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && isOpen.value) {
    event.preventDefault()
    close(true)
  }
}

function handleOutside(event: PointerEvent) {
  if (isOpen.value && !root.value?.contains(event.target as Node)) close()
}

onMounted(() => {
  document.addEventListener('keydown', handleKeydown)
  document.addEventListener('pointerdown', handleOutside)
  window.addEventListener('resize', positionPanel)
  window.addEventListener('scroll', positionPanel, true)
})

onBeforeUnmount(() => {
  document.removeEventListener('keydown', handleKeydown)
  document.removeEventListener('pointerdown', handleOutside)
  window.removeEventListener('resize', positionPanel)
  window.removeEventListener('scroll', positionPanel, true)
  if (isOpen.value) activeHelpId.value = null
})
</script>

<template>
  <span ref="root" class="help-popover">
    <button
      ref="trigger"
      class="help-trigger"
      type="button"
      :aria-label="props.label"
      :aria-controls="id"
      :aria-expanded="isOpen"
      @click="toggle"
      @keydown.enter.prevent="toggle"
      @keydown.space.prevent="toggle"
    >?</button>
    <span v-if="isOpen" :id="id" ref="panel" class="help-popover-panel" role="note" tabindex="-1" :style="panelStyle">
      <slot />
    </span>
  </span>
</template>
