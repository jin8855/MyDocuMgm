import type { ContentStatus, WorkflowStep } from '../types'

export const workflowStepLabels: Record<WorkflowStep, string> = {
  URL: 'URL',
  ANALYSIS_REVIEW: '분석 검토',
  CATEGORY_EDIT: '분류별 편집',
  MEDIA: '이미지',
  DETAIL: '자료 상세',
  BLOG_DRAFT: '블로그 초안',
  COMPLETED: '완료',
}

export const contentStatusLabels: Record<ContentStatus, string> = {
  INBOX: '받은 기록',
  REVIEW_REQUIRED: '검토 필요',
  READY: '정리 완료',
  ARCHIVED: '보관',
}

export const contentStatusOptions = (Object.entries(contentStatusLabels) as [ContentStatus, string][])
  .map(([value, label]) => ({ value, label }))

export const workflowSteps: { key: WorkflowStep; route: string; label: string }[] = [
  { key: 'URL', route: 'url', label: workflowStepLabels.URL },
  { key: 'ANALYSIS_REVIEW', route: 'analysis-review', label: workflowStepLabels.ANALYSIS_REVIEW },
  { key: 'CATEGORY_EDIT', route: 'category-edit', label: workflowStepLabels.CATEGORY_EDIT },
  { key: 'MEDIA', route: 'media', label: workflowStepLabels.MEDIA },
  { key: 'DETAIL', route: 'detail', label: workflowStepLabels.DETAIL },
  { key: 'BLOG_DRAFT', route: 'blog-draft', label: workflowStepLabels.BLOG_DRAFT },
  { key: 'COMPLETED', route: 'completed', label: workflowStepLabels.COMPLETED },
]

export const workflowStepLabel = (value: WorkflowStep) => workflowStepLabels[value]
export const contentStatusLabel = (value: ContentStatus) => contentStatusLabels[value]
