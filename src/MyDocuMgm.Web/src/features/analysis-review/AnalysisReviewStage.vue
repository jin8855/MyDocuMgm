<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api } from '../../shared/api/client'
import HelpPopover from '../../shared/components/HelpPopover.vue'
import type {
  AnalysisRecommendationDecision,
  AnalysisRecommendationDecisionInput,
  AnalysisRecommendationItem,
  AnalysisRecommendationRun,
  Category,
  ContentItem,
  ManualPromptEvidenceKind,
  ManualRecommendationPrompt,
  UrlIntake,
} from '../../shared/types'

const props = defineProps<{
  content: ContentItem
  title: string
  shortSummary: string
  intake: UrlIntake
  error?: string
}>()

const emit = defineEmits<{
  dirty: []
  title: [value: string]
  summary: [value: string]
  recommendationSaved: [value: ContentItem]
}>()

type SelectableDecision = Exclude<AnalysisRecommendationDecision, 'PENDING'>

const run = ref<AnalysisRecommendationRun | null>(null)
const categories = ref<Category[]>([])
const loadingLatest = ref(true)
const buildingPrompt = ref(false)
const importingResponse = ref(false)
const saving = ref(false)
const recommendationError = ref('')
const recommendationNotice = ref('')
const decisions = ref<Record<string, SelectableDecision | ''>>({})
const modifiedValues = ref<Record<string, string>>({})
const promptResult = ref<ManualRecommendationPrompt | null>(null)
const promptText = ref('')
const pastedResponse = ref('')
const selectedEvidenceKinds = ref<ManualPromptEvidenceKind[]>([])

const allEvidenceKinds: ManualPromptEvidenceKind[] = [
  'CURRENT_TITLE', 'CURRENT_SUMMARY', 'DETAIL_CONTENT', 'MANUAL_CAPTION',
  'PINNED_AUTHOR_COMMENT', 'SOURCE_EVIDENCE', 'CURRENT_CATEGORY', 'CURRENT_TAGS',
]

const presentation = computed(() => {
  if (props.intake.sourceKind === 'GENERIC' && props.intake.sourceAcquisitionMode === 'HTTP_METADATA') {
    return {
      badge: 'URL 분석 검토', heading: '웹페이지 내용을 확인하고 제목과 요약을 작성하세요.',
      sourceType: '일반 웹 자료', acquisitionMode: 'URL 자동 수집',
      guide: '웹페이지에서 가져온 제목과 설명을 검토하고 필요한 내용을 수정하세요.', showManualInstagram: false,
    }
  }
  if (props.intake.sourceKind === 'INSTAGRAM' && props.intake.sourceAcquisitionMode === 'MANUAL') {
    return {
      badge: '수동 분석 검토', heading: '원문을 확인하고 제목과 요약을 작성하세요.',
      sourceType: props.intake.instagramContentType === 'POST' ? 'Instagram 게시물' : props.intake.instagramContentType === 'REEL' ? 'Instagram 릴스' : 'Instagram 자료',
      acquisitionMode: '수동 입력', guide: 'AI 분석이나 외부 수집 없이 사용자가 입력한 자료만 표시합니다.', showManualInstagram: true,
    }
  }
  return {
    badge: '분석 검토', heading: '자료 내용을 확인하고 제목과 요약을 작성하세요.', sourceType: '기타 자료',
    acquisitionMode: '확인 필요', guide: '자료 출처와 입력 방식을 확인하고 필요한 내용을 수정하세요.', showManualInstagram: false,
  }
})

const recommendationState = computed(() => {
  if (loadingLatest.value) return '이전 추천을 확인하는 중입니다.'
  if (buildingPrompt.value) return '현재 자료로 프롬프트를 만드는 중입니다.'
  if (importingResponse.value) return '붙여넣은 답변을 확인하는 중입니다.'
  if (!run.value) return '아직 가져온 추천이 없습니다.'
  if (run.value.status === 'FAILED' || run.value.status === 'CANCELLED') return ''
  if (run.value.status === 'PARTIALLY_SUCCEEDED') return '일부 항목만 추천되었습니다. 나머지는 직접 작성할 수 있습니다.'
  return `${run.value.items.length}개 추천을 검토할 수 있습니다.`
})

const missingRecommendationLabels = computed(() => {
  if (run.value?.status !== 'PARTIALLY_SUCCEEDED') return []
  const available = new Set(run.value.items.map(item => item.kind))
  return [
    ['TITLE', '제목'], ['SUMMARY', '요약'], ['CATEGORY', '분류'], ['TAG', '태그'],
  ].filter(([kind]) => !available.has(kind as AnalysisRecommendationItem['kind'])).map(([, label]) => label)
})

function itemLabel(item: AnalysisRecommendationItem): string {
  return { TITLE: '제목', SUMMARY: '요약', CATEGORY: '분류', TAG: '태그' }[item.kind]
}

function confidenceLabel(item: AnalysisRecommendationItem): string {
  return { LOW: '낮음 · 확인 필요', MEDIUM: '보통', HIGH: '높음' }[item.confidence]
}

function evidenceLabel(type: string): string {
  return { DETAIL_CONTENT: '현재 자료', MANUAL_CAPTION: '수동 Caption', PINNED_AUTHOR_COMMENT: '작성자 고정 댓글', SOURCE_EVIDENCE: '저장된 출처 근거' }[type] ?? '현재 자료'
}

function promptEvidenceLabel(kind: ManualPromptEvidenceKind): string {
  return {
    CURRENT_TITLE: '현재 제목', CURRENT_SUMMARY: '현재 요약', DETAIL_CONTENT: '일반 URL 본문',
    MANUAL_CAPTION: '수동 입력 Caption', PINNED_AUTHOR_COMMENT: '수동 작성자 고정 댓글',
    SOURCE_EVIDENCE: '기존 SourceEvidence', CURRENT_CATEGORY: '현재 분류', CURRENT_TAGS: '현재 태그',
  }[kind]
}

function displayValue(item: AnalysisRecommendationItem): string {
  if (item.kind !== 'CATEGORY') return item.recommendedValue
  return categories.value.find(category => category.id === item.recommendedValue)?.displayName ?? '확인할 수 없는 분류'
}

function resetDecisionDraft(nextRun: AnalysisRecommendationRun | null) {
  const nextDecisions: Record<string, SelectableDecision | ''> = {}
  const nextModified: Record<string, string> = {}
  for (const item of nextRun?.items ?? []) {
    nextDecisions[item.id] = item.decision === 'PENDING' ? '' : item.decision
    nextModified[item.id] = item.modifiedValue ?? item.recommendedValue
  }
  decisions.value = nextDecisions
  modifiedValues.value = nextModified
}

async function loadLatest() {
  loadingLatest.value = true
  recommendationError.value = ''
  try {
    const [latest, categoryValues] = await Promise.all([
      api.latestAnalysisRecommendations(props.content.id), api.categories(),
    ])
    run.value = latest ?? null
    categories.value = categoryValues
    resetDecisionDraft(run.value)
  } catch (error) {
    recommendationError.value = error instanceof Error ? error.message : '추천 이력을 불러오지 못했습니다.'
  } finally {
    loadingLatest.value = false
  }
}

async function buildPrompt() {
  if (buildingPrompt.value) return
  buildingPrompt.value = true
  recommendationError.value = ''
  recommendationNotice.value = ''
  try {
    const kinds = promptResult.value ? selectedEvidenceKinds.value : allEvidenceKinds
    const result = await api.createManualRecommendationPrompt(props.content.id, kinds)
    promptResult.value = result
    promptText.value = result.prompt
    selectedEvidenceKinds.value = [...new Set(result.evidence.map(value => value.kind))]
    pastedResponse.value = ''
    recommendationNotice.value = '프롬프트를 만들었습니다. 내용을 확인하고 필요한 부분을 직접 수정하세요.'
  } catch (error) {
    recommendationError.value = error instanceof Error ? error.message : '분석 프롬프트를 만들지 못했습니다.'
  } finally {
    buildingPrompt.value = false
  }
}

function toggleEvidence(kind: ManualPromptEvidenceKind, checked: boolean) {
  selectedEvidenceKinds.value = checked
    ? [...new Set([...selectedEvidenceKinds.value, kind])]
    : selectedEvidenceKinds.value.filter(value => value !== kind)
  recommendationNotice.value = '포함 자료가 변경되었습니다. 프롬프트를 다시 만들어 주세요.'
}

async function copyPrompt() {
  recommendationError.value = ''
  recommendationNotice.value = ''
  try {
    await navigator.clipboard.writeText(promptText.value)
    recommendationNotice.value = '프롬프트를 클립보드에 복사했습니다.'
  } catch {
    recommendationError.value = '클립보드에 복사하지 못했습니다. 프롬프트 텍스트를 직접 선택해 복사해 주세요.'
  }
}

async function importResponse() {
  if (!promptResult.value || importingResponse.value) return
  importingResponse.value = true
  recommendationError.value = ''
  recommendationNotice.value = ''
  try {
    run.value = await api.importManualAnalysisRecommendations(props.content.id, {
      schemaVersion: promptResult.value.schemaVersion,
      sourceFingerprint: promptResult.value.sourceFingerprint,
      pastedResponse: pastedResponse.value,
      idempotencyKey: crypto.randomUUID(),
      includedEvidenceIds: promptResult.value.evidence.map(value => value.evidenceId),
    })
    resetDecisionDraft(run.value)
    recommendationNotice.value = run.value.items.length
      ? '답변 형식을 확인했습니다. 추천별로 적용, 수정 또는 사용 안 함을 선택하세요.'
      : '답변 형식은 유효하지만 제공된 추천 항목이 없습니다. 직접 작성으로 계속할 수 있습니다.'
  } catch (error) {
    recommendationError.value = error instanceof Error ? error.message : '붙여넣은 답변을 확인하지 못했습니다.'
  } finally {
    importingResponse.value = false
  }
}

function clearResponse() {
  pastedResponse.value = ''
  recommendationError.value = ''
  recommendationNotice.value = ''
}

function choose(item: AnalysisRecommendationItem, decision: SelectableDecision) {
  if (item.decision !== 'PENDING') return
  decisions.value = { ...decisions.value, [item.id]: decision }
  if (decision === 'MODIFIED' && !modifiedValues.value[item.id]) {
    modifiedValues.value = { ...modifiedValues.value, [item.id]: item.recommendedValue }
  }
}

function updateModified(itemId: string, value: string) {
  modifiedValues.value = { ...modifiedValues.value, [itemId]: value }
}

async function saveDecisions() {
  if (!run.value || saving.value) return
  recommendationError.value = ''
  recommendationNotice.value = ''
  const inputs: AnalysisRecommendationDecisionInput[] = run.value.items
    .filter(item => item.decision === 'PENDING' && decisions.value[item.id])
    .map(item => ({ itemId: item.id, decision: decisions.value[item.id] as SelectableDecision, modifiedValue: decisions.value[item.id] === 'MODIFIED' ? modifiedValues.value[item.id] ?? '' : null }))
  if (inputs.length === 0) {
    recommendationError.value = '저장할 추천 항목의 적용, 수정 또는 사용 안 함을 선택해 주세요.'
    return
  }
  saving.value = true
  try {
    const result = await api.saveAnalysisRecommendationDecisions(props.content.id, run.value.id, props.content.rowVersion, inputs)
    run.value = result.run
    emit('recommendationSaved', result.content)
    recommendationNotice.value = '선택한 추천 결정을 저장했습니다.'
    resetDecisionDraft(result.run)
  } catch (error) {
    recommendationError.value = error instanceof Error ? error.message : '추천 결정을 저장하지 못했습니다.'
  } finally {
    saving.value = false
  }
}

function continueManually() { document.querySelector<HTMLInputElement>('#analysis-title')?.focus() }
function updateTitle(event: Event) { emit('title', (event.target as HTMLInputElement).value); emit('dirty') }
function updateSummary(event: Event) { emit('summary', (event.target as HTMLTextAreaElement).value); emit('dirty') }

onMounted(loadLatest)
</script>

<template>
  <section class="surface review-grid" data-testid="manual-analysis-review">
    <div>
      <span class="badge accent">{{ presentation.badge }}</span>
      <h2>{{ presentation.heading }}</h2>
      <p class="notice">{{ presentation.guide }}</p>
    </div>

    <dl class="intake-summary">
      <div><dt>자료 유형</dt><dd>{{ presentation.sourceType }}</dd></div>
      <div><dt>입력 방식</dt><dd>{{ presentation.acquisitionMode }}</dd></div>
      <div><dt>연결 이미지</dt><dd>{{ intake.linkedMediaIds.length }}개</dd></div>
      <div><dt>원본 URL</dt><dd>{{ intake.normalizedUrl }}</dd></div>
    </dl>

    <article v-if="presentation.showManualInstagram" class="manual-instagram-panel">
      <h3>수동 입력 Caption</h3>
      <p class="empty-compact">{{ intake.manualCaption || '입력된 Caption이 없습니다.' }}</p>
      <h3>작성자 고정 댓글</h3>
      <p class="empty-compact">{{ intake.pinnedAuthorCommentState === 'PRESENT' ? intake.pinnedAuthorCommentText : '작성자 고정 댓글 없음' }}</p>
    </article>

    <section class="recommendation-panel" aria-labelledby="recommendation-heading">
      <div class="section-title compact">
        <div>
          <span class="eyebrow">선택 기능</span>
          <div class="inline-heading"><h3 id="recommendation-heading">수동 AI 추천 가져오기</h3><HelpPopover label="분석 추천 도움말">MyDocuMgm은 AI 서비스로 자료를 자동 전송하지 않습니다. 추천 확신은 정확한 확률이 아니라 근거가 충분한지를 높음·보통·낮음으로 나타냅니다. 최종 결정은 사용자가 합니다.</HelpPopover></div>
          <p v-if="recommendationState">{{ recommendationState }}</p>
        </div>
        <button class="button secondary" type="button" :disabled="buildingPrompt" @click="buildPrompt">
          {{ buildingPrompt ? '프롬프트 만드는 중…' : promptResult ? '프롬프트 다시 만들기' : '분석 프롬프트 만들기' }}
        </button>
      </div>

      <p class="manual-ai-boundary">MyDocuMgm은 AI 서비스로 자료를 자동 전송하지 않습니다.</p>

      <section v-if="promptResult" class="manual-prompt-flow" aria-labelledby="prompt-preview-heading">
        <fieldset class="prompt-evidence-options">
          <legend>포함 자료 선택</legend>
          <label v-for="kind in [...new Set(promptResult.evidence.map(value => value.kind))]" :key="kind">
            <input
              type="checkbox"
              :checked="selectedEvidenceKinds.includes(kind)"
              @change="toggleEvidence(kind, ($event.target as HTMLInputElement).checked)"
            />
            {{ promptEvidenceLabel(kind) }}
          </label>
        </fieldset>

        <label id="prompt-preview-heading" for="manual-prompt-preview">프롬프트 미리보기</label>
        <textarea id="manual-prompt-preview" v-model="promptText" rows="12" spellcheck="false" />
        <p class="manual-ai-warning">복사한 내용을 외부 AI 도구에 붙여 넣으면 해당 서비스로 전송됩니다. 전송 전에 개인정보와 불필요한 내용을 확인하세요.</p>
        <div class="manual-prompt-actions">
          <button class="button secondary" type="button" @click="copyPrompt">프롬프트 복사</button>
        </div>

        <label for="manual-ai-response">외부 AI 도구의 답변을 붙여넣으세요</label>
        <textarea id="manual-ai-response" v-model="pastedResponse" rows="10" spellcheck="false" placeholder='{"schemaVersion":"mydocumgm.analysis-recommendation.v1", ...}' />
        <div class="manual-prompt-actions">
          <button class="button" type="button" :disabled="importingResponse || !pastedResponse.trim()" @click="importResponse">
            {{ importingResponse ? '확인 중…' : '답변 확인' }}
          </button>
          <button class="button secondary" type="button" :disabled="!pastedResponse" @click="clearResponse">다시 지우기</button>
          <button class="button ghost" type="button" @click="continueManually">수동으로 계속 작성</button>
        </div>
      </section>

      <p v-if="recommendationError" class="media-error" role="alert">{{ recommendationError }}</p>
      <p v-if="recommendationNotice" class="media-success" role="status">{{ recommendationNotice }}</p>
      <p v-if="missingRecommendationLabels.length" class="recommendation-missing" role="status">
        추천 없음: {{ missingRecommendationLabels.join(', ') }}
      </p>

      <div v-if="run && ['SUCCEEDED', 'PARTIALLY_SUCCEEDED'].includes(run.status)" class="recommendation-list">
        <article v-for="item in run.items" :key="item.id" class="recommendation-card" :data-kind="item.kind">
          <header>
            <strong>{{ itemLabel(item) }}</strong>
            <span class="confidence-badge" :class="item.confidence.toLowerCase()">추천 확신 {{ confidenceLabel(item) }}</span>
          </header>
          <div class="recommendation-comparison">
            <div>
              <small>현재 값</small>
              <p v-if="item.kind === 'TITLE'">{{ content.title }}</p>
              <p v-else-if="item.kind === 'SUMMARY'">{{ content.shortSummary || '비어 있음' }}</p>
              <p v-else-if="item.kind === 'CATEGORY'">{{ content.categoryDisplayName }}</p>
              <p v-else>{{ content.tags.join(', ') || '비어 있음' }}</p>
            </div>
            <div><small>추천 값</small><p>{{ displayValue(item) }}</p></div>
          </div>
          <p class="recommendation-reason"><strong>추천 근거</strong> {{ item.reason }}</p>
          <ul v-if="item.evidence.length" class="recommendation-evidence">
            <li v-for="(evidence, index) in item.evidence" :key="`${item.id}-${index}`"><span>{{ evidenceLabel(evidence.evidenceType) }}</span>{{ evidence.excerpt }}</li>
          </ul>

          <p v-if="item.decision !== 'PENDING'" class="decision-recorded">기록된 결정: {{ { APPLIED: '적용', MODIFIED: '수정 후 적용', REJECTED: '사용 안 함' }[item.decision] }}</p>
          <fieldset v-else class="decision-options">
            <legend class="visually-hidden">{{ itemLabel(item) }} 추천 결정</legend>
            <button type="button" class="button small" :class="{ active: decisions[item.id] === 'APPLIED' }" @click="choose(item, 'APPLIED')">적용</button>
            <button type="button" class="button small secondary" :class="{ active: decisions[item.id] === 'MODIFIED' }" @click="choose(item, 'MODIFIED')">수정 후 적용</button>
            <button type="button" class="button small ghost" :class="{ active: decisions[item.id] === 'REJECTED' }" @click="choose(item, 'REJECTED')">사용 안 함</button>
          </fieldset>

          <label v-if="item.decision === 'PENDING' && decisions[item.id] === 'MODIFIED'" :for="`modified-${item.id}`" class="modified-recommendation">
            수정할 값
            <select v-if="item.kind === 'CATEGORY'" :id="`modified-${item.id}`" :value="modifiedValues[item.id]" @change="updateModified(item.id, ($event.target as HTMLSelectElement).value)">
              <option v-for="category in categories" :key="category.id" :value="category.id">{{ category.displayName }}</option>
            </select>
            <textarea v-else-if="item.kind === 'SUMMARY'" :id="`modified-${item.id}`" :value="modifiedValues[item.id]" maxlength="500" rows="3" @input="updateModified(item.id, ($event.target as HTMLTextAreaElement).value)" />
            <input v-else :id="`modified-${item.id}`" :value="modifiedValues[item.id]" :maxlength="item.kind === 'TITLE' ? 200 : 100" @input="updateModified(item.id, ($event.target as HTMLInputElement).value)" />
          </label>
        </article>
      </div>

      <div class="recommendation-actions">
        <button v-if="!promptResult" type="button" class="button ghost" @click="continueManually">수동으로 계속 작성</button>
        <button v-if="run && run.items.some(item => item.decision === 'PENDING')" type="button" class="button" :disabled="saving" @click="saveDecisions">{{ saving ? '결정 저장 중…' : '선택한 결정 저장' }}</button>
      </div>
    </section>

    <label class="analysis-field" for="analysis-title">
      제목
      <input id="analysis-title" :value="title" maxlength="200" required autocomplete="off" @input="updateTitle" />
      <small>{{ title.length }} / 200</small>
    </label>

    <label class="analysis-field" for="analysis-summary">
      직접 작성한 요약
      <textarea id="analysis-summary" :value="shortSummary" maxlength="500" rows="6" @input="updateSummary" />
      <small>{{ shortSummary.length }} / 500</small>
    </label>

    <p v-if="error" class="media-error" role="alert">{{ error }}</p>
  </section>
</template>
