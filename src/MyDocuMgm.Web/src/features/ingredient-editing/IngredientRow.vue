<script setup lang="ts">
import type { CookingIngredient } from '../../shared/types'
import IconButton from '../../shared/components/IconButton.vue'

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
      <IconButton icon="edit" :label="`${ingredient.name} 재료 수정`" @click="emit('edit', ingredient)" />
      <IconButton icon="delete" tone="danger" :label="`${ingredient.name} 재료 삭제`" @click="emit('delete', ingredient)" />
    </td>
  </tr>
</template>
