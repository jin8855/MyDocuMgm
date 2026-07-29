<script setup lang="ts">
import type { CookingIngredient } from '../../shared/types'

defineProps<{ ingredient: CookingIngredient }>()
const emit = defineEmits<{ edit: [value: CookingIngredient]; delete: [value: CookingIngredient] }>()
</script>

<template>
  <tr>
    <td>{{ ingredient.sortOrder }}</td>
    <td><strong>{{ ingredient.name }}</strong><span v-if="ingredient.isPrimary" class="badge accent">검색용 주재료</span></td>
    <td>{{ ingredient.quantity || '—' }}</td>
    <td>{{ ingredient.ingredientType }}</td>
    <td class="icon-actions">
      <button title="재료 수정" :aria-label="`${ingredient.name} 재료 수정`" @click="emit('edit', ingredient)">✎<span class="icon-tip">수정</span></button>
      <button class="danger-icon" title="재료 삭제" :aria-label="`${ingredient.name} 재료 삭제`" @click="emit('delete', ingredient)">⌫<span class="icon-tip">삭제</span></button>
    </td>
  </tr>
</template>
