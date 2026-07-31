<script setup lang="ts">
import { computed } from 'vue'
import type { Category, SearchQuery } from '../../shared/types'
import { contentStatusOptions } from '../../shared/presentation/labels'

const props = defineProps<{ categories: Category[]; modelValue: SearchQuery }>()
const emit = defineEmits<{ 'update:modelValue': [value: SearchQuery]; search: []; reset: [] }>()
const selectedCategory = computed(() => props.categories.find((category) => category.code === props.modelValue.majorCategory))
const attributes = computed(() => selectedCategory.value?.attributes?.filter((attribute) => attribute.isActive && attribute.isSearchable) ?? [])

function update<K extends keyof SearchQuery>(key: K, value: SearchQuery[K]) {
  const next = { ...props.modelValue, [key]: value }
  if (key === 'majorCategory') {
    next.attributeKey = ''
    next.attributeValue = ''
  }
  emit('update:modelValue', next)
}
</script>

<template>
  <section class="search-panel" aria-label="작업목록 검색">
    <div class="search-grid">
      <label>대분류
        <select :value="modelValue.majorCategory" @change="update('majorCategory', ($event.target as HTMLSelectElement).value)">
          <option value="">전체</option>
          <option v-for="category in categories" :key="category.code" :value="category.code">{{ category.displayName }}</option>
        </select>
      </label>
      <label>중분류 검색 속성
        <select :value="modelValue.attributeKey" :disabled="!attributes.length" @change="update('attributeKey', ($event.target as HTMLSelectElement).value)">
          <option value="">전체 속성</option>
          <option v-for="attribute in attributes" :key="attribute.attributeKey" :value="attribute.attributeKey">{{ attribute.displayName }}</option>
        </select>
      </label>
      <label>속성값
        <input :value="modelValue.attributeValue" :disabled="!modelValue.attributeKey" placeholder="속성값 입력" @input="update('attributeValue', ($event.target as HTMLInputElement).value)">
      </label>
      <label>상태
        <select :value="modelValue.status" @change="update('status', ($event.target as HTMLSelectElement).value)">
          <option value="">전체</option>
          <option v-for="status in contentStatusOptions" :key="status.value" :value="status.value">{{ status.label }}</option>
        </select>
      </label>
      <label>검색 범위
        <select :value="modelValue.searchScope" @change="update('searchScope', ($event.target as HTMLSelectElement).value as SearchQuery['searchScope'])">
          <option value="ALL">전체 검색</option><option value="TAG">태그만 검색</option>
        </select>
      </label>
      <label class="keyword-field">검색어
        <input :value="modelValue.keyword" :placeholder="modelValue.searchScope === 'TAG' ? '태그명 입력' : '제목·요약·상세·속성·재료·태그'" @input="update('keyword', ($event.target as HTMLInputElement).value)" @keydown.enter.prevent="emit('search')">
      </label>
    </div>
    <div class="search-actions"><button class="button" @click="emit('reset')">초기화</button><button class="button primary" @click="emit('search')">검색</button></div>
  </section>
</template>
