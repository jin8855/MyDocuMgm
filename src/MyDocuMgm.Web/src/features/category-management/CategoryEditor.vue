<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { Category, SearchAttribute } from '../../shared/types'
import { cloneValue } from '../../shared/utils/clone'

const props = defineProps<{ category: Category }>()
const emit = defineEmits<{ saveCategory: [value: Category]; saveAttribute: [value: SearchAttribute] }>()
const form = reactive<Category>(cloneValue(props.category))
watch(() => props.category, (value) => Object.assign(form, cloneValue(value)), { deep: true })
function saveAttribute(value: SearchAttribute) { emit('saveAttribute', cloneValue(value)) }
function saveCategory() { emit('saveCategory', cloneValue(form)) }
</script>

<template>
  <section class="surface category-editor">
    <div class="section-title"><div><h3>{{ form.displayName }} 설정</h3><p>코드는 고정되며 표시와 검색 동작만 변경합니다.</p></div><span class="code-label">{{ form.code }}</span></div>
    <div class="form-grid three">
      <label>표시명<input v-model="form.displayName" maxlength="80"></label>
      <label>정렬 순서<input v-model.number="form.sortOrder" type="number" min="1" max="11"></label>
      <label class="check-row"><input v-model="form.isActive" type="checkbox"> 활성 상태</label>
    </div>
    <div class="section-title attribute-title"><div><h3>검색 속성</h3><p>코드에 등록된 안전한 속성만 검색에 노출합니다.</p></div></div>
    <table class="data-table">
      <thead><tr><th>순서</th><th>속성 키</th><th>표시명</th><th>활성</th><th>검색 사용</th><th></th></tr></thead>
      <tbody>
        <tr v-for="attribute in form.attributes" :key="attribute.attributeKey">
          <td><input v-model.number="attribute.sortOrder" class="tiny-input" type="number" min="1"></td>
          <td><code>{{ attribute.attributeKey }}</code></td>
          <td><input v-model="attribute.displayName"></td>
          <td><input v-model="attribute.isActive" type="checkbox"></td>
          <td><input v-model="attribute.isSearchable" type="checkbox"></td>
          <td><button class="button compact" @click="saveAttribute(attribute)">저장</button></td>
        </tr>
      </tbody>
    </table>
    <footer class="editor-footer"><button class="button danger-quiet" @click="form.isActive = false">대분류 비활성화</button><button class="button primary" @click="saveCategory">분류 저장</button></footer>
  </section>
</template>
