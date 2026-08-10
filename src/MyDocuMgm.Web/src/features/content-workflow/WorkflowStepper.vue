<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import type { WorkflowStep } from '../../shared/types'
import { workflowSteps } from '../../shared/presentation/labels'

const props = withDefaults(defineProps<{ current: WorkflowStep; showTools?: boolean; showEdit?: boolean }>(), { showEdit: true })
const emit = defineEmits<{ edit: []; delete: []; close: [] }>()
const currentIndex = computed(() => workflowSteps.findIndex((step) => step.key === props.current))
const stepList = ref<HTMLOListElement>()

async function revealCurrentStep() {
  await nextTick()
  if (typeof window === 'undefined' || !window.matchMedia?.('(max-width: 720px)').matches) return

  stepList.value
    ?.querySelector<HTMLElement>('[aria-current="step"]')
    ?.scrollIntoView({ behavior: 'auto', block: 'nearest', inline: 'center' })
}

watch(() => props.current, revealCurrentStep, { immediate: true })
</script>

<template>
  <div class="workflow-head">
    <ol ref="stepList" class="workflow-steps" aria-label="전체 작업 단계">
      <li
        v-for="(step, index) in workflowSteps"
        :key="step.key"
        :class="{ current: index === currentIndex, done: index < currentIndex }"
        :aria-current="index === currentIndex ? 'step' : undefined"
      >
        <span>{{ index + 1 }}</span>{{ step.label }}
      </li>
    </ol>
    <div v-if="showTools" class="workflow-tools">
      <button v-if="showEdit" class="button" @click="emit('edit')">편집</button>
      <button class="button danger-quiet" @click="emit('delete')">삭제</button>
      <button class="button" @click="emit('close')">닫기</button>
    </div>
  </div>
</template>
