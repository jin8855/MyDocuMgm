<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import CategoryEditor from '../features/category-management/CategoryEditor.vue'
import { api } from '../shared/api/client'
import type { Category, SearchAttribute } from '../shared/types'

type AttributeLoadState = 'idle' | 'loading' | 'loaded' | 'error'

const categories = ref<Category[]>([])
const selectedCode = ref('')
const notice = ref('')
const pageError = ref('')
const attributesByCode = reactive<Record<string, SearchAttribute[]>>({})
const attributeStateByCode = reactive<Record<string, AttributeLoadState>>({})
const attributeRequests = new Map<string, Promise<void>>()

const selected = computed(() => categories.value.find(value => value.code === selectedCode.value))
const selectedAttributes = computed(() => attributesByCode[selectedCode.value] ?? [])
const selectedAttributeState = computed(() => attributeStateByCode[selectedCode.value] ?? 'idle')

function attributeStatus(code: string): string {
  const state = attributeStateByCode[code] ?? 'idle'
  if (state === 'loading' || state === 'idle') return '불러오는 중'
  if (state === 'error') return '불러오기 실패'
  return `${attributesByCode[code]?.length ?? 0}개 속성`
}

async function loadAttributes(code: string, force = false): Promise<void> {
  if (!force && attributeStateByCode[code] === 'loaded') return
  const existing = attributeRequests.get(code)
  if (existing) return existing

  const request = (async () => {
    attributeStateByCode[code] = 'loading'
    try {
      attributesByCode[code] = await api.attributes(code)
      attributeStateByCode[code] = 'loaded'
    } catch {
      attributeStateByCode[code] = 'error'
    } finally {
      attributeRequests.delete(code)
    }
  })()
  attributeRequests.set(code, request)
  return request
}

async function selectCategory(value: Category) {
  selectedCode.value = value.code
  await loadAttributes(value.code)
}

onMounted(async () => {
  try {
    categories.value = await api.categories()
    const initial = categories.value.find(value => value.code === 'COOKING') ?? categories.value[0]
    if (!initial) return
    selectedCode.value = initial.code
    await loadAttributes(initial.code)
    void Promise.all(categories.value.filter(value => value.code !== initial.code).map(value => loadAttributes(value.code)))
  } catch {
    pageError.value = '분류 목록을 불러오지 못했습니다.'
  }
})

async function saveCategory(value: Category) {
  const saved = await api.updateCategory(value.code, value)
  categories.value = categories.value.map(category => category.code === saved.code ? saved : category)
  notice.value = '분류 설정을 저장했습니다.'
}

async function saveAttribute(value: SearchAttribute) {
  await api.updateAttribute(value.categoryCode, value)
  await loadAttributes(value.categoryCode, true)
  notice.value = '검색 속성을 저장했습니다.'
}
</script>

<template>
  <div class="page">
    <header class="page-title">
      <h1>분류 관리</h1>
    </header>
    <p v-if="pageError" class="inline-error" role="alert">{{ pageError }}</p>
    <p v-if="notice" class="notice" role="status">{{ notice }}</p>
    <div class="category-layout">
      <aside class="surface category-list">
        <button
          v-for="category in categories"
          :key="category.code"
          type="button"
          :data-category-code="category.code"
          :class="{ active: selected?.code === category.code }"
          @click="selectCategory(category)"
        ><span>{{ category.displayName }}</span><small>{{ attributeStatus(category.code) }} · {{ category.isActive ? '활성' : '비활성' }}</small></button>
      </aside>
      <CategoryEditor
        v-if="selected"
        :category="selected"
        :attributes="selectedAttributes"
        :attribute-state="selectedAttributeState"
        @save-category="saveCategory"
        @save-attribute="saveAttribute"
        @retry-attributes="loadAttributes(selected.code, true)"
      />
    </div>
  </div>
</template>
