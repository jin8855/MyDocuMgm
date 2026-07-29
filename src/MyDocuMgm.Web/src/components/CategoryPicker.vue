<script setup lang="ts">
import type { Category } from '../types'

defineProps<{ categories: Category[]; modelValue: string }>()
defineEmits<{ 'update:modelValue': [value: string] }>()

const glyphs: Record<string, string> = {
  PLACE: '⌖', COOKING: '♨', EXERCISE: '↗', CLEANING_LAUNDRY: '✦',
  TRAVEL: '△', PHOTO: '◉', STUDY: 'Aa', PRODUCT: '□',
  PHONE_COMPUTER: '⌘', TIP: '!', OTHER: '···',
}
</script>

<template>
  <fieldset class="category-fieldset">
    <legend>어떤 생활 기록인가요?</legend>
    <div class="category-grid">
      <label v-for="category in categories" :key="category.code" :class="{ selected: modelValue === category.id }">
        <input
          type="radio"
          name="category"
          :value="category.id"
          :checked="modelValue === category.id"
          @change="$emit('update:modelValue', category.id)"
        >
        <span class="category-glyph" aria-hidden="true">{{ glyphs[category.code] }}</span>
        <span>{{ category.displayName }}</span>
      </label>
    </div>
  </fieldset>
</template>
