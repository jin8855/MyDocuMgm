<script setup lang="ts">
import type { CookingIngredient } from '../../shared/types'
import IngredientRow from './IngredientRow.vue'

defineProps<{ ingredients: CookingIngredient[] }>()
const emit = defineEmits<{ edit: [value: CookingIngredient]; delete: [value: CookingIngredient]; add: [] }>()
</script>

<template>
  <section class="surface">
    <div class="section-title"><div><h3>요리 재료</h3><p>재료명·분량·구분과 검색용 주재료를 관리합니다.</p></div><button class="button" @click="emit('add')">＋ 재료 추가</button></div>
    <div class="table-scroll">
      <table class="data-table">
        <thead><tr><th>순서</th><th>재료명</th><th>분량</th><th>구분</th><th><span class="sr-only">작업</span></th></tr></thead>
        <tbody><IngredientRow v-for="ingredient in ingredients" :key="ingredient.id" :ingredient="ingredient" @edit="emit('edit', $event)" @delete="emit('delete', $event)" /></tbody>
      </table>
    </div>
  </section>
</template>
