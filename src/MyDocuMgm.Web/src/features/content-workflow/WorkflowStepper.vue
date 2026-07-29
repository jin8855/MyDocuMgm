<script setup lang="ts">
import { computed } from 'vue'
import type { WorkflowStep } from '../../shared/types'
import { workflowSteps } from '../../shared/types'

const props = defineProps<{ current: WorkflowStep; showTools?: boolean }>()
const emit = defineEmits<{ edit: []; delete: []; close: [] }>()
const currentIndex = computed(() => workflowSteps.findIndex((step) => step.key === props.current))
</script>

<template>
  <div class="workflow-head">
    <ol class="workflow-steps" aria-label="전체 작업 단계">
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
      <button class="button" @click="emit('edit')">편집</button>
      <button class="button danger-quiet" @click="emit('delete')">삭제</button>
      <button class="button" @click="emit('close')">닫기</button>
    </div>
  </div>
</template>
