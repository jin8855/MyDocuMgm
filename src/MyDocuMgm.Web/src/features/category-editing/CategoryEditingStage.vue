<script setup lang="ts">
import type { CookingIngredient } from '../../shared/types'
import IngredientTable from '../ingredient-editing/IngredientTable.vue'
import PrimaryIngredientSelector from '../ingredient-editing/PrimaryIngredientSelector.vue'

defineProps<{ ingredients: CookingIngredient[] }>()
const emit = defineEmits<{
  dirty: []
  add: []
  edit: [value: CookingIngredient]
  delete: [value: CookingIngredient]
  unset: [value: CookingIngredient]
}>()
</script>

<template>
  <section class="surface form-stack">
    <div class="section-title"><div><h2>분류별 편집</h2><p>요리 분류에 필요한 필드와 검색 기준을 정리합니다.</p></div></div>
    <div class="form-grid three">
      <label>난이도<select @change="emit('dirty')"><option>쉬움</option><option>보통</option><option>어려움</option></select></label>
      <label>소요시간<input value="20분" @input="emit('dirty')"></label>
      <label>인분<input value="2인분" @input="emit('dirty')"></label>
    </div>
    <PrimaryIngredientSelector :ingredients="ingredients" @add="emit('add')" @edit="emit('edit', $event)" @unset="emit('unset', $event)" />
  </section>
  <IngredientTable :ingredients="ingredients" @add="emit('add')" @edit="emit('edit', $event)" @delete="emit('delete', $event)" />
</template>
