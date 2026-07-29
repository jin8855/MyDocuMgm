<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { CookingIngredient } from '../../shared/types'
import { cloneValue } from '../../shared/utils/clone'

const props = defineProps<{ open: boolean; value?: CookingIngredient }>()
const emit = defineEmits<{ close: []; save: [value: CookingIngredient] }>()
const form = reactive<CookingIngredient>(empty())

function empty(): CookingIngredient {
  return { id: '', sortOrder: 0, name: '', quantity: '', ingredientType: '부재료', isPrimary: false, note: '', rowVersion: '' }
}
watch(() => [props.open, props.value] as const, () => Object.assign(form, props.value ? cloneValue(props.value) : empty()), { immediate: true })
function save() { if (form.name.trim()) emit('save', cloneValue(form)) }
</script>

<template>
  <div v-if="open" class="modal-backdrop" role="presentation" @mousedown.self="emit('close')">
    <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="ingredient-dialog-title">
      <header><h3 id="ingredient-dialog-title">{{ value ? '재료 수정' : '재료 추가' }}</h3><button class="icon-close" title="닫기" aria-label="재료 대화상자 닫기" @click="emit('close')">×</button></header>
      <label>재료명<input v-model="form.name" autofocus placeholder="예: 식빵"></label>
      <div class="dialog-grid"><label>분량<input v-model="form.quantity" placeholder="예: 4장"></label><label>구분<select v-model="form.ingredientType"><option>주재료</option><option>부재료</option><option>확인 필요</option></select></label></div>
      <label class="check-row"><input v-model="form.isPrimary" type="checkbox"> 검색용 주재료로 지정</label>
      <label>메모<input v-model="form.note" placeholder="선택 입력"></label>
      <footer><button class="button" @click="emit('close')">취소</button><button class="button primary" :disabled="!form.name.trim()" @click="save">저장</button></footer>
    </section>
  </div>
</template>
