<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import HelpPopover from '../../shared/components/HelpPopover.vue'
import type { Category, SearchAttribute } from '../../shared/types'
import { cloneValue } from '../../shared/utils/clone'

const props = defineProps<{
  category: Category
  attributes: SearchAttribute[]
  attributeState: 'idle' | 'loading' | 'loaded' | 'error'
}>()
const emit = defineEmits<{
  saveCategory: [value: Category]
  saveAttribute: [value: SearchAttribute]
  retryAttributes: []
}>()
const form = reactive<Category>(cloneValue(props.category))
const attributeForms = ref<SearchAttribute[]>(cloneValue(props.attributes))
watch(() => props.category, (value) => Object.assign(form, cloneValue(value)), { deep: true })
watch(() => props.attributes, value => { attributeForms.value = cloneValue(value) }, { deep: true })
function saveAttribute(value: SearchAttribute) { emit('saveAttribute', cloneValue(value)) }
function saveCategory() { emit('saveCategory', cloneValue(form)) }
</script>

<template>
  <section class="surface category-editor">
    <div class="section-title"><h3>{{ form.displayName }} 설정</h3></div>
    <div class="form-grid three">
      <label>표시명<input v-model="form.displayName" maxlength="80"></label>
      <label>정렬 순서<input v-model.number="form.sortOrder" type="number" min="1" max="11"></label>
      <label class="check-row"><input v-model="form.isActive" type="checkbox"> 활성 상태</label>
    </div>
    <div class="section-title attribute-title"><div class="title-with-help"><h3>검색 속성</h3><HelpPopover label="검색 속성 도움말">분류별로 미리 정해진 검색 항목입니다. 표시명, 순서, 활성 상태와 검색 사용 여부만 변경할 수 있습니다.</HelpPopover></div></div>
    <p v-if="attributeState === 'loading' || attributeState === 'idle'" class="empty-compact" role="status">검색 속성을 불러오는 중입니다.</p>
    <div v-else-if="attributeState === 'error'" class="attribute-error" role="alert">
      <p>검색 속성을 불러오지 못했습니다.</p>
      <button class="button" type="button" data-retry-attributes @click="emit('retryAttributes')">다시 시도</button>
    </div>
    <p v-else-if="attributeForms.length === 0" class="empty-compact">등록된 검색 속성이 없습니다.</p>
    <div v-else class="table-scroll">
    <table class="data-table category-attribute-table">
      <thead><tr><th>순서</th><th>표시명</th><th>활성</th><th>검색 사용</th><th>저장</th></tr></thead>
      <tbody>
        <tr v-for="attribute in attributeForms" :key="attribute.attributeKey" data-attribute-row>
          <td data-label="순서"><input v-model.number="attribute.sortOrder" class="tiny-input" type="number" min="1"></td>
          <td data-label="표시명"><input v-model="attribute.displayName" data-attribute-display-name></td>
          <td data-label="활성"><input v-model="attribute.isActive" type="checkbox"></td>
          <td data-label="검색 사용"><input v-model="attribute.isSearchable" type="checkbox"></td>
          <td data-label="저장"><button class="button compact" type="button" data-save-attribute @click="saveAttribute(attribute)">저장</button></td>
        </tr>
      </tbody>
    </table>
    </div>
    <footer class="editor-footer"><button class="button danger-quiet" @click="form.isActive = false">대분류 비활성화</button><button class="button primary" @click="saveCategory">분류 저장</button></footer>
  </section>
</template>
