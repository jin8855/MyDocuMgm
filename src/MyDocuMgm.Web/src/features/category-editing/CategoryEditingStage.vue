<script setup lang="ts">
import { computed } from 'vue'
import type { Category, CookingIngredient } from '../../shared/types'
import IngredientTable from '../ingredient-editing/IngredientTable.vue'
import PrimaryIngredientSelector from '../ingredient-editing/PrimaryIngredientSelector.vue'
import { categoryFields } from './categoryFields'
import HelpPopover from '../../shared/components/HelpPopover.vue'

const props = defineProps<{
  title: string
  shortSummary: string | null
  categories: Category[]
  categoryId: string
  values: Record<string, string | null>
  ingredients: CookingIngredient[]
  ingredientsEnabled: boolean
  saving: boolean
  error: string
}>()
const emit = defineEmits<{
  dirty: []
  category: [value: string]
  field: [key: string, value: string]
  add: []
  edit: [value: CookingIngredient]
  delete: [value: CookingIngredient]
  unset: [value: CookingIngredient]
}>()

const availableCategories = computed(() => props.categories.filter(category =>
  category.isActive || category.id === props.categoryId))
const selectedCategory = computed(() => props.categories.find(category => category.id === props.categoryId))
const fields = computed(() => categoryFields[selectedCategory.value?.code ?? ''] ?? [])

function selectCategory(event: Event) {
  emit('category', (event.target as HTMLSelectElement).value)
  emit('dirty')
}

function updateField(key: string, event: Event) {
  emit('field', key, (event.target as HTMLInputElement | HTMLTextAreaElement).value)
  emit('dirty')
}
</script>

<template>
  <section class="surface form-stack" data-testid="category-edit-stage" :aria-busy="saving">
    <div class="section-title">
      <div class="title-with-help"><h2>분류별 편집</h2><HelpPopover label="분류별 편집 도움말">자동 분류 없이 분류와 현재 저장 구조가 지원하는 상세 항목을 직접 정합니다.</HelpPopover></div>
    </div>
    <aside class="category-context" aria-label="분석 검토 결과">
      <strong>{{ title }}</strong><p>{{ shortSummary || '작성된 요약이 없습니다.' }}</p>
    </aside>
    <label for="category-edit-category">분류 <span aria-hidden="true">*</span>
      <select id="category-edit-category" :value="categoryId" :disabled="saving" required @change="selectCategory">
        <option value="" disabled>분류를 선택해 주세요</option>
        <option v-for="category in availableCategories" :key="category.id" :value="category.id">{{ category.displayName }}</option>
      </select>
    </label>
    <div v-if="selectedCategory" class="category-field-panel">
      <div class="section-title compact"><div><h3>{{ selectedCategory.displayName }} 상세 항목</h3></div></div>
      <div class="form-grid two">
        <label v-for="field in fields" :key="field.key" :for="`category-field-${field.key}`">
          {{ field.label }}
          <textarea v-if="field.kind === 'textarea'" :id="`category-field-${field.key}`" :value="values[field.key] ?? ''" :placeholder="field.placeholder" :disabled="saving" rows="4" @input="updateField(field.key, $event)" />
          <input v-else :id="`category-field-${field.key}`" :type="field.kind" :min="field.min" :value="values[field.key] ?? ''" :placeholder="field.placeholder" :disabled="saving" @input="updateField(field.key, $event)">
        </label>
      </div>
    </div>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <template v-if="selectedCategory?.code === 'COOKING'">
      <PrimaryIngredientSelector v-if="ingredientsEnabled" :ingredients="ingredients" @add="emit('add')" @edit="emit('edit', $event)" @unset="emit('unset', $event)" />
      <p v-else class="notice">요리 재료를 편집하려면 먼저 분류를 임시저장해 주세요.</p>
    </template>
  </section>
  <IngredientTable v-if="selectedCategory?.code === 'COOKING' && ingredientsEnabled" :ingredients="ingredients" @add="emit('add')" @edit="emit('edit', $event)" @delete="emit('delete', $event)" />
</template>
