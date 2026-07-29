<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { onBeforeRouteLeave, useRouter } from 'vue-router'
import { api } from '../api'
import CategoryPicker from '../components/CategoryPicker.vue'
import StatePanel from '../components/StatePanel.vue'
import type { Category, ContentStatus, SaveContent } from '../types'

const props = defineProps<{ id?: string }>()
const router = useRouter()
const categories = ref<Category[]>([])
const loading = ref(Boolean(props.id))
const saving = ref(false)
const error = ref('')
const dirty = ref(false)
const saved = ref(false)
const form = reactive<SaveContent>({
  categoryId: '',
  title: '',
  shortSummary: '',
  detailContent: '',
  status: 'INBOX',
  visibility: 'PRIVATE',
  isFavorite: false,
  experienceStatus: 'NONE',
  tags: [],
})
const tagText = ref('')
const heading = computed(() => props.id ? '기록 다듬기' : '새 생활 기록')

function markDirty() { if (!saved.value) dirty.value = true }
function addTag() {
  const value = tagText.value.trim()
  if (value && !form.tags.some((tag) => tag.localeCompare(value, 'ko', { sensitivity: 'base' }) === 0)) form.tags.push(value)
  tagText.value = ''
  markDirty()
}
async function save() {
  error.value = ''
  if (!form.categoryId || !form.title.trim()) { error.value = '분류와 제목을 입력해 주세요.'; return }
  saving.value = true
  try {
    const result = await api.save(form, props.id)
    saved.value = true
    dirty.value = false
    await router.push(`/contents/${result.id}`)
  } catch (reason) { error.value = reason instanceof Error ? reason.message : '저장하지 못했습니다.' }
  finally { saving.value = false }
}

onBeforeRouteLeave(() => !dirty.value || window.confirm('저장하지 않은 변경이 있습니다. 페이지를 나갈까요?'))
onMounted(async () => {
  categories.value = await api.categories()
  if (props.id) {
    try {
      const item = await api.content(props.id)
      Object.assign(form, {
        categoryId: item.categoryId,
        title: item.title,
        shortSummary: item.shortSummary ?? '',
        detailContent: item.detailContent ?? '',
        status: item.status as ContentStatus,
        visibility: item.visibility,
        isFavorite: item.isFavorite,
        experienceStatus: item.experienceStatus ?? 'NONE',
        tags: item.tags ?? [],
        rowVersion: item.rowVersion,
      })
    } catch (reason) { error.value = reason instanceof Error ? reason.message : '기록을 불러오지 못했습니다.' }
    finally { loading.value = false }
  }
})
</script>

<template>
  <div class="page narrow">
    <div class="page-heading form-heading"><div><p class="eyebrow">{{ props.id ? '기록 편집' : '기록 시작' }}</p><h1>{{ heading }}</h1><p>지금 기억나는 만큼만 적어도 충분합니다.</p></div><button class="button primary" :disabled="saving" @click="save">{{ saving ? '저장 중…' : '저장하기' }}</button></div>
    <StatePanel v-if="loading" kind="loading" title="기록을 펼치는 중입니다" />
    <form v-else class="editor" @input="markDirty" @submit.prevent="save">
      <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
      <CategoryPicker v-model="form.categoryId" :categories="categories" @update:model-value="markDirty" />
      <div class="field"><label for="title">제목 <strong>필수</strong></label><input id="title" v-model="form.title" maxlength="200" placeholder="나중에도 바로 알아볼 제목"></div>
      <div class="field"><label for="summary">한 줄 요약 <span>{{ form.shortSummary.length }}/500</span></label><input id="summary" v-model="form.shortSummary" maxlength="500" placeholder="이 기록에서 가장 중요한 한 가지"></div>
      <div class="field"><label for="detail">상세 내용 <span>{{ form.detailContent.length }}/20,000</span></label><textarea id="detail" v-model="form.detailContent" maxlength="20000" rows="10" placeholder="순서, 주의점, 다시 기억할 내용을 적어 주세요."></textarea></div>
      <div class="split-fields">
        <div class="field"><label for="status">정리 상태</label><select id="status" v-model="form.status"><option value="INBOX">받은 기록</option><option value="REVIEW_REQUIRED">확인 필요</option><option value="READY">정리 완료</option><option value="ARCHIVED">보관</option></select><small>DRAFTED·PUBLISHED는 블로그 기능 전에는 선택할 수 없습니다.</small></div>
        <div class="field"><label for="experience">경험 상태</label><select id="experience" v-model="form.experienceStatus"><option value="NONE">표시 안 함</option><option value="WANT_TO_TRY">해보고 싶음</option><option value="TRIED">해봄</option></select></div>
      </div>
      <div class="field"><label for="tags">태그</label><div class="tag-input"><input id="tags" v-model="tagText" placeholder="태그 입력 후 Enter" @keydown.enter.prevent="addTag"><button type="button" @click="addTag">추가</button></div><div class="tags"><button v-for="tag in form.tags" :key="tag" type="button" @click="form.tags = form.tags.filter((value) => value !== tag); markDirty()">#{{ tag }} ×</button></div></div>
      <div class="editor-options"><label><input v-model="form.isFavorite" type="checkbox"> 즐겨찾기에 표시</label><label><input v-model="form.visibility" type="checkbox" true-value="PUBLIC_ALLOWED" false-value="PRIVATE"> 향후 공개 허용</label><span>기본값은 비공개입니다.</span></div>
      <div class="mobile-save"><button class="button primary" :disabled="saving" type="submit">저장하기</button></div>
    </form>
  </div>
</template>
