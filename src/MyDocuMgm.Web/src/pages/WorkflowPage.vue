<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { onBeforeRouteLeave, useRouter } from 'vue-router'
import { isNewWorkId } from '../app/router'
import WorkflowStepper from '../features/content-workflow/WorkflowStepper.vue'
import WorkflowFooter from '../features/content-workflow/WorkflowFooter.vue'
import UrlStage from '../features/content-workflow/UrlStage.vue'
import DetailStage from '../features/content-workflow/DetailStage.vue'
import CompletedStage from '../features/content-workflow/CompletedStage.vue'
import AnalysisReviewStage from '../features/analysis-review/AnalysisReviewStage.vue'
import CategoryEditingStage from '../features/category-editing/CategoryEditingStage.vue'
import BlogDraftStage from '../features/blog-draft/BlogDraftStage.vue'
import IngredientDialog from '../features/ingredient-editing/IngredientDialog.vue'
import ImageEditingStage from '../features/media-management/ImageEditingStage.vue'
import { useMediaState } from '../features/media-management/useMediaState'
import { api } from '../shared/api/client'
import type { BlogDraft as BlogDraftData, Category, CategoryEdit, ContentItem, CookingIngredient, DetailStage as DetailStageData, ImageStage, ImageStageMediaItem, LinkableMediaPage, UrlIntake, WorkflowStep } from '../shared/types'
import { workflowSteps } from '../shared/presentation/labels'
import { cloneValue } from '../shared/utils/clone'

const props = defineProps<{ id: string; step: string }>()
const router = useRouter()
const content = ref<ContentItem>()
const ingredients = ref<CookingIngredient[]>([])
const loadError = ref('')
const ingredientDialogOpen = ref(false)
const editingIngredient = ref<CookingIngredient>()
const dirty = ref(false)
const analysisIntake = ref<UrlIntake>()
const analysisTitle = ref('')
const analysisSummary = ref('')
const analysisSaving = ref(false)
const analysisError = ref('')
const categoryEdit = ref<CategoryEdit>()
const categoryOptions = ref<Category[]>([])
const categoryEditCategoryId = ref('')
const categoryEditValues = ref<Record<string, Record<string, string | null>>>({})
const categoryEditSaving = ref(false)
const categoryEditError = ref('')
const imageStage = ref<ImageStage>()
const imageSelectedMediaIds = ref(new Set<string>())
const imageSelectionItems = ref(new Map<string, ImageStageMediaItem>())
const imageStageSaving = ref(false)
const imageStageError = ref('')
const imageStageNotice = ref('')
const detailStage = ref<DetailStageData>()
const detailStageSaving = ref(false)
const detailStageError = ref('')
const detailStageNotice = ref('')
const blogDraftStage = ref<BlogDraftData>()
const blogDraftTitle = ref('')
const blogDraftBody = ref('')
const blogDraftSaving = ref(false)
const blogDraftError = ref('')
const blogDraftNotice = ref('')
const blogReuseConfirmed = ref(false)
const newlyUploadedImageIds = new Set<string>()
const showUnsaved = ref(false)
const showDelete = ref(false)
const deletingContent = ref(false)
const pendingAction = ref<null | (() => void)>(null)
const mediaState = useMediaState(() => props.id)
let loadGeneration = 0

const isNewWork = computed(() => isNewWorkId(props.id))
const current = computed<WorkflowStep>(() =>
  workflowSteps.find((item) => item.route === props.step)?.key ?? 'URL')
const currentIndex = computed(() => workflowSteps.findIndex((item) => item.key === current.value))
const previous = computed(() => currentIndex.value > 0 ? workflowSteps[currentIndex.value - 1] : undefined)
const next = computed(() => currentIndex.value < workflowSteps.length - 1 ? workflowSteps[currentIndex.value + 1] : undefined)
const selectedCategory = computed(() => categoryOptions.value.find(category => category.id === categoryEditCategoryId.value))
const selectedCategoryValues = computed(() =>
  categoryEditValues.value[selectedCategory.value?.code ?? ''] ?? {})
const ingredientsEnabled = computed(() =>
  selectedCategory.value?.code === 'COOKING' &&
  categoryEdit.value?.categoryId === categoryEditCategoryId.value)
const imageLibrary = computed<LinkableMediaPage>(() => ({
  items: mediaState.result.value.items.map(item => ({
    id: item.id,
    ownerContentId: props.id,
    originalFileName: item.originalFileName,
    thumbnailUrl: item.thumbnailUrl,
    mimeType: item.mimeType,
    sizeBytes: item.sizeBytes,
    width: item.width,
    height: item.height,
  })),
  totalCount: mediaState.result.value.totalCount,
  page: mediaState.result.value.page,
  pageSize: mediaState.result.value.pageSize,
  totalPages: mediaState.result.value.totalPages,
}))
const imageSelectedItems = computed(() => [...imageSelectedMediaIds.value]
  .flatMap(id => {
    const item = imageSelectionItems.value.get(id)
    return item ? [item] : []
  }))
const imageDisplayError = computed(() => imageStageError.value || mediaState.operationError.value)

function resetWorkflowState() {
  loadError.value = ''
  content.value = undefined
  ingredients.value = []
  ingredientDialogOpen.value = false
  editingIngredient.value = undefined
  dirty.value = false
  analysisIntake.value = undefined
  analysisTitle.value = ''
  analysisSummary.value = ''
  analysisSaving.value = false
  analysisError.value = ''
  categoryEdit.value = undefined
  categoryOptions.value = []
  categoryEditCategoryId.value = ''
  categoryEditValues.value = {}
  categoryEditSaving.value = false
  categoryEditError.value = ''
  imageStage.value = undefined
  imageSelectedMediaIds.value = new Set()
  imageSelectionItems.value = new Map()
  imageStageSaving.value = false
  imageStageError.value = ''
  imageStageNotice.value = ''
  detailStage.value = undefined
  detailStageSaving.value = false
  detailStageError.value = ''
  detailStageNotice.value = ''
  blogDraftStage.value = undefined
  blogDraftTitle.value = ''
  blogDraftBody.value = ''
  blogDraftSaving.value = false
  blogDraftError.value = ''
  blogDraftNotice.value = ''
  blogReuseConfirmed.value = false
  newlyUploadedImageIds.clear()
  showUnsaved.value = false
  showDelete.value = false
  pendingAction.value = null
  mediaState.reset()
}

async function load() {
  const generation = ++loadGeneration
  resetWorkflowState()
  if (isNewWork.value) return
  if (current.value === 'URL') return

  try {
    const nextContent = await api.content(props.id)
    const nextIntake = current.value === 'ANALYSIS_REVIEW'
      ? await api.urlIntake(props.id)
      : undefined
    const nextCategoryEdit = current.value === 'CATEGORY_EDIT'
      ? await api.categoryEdit(props.id)
      : undefined
    const nextCategories = current.value === 'CATEGORY_EDIT'
      ? await api.categories()
      : []
    const nextIngredients = current.value === 'CATEGORY_EDIT' && nextCategoryEdit?.categoryCode === 'COOKING'
      ? await api.ingredients(props.id)
      : []
    const nextImageStage = current.value === 'MEDIA'
      ? await api.imageStage(props.id)
      : undefined
    const nextDetailStage = current.value === 'DETAIL'
      ? await api.detailStage(props.id)
      : undefined
    const nextBlogDraft = current.value === 'BLOG_DRAFT'
      ? await api.blogDraft(props.id)
      : current.value === 'COMPLETED'
        ? await api.completion(props.id)
        : undefined
    if (generation !== loadGeneration) return
    content.value = nextContent
    analysisIntake.value = nextIntake
    analysisTitle.value = nextContent.title
    analysisSummary.value = nextContent.shortSummary ?? ''
    categoryEdit.value = nextCategoryEdit
    categoryOptions.value = nextCategories
    categoryEditCategoryId.value = nextCategoryEdit?.categoryId ?? ''
    categoryEditValues.value = cloneValue(nextCategoryEdit?.valuesByCategory ?? {})
    ingredients.value = nextIngredients
    imageStage.value = nextImageStage
    detailStage.value = nextDetailStage
    blogDraftStage.value = nextBlogDraft
    blogDraftTitle.value = nextBlogDraft?.title ?? ''
    blogDraftBody.value = nextBlogDraft?.body ?? ''
    blogReuseConfirmed.value = nextBlogDraft?.externalSourceReuseConfirmed ?? false
    if (nextImageStage) {
      imageSelectedMediaIds.value = new Set(nextImageStage.linkedMediaIds)
      imageSelectionItems.value = new Map(nextImageStage.linkedMedia.map(item => [item.id, item]))
    }
    if (current.value === 'MEDIA') {
      await mediaState.load()
      cacheCurrentImagePage()
    }
  } catch (error) {
    if (generation !== loadGeneration) return
    loadError.value = error instanceof Error ? error.message : '콘텐츠를 불러오지 못했습니다.'
  } finally {
    if (generation === loadGeneration) dirty.value = false
  }
}

function navigate(route?: string) {
  if (current.value === 'MEDIA' && mediaState.uploading.value) {
    imageStageError.value = '이미지 업로드가 끝난 뒤 이동해 주세요.'
    return
  }
  if (!route) return
  const action = () => router.push(`/workflow/${props.id}/${route}`)
  if (dirty.value) {
    pendingAction.value = action
    showUnsaved.value = true
  } else action()
}

function close() {
  const action = () => router.push('/contents')
  if (dirty.value) {
    pendingAction.value = action
    showUnsaved.value = true
  } else action()
}

async function discardAndContinue() {
  if (current.value === 'MEDIA' && newlyUploadedImageIds.size > 0) {
    const cleaned = await cleanupImageUploads([...newlyUploadedImageIds])
    if (!cleaned) {
      imageStageError.value = '새로 업로드한 이미지 정리에 실패하여 이동을 중단했습니다.'
      return
    }
  }
  dirty.value = false
  showUnsaved.value = false
  pendingAction.value?.()
  pendingAction.value = null
}

async function persistAnalysisReview(complete: boolean): Promise<boolean> {
  if (!content.value || !analysisIntake.value || analysisSaving.value) return false
  analysisError.value = ''
  if (!analysisTitle.value.trim()) {
    analysisError.value = '제목을 입력해 주세요.'
    return false
  }

  analysisSaving.value = true
  try {
    const updated = await api.saveAnalysisReview(
      content.value.id,
      analysisTitle.value,
      analysisSummary.value,
      complete,
      content.value.rowVersion,
    )
    content.value = updated
    analysisTitle.value = updated.title
    analysisSummary.value = updated.shortSummary ?? ''
    dirty.value = false
    return true
  } catch (error) {
    analysisError.value = error instanceof Error ? error.message : '분석 검토를 저장하지 못했습니다.'
    return false
  } finally {
    analysisSaving.value = false
  }
}

function selectCategory(value: string) {
  categoryEditCategoryId.value = value
  const code = categoryOptions.value.find(category => category.id === value)?.code
  if (code && !categoryEditValues.value[code]) categoryEditValues.value[code] = {}
}

function updateCategoryField(key: string, value: string) {
  const code = selectedCategory.value?.code
  if (!code) return
  const categoryValues = categoryEditValues.value[code] ?? {}
  categoryEditValues.value[code] = { ...categoryValues, [key]: value }
}

async function persistCategoryEdit(complete: boolean): Promise<boolean> {
  if (!content.value || !categoryEdit.value || categoryEditSaving.value) return false
  categoryEditError.value = ''
  if (!categoryEditCategoryId.value || !selectedCategory.value) {
    categoryEditError.value = '분류를 선택해 주세요.'
    return false
  }
  categoryEditSaving.value = true
  try {
    const updated = await api.saveCategoryEdit(
      content.value.id,
      categoryEditCategoryId.value,
      selectedCategoryValues.value,
      complete,
      categoryEdit.value.rowVersion,
    )
    categoryEdit.value = updated
    categoryEditCategoryId.value = updated.categoryId
    categoryEditValues.value = cloneValue(updated.valuesByCategory)
    content.value = {
      ...content.value,
      categoryId: updated.categoryId,
      categoryCode: updated.categoryCode,
      categoryDisplayName: categoryOptions.value.find(category => category.id === updated.categoryId)?.displayName
        ?? content.value.categoryDisplayName,
      currentWorkflowStep: updated.currentWorkflowStep,
      rowVersion: updated.rowVersion,
    }
    if (!complete && updated.categoryCode === 'COOKING') {
      try {
        ingredients.value = await api.ingredients(props.id)
      } catch {
        categoryEditError.value = '분류는 저장했지만 재료 목록을 다시 불러오지 못했습니다.'
      }
    }
    dirty.value = false
    return true
  } catch (error) {
    categoryEditError.value = error instanceof Error ? error.message : '분류별 편집을 저장하지 못했습니다.'
    return false
  } finally {
    categoryEditSaving.value = false
  }
}

function cacheCurrentImagePage() {
  const nextItems = new Map(imageSelectionItems.value)
  imageLibrary.value.items.forEach(item => nextItems.set(item.id, item))
  imageSelectionItems.value = nextItems
}

function applyImageStage(value: ImageStage) {
  imageStage.value = value
  imageSelectedMediaIds.value = new Set(value.linkedMediaIds)
  const nextItems = new Map(imageSelectionItems.value)
  value.linkedMedia.forEach(item => nextItems.set(item.id, item))
  imageSelectionItems.value = nextItems
  if (content.value) {
    content.value = {
      ...content.value,
      currentWorkflowStep: value.currentWorkflowStep,
      rowVersion: value.rowVersion,
    }
  }
}

async function setImagePage(page: number) {
  await mediaState.setPage(page)
  cacheCurrentImagePage()
}

function toggleImage(mediaId: string) {
  const nextIds = new Set(imageSelectedMediaIds.value)
  if (nextIds.has(mediaId)) nextIds.delete(mediaId)
  else nextIds.add(mediaId)
  imageSelectedMediaIds.value = nextIds
  cacheCurrentImagePage()
  dirty.value = true
  imageStageNotice.value = ''
}

async function uploadImage(file: File) {
  imageStageError.value = ''
  imageStageNotice.value = ''
  const uploaded = await mediaState.upload(file)
  if (!uploaded || mediaState.operationError.value) return
  const item: ImageStageMediaItem = {
    id: uploaded.item.id,
    ownerContentId: props.id,
    originalFileName: uploaded.item.originalFileName,
    thumbnailUrl: uploaded.item.thumbnailUrl,
    mimeType: uploaded.item.mimeType,
    sizeBytes: uploaded.item.sizeBytes,
    width: uploaded.item.width,
    height: uploaded.item.height,
  }
  if (!uploaded.reused) newlyUploadedImageIds.add(item.id)
  imageSelectionItems.value = new Map(imageSelectionItems.value).set(item.id, item)
  imageSelectedMediaIds.value = new Set([...imageSelectedMediaIds.value, item.id])
  dirty.value = true
}

async function cleanupImageUploads(ids: string[]): Promise<boolean> {
  if (ids.length === 0) return true
  const results = await Promise.allSettled(ids.map(id => api.permanentlyDeleteOrphanMedia(id)))
  let complete = true
  const nextIds = new Set(imageSelectedMediaIds.value)
  const nextItems = new Map(imageSelectionItems.value)
  results.forEach((result, index) => {
    const id = ids[index]!
    if (result.status === 'fulfilled') {
      newlyUploadedImageIds.delete(id)
      nextIds.delete(id)
      nextItems.delete(id)
    } else complete = false
  })
  imageSelectedMediaIds.value = nextIds
  imageSelectionItems.value = nextItems
  try {
    await mediaState.load()
    cacheCurrentImagePage()
  } catch {
    complete = false
  }
  return complete
}

async function cleanupUnselectedImageUploads(): Promise<boolean> {
  const unselected = [...newlyUploadedImageIds].filter(id => !imageSelectedMediaIds.value.has(id))
  return cleanupImageUploads(unselected)
}

async function persistImageStage(complete: boolean): Promise<boolean> {
  if (mediaState.uploading.value) {
    imageStageError.value = '이미지 업로드가 끝난 뒤 저장해 주세요.'
    return false
  }
  if (!content.value || !imageStage.value || imageStageSaving.value) return false
  imageStageError.value = ''
  imageStageNotice.value = ''
  imageStageSaving.value = true
  try {
    if (!await cleanupUnselectedImageUploads()) {
      imageStageError.value = '선택 해제한 신규 이미지 정리에 실패하여 저장을 중단했습니다.'
      return false
    }
    const updated = await api.saveImageStage(
      content.value.id,
      [...imageSelectedMediaIds.value],
      complete,
      imageStage.value.rowVersion,
    )
    applyImageStage(updated)
    newlyUploadedImageIds.clear()
    dirty.value = false
    imageStageNotice.value = complete
      ? '이미지 연결을 저장하고 자료 상세 단계로 이동합니다.'
      : '이미지 연결을 임시저장했습니다.'
    return true
  } catch (error) {
    const cleanupComplete = await cleanupImageUploads([...newlyUploadedImageIds])
    imageStageError.value = error instanceof Error ? error.message : '이미지 연결을 저장하지 못했습니다.'
    if (!cleanupComplete) {
      imageStageError.value += ' 신규 업로드 이미지 자동 정리에 실패했습니다. 휴지통·미디어 정리에서 확인해 주세요.'
    }
    return false
  } finally {
    imageStageSaving.value = false
  }
}

function applyDetailStage(value: DetailStageData) {
  detailStage.value = value
  if (content.value) {
    content.value = {
      ...content.value,
      currentWorkflowStep: value.currentWorkflowStep,
      rowVersion: value.rowVersion,
    }
  }
}

async function persistDetailStage(complete: boolean): Promise<boolean> {
  if (!content.value || !detailStage.value || detailStageSaving.value) return false
  detailStageError.value = ''
  detailStageNotice.value = ''
  detailStageSaving.value = true
  try {
    const updated = await api.saveDetailStage(
      content.value.id,
      {},
      complete,
      detailStage.value.rowVersion,
    )
    applyDetailStage(updated)
    dirty.value = false
    detailStageNotice.value = complete
      ? '통합 검토를 완료하고 블로그 초안 단계로 이동합니다.'
      : '현재 통합 검토 상태를 확인했습니다.'
    return true
  } catch (error) {
    detailStageError.value = error instanceof Error ? error.message : '자료 상세 검토를 저장하지 못했습니다.'
    return false
  } finally {
    detailStageSaving.value = false
  }
}

function applyBlogDraft(value: BlogDraftData) {
  blogDraftStage.value = value
  blogDraftTitle.value = value.title
  blogDraftBody.value = value.body
  blogReuseConfirmed.value = value.externalSourceReuseConfirmed
  if (content.value) {
    content.value = {
      ...content.value,
      currentWorkflowStep: value.currentWorkflowStep,
      rowVersion: value.contentRowVersion,
    }
  }
}

async function persistBlogDraft(complete: boolean): Promise<boolean> {
  if (!content.value || !blogDraftStage.value || blogDraftSaving.value) return false
  blogDraftError.value = ''
  blogDraftNotice.value = ''
  if (blogDraftTitle.value.length > blogDraftStage.value.titleMaxLength) {
    blogDraftError.value = `초안 제목은 ${blogDraftStage.value.titleMaxLength.toLocaleString()}자 이하여야 합니다.`
    return false
  }
  if (blogDraftBody.value.length > blogDraftStage.value.bodyMaxLength) {
    blogDraftError.value = `초안 본문은 ${blogDraftStage.value.bodyMaxLength.toLocaleString()}자 이하여야 합니다.`
    return false
  }
  if (complete && !blogDraftTitle.value.trim()) {
    blogDraftError.value = '완료하려면 초안 제목을 입력해 주세요.'
    return false
  }
  if (complete && !blogDraftBody.value.trim()) {
    blogDraftError.value = '완료하려면 초안 본문을 입력해 주세요.'
    return false
  }

  blogDraftSaving.value = true
  try {
    const updated = await api.saveBlogDraft(
      content.value.id,
      { title: blogDraftTitle.value, body: blogDraftBody.value },
      complete,
      blogDraftStage.value.contentRowVersion,
      blogDraftStage.value.draftRowVersion,
      blogReuseConfirmed.value,
    )
    applyBlogDraft(updated)
    dirty.value = false
    blogDraftNotice.value = complete
      ? '초안을 저장하고 로컬 작업 흐름을 완료했습니다. 외부로 게시되지는 않습니다.'
      : '블로그 초안을 임시저장했습니다.'
    return true
  } catch (error) {
    blogDraftError.value = error instanceof Error ? error.message : '블로그 초안을 저장하지 못했습니다.'
    return false
  } finally {
    blogDraftSaving.value = false
  }
}

async function saveCurrent() {
  if (current.value === 'ANALYSIS_REVIEW') {
    await persistAnalysisReview(false)
    return
  }
  if (current.value === 'CATEGORY_EDIT') {
    await persistCategoryEdit(false)
    return
  }
  if (current.value === 'MEDIA') {
    await persistImageStage(false)
    return
  }
  if (current.value === 'DETAIL') {
    await persistDetailStage(false)
    return
  }
  if (current.value === 'BLOG_DRAFT') {
    await persistBlogDraft(false)
    return
  }
  dirty.value = false
}

async function goNext() {
  if (current.value === 'ANALYSIS_REVIEW') {
    if (await persistAnalysisReview(true)) {
      await router.push(`/workflow/${props.id}/${next.value?.route ?? 'category-edit'}`)
    }
    return
  }
  if (current.value === 'CATEGORY_EDIT') {
    if (await persistCategoryEdit(true)) {
      await router.push(`/workflow/${props.id}/${next.value?.route ?? 'media'}`)
    }
    return
  }
  if (current.value === 'MEDIA') {
    if (await persistImageStage(true)) {
      await router.push(`/workflow/${props.id}/${next.value?.route ?? 'detail'}`)
    }
    return
  }
  if (current.value === 'DETAIL') {
    if (await persistDetailStage(true)) {
      await router.push(`/workflow/${props.id}/${next.value?.route ?? 'blog-draft'}`)
    }
    return
  }
  if (current.value === 'BLOG_DRAFT') {
    if (await persistBlogDraft(true)) {
      await router.push(`/workflow/${props.id}/${next.value?.route ?? 'completed'}`)
    }
    return
  }
  navigate(next.value?.route)
}

async function saveDraft() {
  if (current.value === 'ANALYSIS_REVIEW') {
    const action = pendingAction.value
    if (!await persistAnalysisReview(false)) return
    showUnsaved.value = false
    pendingAction.value = null
    action?.()
    return
  }
  if (current.value === 'CATEGORY_EDIT') {
    const action = pendingAction.value
    if (!await persistCategoryEdit(false)) return
    showUnsaved.value = false
    pendingAction.value = null
    action?.()
    return
  }
  if (current.value === 'MEDIA') {
    const action = pendingAction.value
    if (!await persistImageStage(false)) return
    showUnsaved.value = false
    pendingAction.value = null
    action?.()
    return
  }
  if (current.value === 'DETAIL') {
    const action = pendingAction.value
    if (!await persistDetailStage(false)) return
    showUnsaved.value = false
    pendingAction.value = null
    action?.()
    return
  }
  if (current.value === 'BLOG_DRAFT') {
    const action = pendingAction.value
    if (!await persistBlogDraft(false)) return
    showUnsaved.value = false
    pendingAction.value = null
    action?.()
    return
  }
  dirty.value = false
  showUnsaved.value = false
  pendingAction.value?.()
  pendingAction.value = null
}

function editIngredient(value?: CookingIngredient) {
  editingIngredient.value = value ? cloneValue(value) : undefined
  ingredientDialogOpen.value = true
}

async function saveIngredient(value: CookingIngredient) {
  await api.saveIngredient(props.id, value)
  ingredients.value = await api.ingredients(props.id)
  ingredientDialogOpen.value = false
  dirty.value = true
}

async function deleteIngredient(value: CookingIngredient) {
  if (!window.confirm(`‘${value.name}’ 재료를 삭제하시겠습니까?`)) return
  await api.deleteIngredient(props.id, value)
  ingredients.value = await api.ingredients(props.id)
  dirty.value = true
}

async function unsetPrimary(value: CookingIngredient) {
  await api.saveIngredient(props.id, { ...value, isPrimary: false })
  ingredients.value = await api.ingredients(props.id)
  dirty.value = true
}

function markDirty() { dirty.value = true }

function acceptUrl(contentId: string) {
  dirty.value = false
  if (contentId !== props.id) void router.replace(`/workflow/${contentId}/url`)
}

async function softDeleteContent() {
  if (!content.value || deletingContent.value) return
  deletingContent.value = true
  loadError.value = ''
  try {
    await api.softDeleteContent(content.value.id, content.value.rowVersion)
    showDelete.value = false
    dirty.value = false
    await router.push('/contents')
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '콘텐츠를 휴지통으로 이동하지 못했습니다.'
  } finally {
    deletingContent.value = false
  }
}

onBeforeRouteLeave((to) => {
  if (current.value === 'MEDIA' && mediaState.uploading.value) {
    imageStageError.value = '이미지 업로드가 끝난 뒤 이동해 주세요.'
    return false
  }
  if (!dirty.value) return true
  pendingAction.value = () => { void router.push(to.fullPath) }
  showUnsaved.value = true
  return false
})

watch(() => [props.id, props.step], load, { immediate: true })
</script>

<template>
  <main class="page workflow-page">
    <WorkflowStepper
      :current="current"
      :show-tools="current === 'DETAIL'"
      :show-edit="false"
      @edit="markDirty"
      @delete="showDelete = true"
      @close="close"
    />

    <header class="page-title compact-title">
      <div>
        <span class="eyebrow">7단계 작업 흐름</span>
        <h1>{{ content?.title ?? (isNewWork ? '새 작업' : current === 'URL' ? 'URL 접수 작업' : '콘텐츠 불러오는 중') }}</h1>
        <p>{{ workflowSteps[currentIndex]?.label }} 단계에서 필요한 정보만 차분하게 확인합니다.</p>
      </div>
      <span v-if="dirty" class="dirty-indicator">저장하지 않은 변경</span>
    </header>

    <section v-if="loadError" class="surface empty-state" role="alert">
      <h2>콘텐츠를 불러오지 못했습니다</h2>
      <p>{{ loadError }}</p>
      <button class="button" @click="router.push('/contents')">작업목록으로 돌아가기</button>
    </section>

    <UrlStage
      v-else-if="current === 'URL'"
      :key="props.id"
      :content-id="props.id"
      :new-work="isNewWork"
      @dirty="markDirty"
      @saved="dirty = false"
      @accepted="acceptUrl"
    />
    <section v-else-if="isNewWork" class="surface empty-state" data-testid="new-work-empty-stage">
      <h2>{{ workflowSteps[currentIndex]?.label }} 데이터가 없습니다</h2>
      <p>새 작업은 이전 작업의 데이터와 연결되지 않습니다. URL 단계부터 새 내용을 입력해 주세요.</p>
    </section>
    <AnalysisReviewStage
      v-else-if="current === 'ANALYSIS_REVIEW' && analysisIntake"
      :title="analysisTitle"
      :short-summary="analysisSummary"
      :intake="analysisIntake"
      :error="analysisError"
      @title="analysisTitle = $event"
      @summary="analysisSummary = $event"
      @dirty="markDirty"
    />

    <template v-else-if="current === 'CATEGORY_EDIT' && categoryEdit">
      <CategoryEditingStage
        :title="categoryEdit.title"
        :short-summary="categoryEdit.shortSummary"
        :categories="categoryOptions"
        :category-id="categoryEditCategoryId"
        :values="selectedCategoryValues"
        :ingredients="ingredients"
        :ingredients-enabled="ingredientsEnabled"
        :saving="categoryEditSaving"
        :error="categoryEditError"
        @dirty="markDirty"
        @category="selectCategory"
        @field="updateCategoryField"
        @add="editIngredient()"
        @edit="editIngredient"
        @delete="deleteIngredient"
        @unset="unsetPrimary"
      />
      <IngredientDialog
        :open="ingredientDialogOpen"
        :value="editingIngredient"
        @close="ingredientDialogOpen = false"
        @save="saveIngredient"
      />
    </template>

    <ImageEditingStage
      v-else-if="current === 'MEDIA' && imageStage"
      :library="imageLibrary"
      :selected-items="imageSelectedItems"
      :selected-ids="imageSelectedMediaIds"
      :busy="imageStageSaving || mediaState.uploading.value"
      :error="imageDisplayError"
      :notice="imageStageNotice"
      :preview-url="mediaState.uploadPreviewUrl.value"
      :file-name="mediaState.uploadFile.value?.name"
      :progress="mediaState.uploadProgress.value"
      :uploading="mediaState.uploading.value"
      :upload-result="mediaState.uploadResult.value"
      @upload="uploadImage"
      @cancel-upload="mediaState.cancelUpload"
      @clear-preview="mediaState.clearPreview"
      @toggle="toggleImage"
      @page="setImagePage"
    />

    <DetailStage
      v-else-if="current === 'DETAIL' && detailStage"
      :stage="detailStage"
      :saving="detailStageSaving"
      :error="detailStageError"
      :notice="detailStageNotice"
    />
    <BlogDraftStage
      v-else-if="current === 'BLOG_DRAFT'"
      :stage="blogDraftStage"
      :title="blogDraftTitle"
      :body="blogDraftBody"
      :saving="blogDraftSaving"
      :error="blogDraftError"
      :notice="blogDraftNotice"
      :reuse-confirmed="blogReuseConfirmed"
      @title="blogDraftTitle = $event"
      @body="blogDraftBody = $event"
      @reuse-confirmation="blogReuseConfirmed = $event"
      @dirty="markDirty"
    />
    <CompletedStage v-else :stage="blogDraftStage" @close="router.push('/contents')" />

    <WorkflowFooter
      v-if="!loadError && current !== 'COMPLETED'"
      :previous-label="previous?.label"
      :next-label="next?.label"
      :save-label="analysisSaving || categoryEditSaving || imageStageSaving || detailStageSaving || blogDraftSaving ? '저장 중…' : '임시저장'"
      :disabled="analysisSaving || categoryEditSaving || imageStageSaving || detailStageSaving || blogDraftSaving || mediaState.uploading.value"
      @previous="navigate(previous?.route)"
      @save="saveCurrent"
      @next="goNext"
    />

    <div v-if="showUnsaved" class="modal-backdrop" role="presentation">
      <section class="dialog unsaved-dialog" role="dialog" aria-modal="true" aria-labelledby="unsaved-title">
        <header><div><span class="eyebrow">저장 확인</span><h3 id="unsaved-title">변경 내용을 저장하지 않았습니다</h3></div></header>
        <p>현재 단계에서 입력한 내용을 저장한 뒤 이동하거나, 변경을 버리고 이동할 수 있습니다.</p>
        <footer>
          <button class="button" @click="showUnsaved = false; pendingAction = null">계속 편집</button>
          <button class="button danger-quiet" @click="discardAndContinue">변경 버리기</button>
          <button class="button primary" @click="saveDraft">저장 후 이동</button>
        </footer>
      </section>
    </div>

    <div v-if="showDelete" class="modal-backdrop" role="presentation">
      <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="delete-title">
        <header><h3 id="delete-title">콘텐츠를 삭제하시겠습니까?</h3></header>
        <p>콘텐츠를 휴지통으로 이동합니다. 연결된 미디어와 파일은 삭제하지 않습니다.</p>
        <footer><button class="button" :disabled="deletingContent" @click="showDelete = false">취소</button><button class="button danger" :disabled="deletingContent" @click="softDeleteContent">{{ deletingContent ? '이동 중…' : '휴지통으로 이동' }}</button></footer>
      </section>
    </div>
  </main>
</template>
