<script setup lang="ts">
import type { CookingIngredient } from '../../shared/types'

defineProps<{ ingredients: CookingIngredient[] }>()
const emit = defineEmits<{ edit: [value: CookingIngredient]; unset: [value: CookingIngredient]; add: [] }>()
</script>

<template>
  <section class="primary-ingredients">
    <div class="section-title"><div><h3>검색용 주재료</h3><p>여러 개를 지정할 수 있으며 해제해도 재료는 삭제되지 않습니다.</p></div><button class="button" @click="emit('add')">＋ 주재료 추가</button></div>
    <div class="chip-list">
      <span v-for="ingredient in ingredients.filter((item) => item.isPrimary)" :key="ingredient.id" class="query-chip">
        {{ ingredient.name }}
        <button title="주재료 이름 수정" :aria-label="`${ingredient.name} 주재료 이름 수정`" @click="emit('edit', ingredient)">✎</button>
        <button title="주재료 지정 해제" :aria-label="`${ingredient.name} 주재료 지정 해제`" @click="emit('unset', ingredient)">×</button>
      </span>
    </div>
  </section>
</template>
