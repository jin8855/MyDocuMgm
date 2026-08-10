export type CategoryFieldKind = 'text' | 'number' | 'textarea'

export interface CategoryFieldDefinition {
  key: string
  label: string
  kind: CategoryFieldKind
  placeholder?: string
  min?: number
}

export const categoryFields: Record<string, CategoryFieldDefinition[]> = {
  PLACE: [
    { key: 'address', label: '주소', kind: 'text' },
    { key: 'businessHours', label: '운영시간', kind: 'text' },
    { key: 'parkingInfo', label: '주차', kind: 'text' },
    { key: 'recommendedMenuOrSpot', label: '추천 메뉴·장소', kind: 'text' },
  ],
  COOKING: [
    { key: 'servings', label: '인분', kind: 'number', min: 1 },
    { key: 'preparationMinutes', label: '준비 시간(분)', kind: 'number', min: 0 },
    { key: 'cookingMinutes', label: '조리 시간(분)', kind: 'number', min: 0 },
    { key: 'difficulty', label: '난이도', kind: 'text', placeholder: '예: 쉬움, 보통, 어려움' },
  ],
  EXERCISE: [
    { key: 'targetArea', label: '운동 부위', kind: 'text' },
    { key: 'durationMinutes', label: '운동 시간(분)', kind: 'number', min: 0 },
    { key: 'difficulty', label: '난이도', kind: 'text' },
    { key: 'equipment', label: '준비물', kind: 'text' },
  ],
  CLEANING_LAUNDRY: [
    { key: 'target', label: '대상', kind: 'text' },
    { key: 'supplies', label: '세제·제품', kind: 'text' },
    { key: 'precautions', label: '주의사항', kind: 'textarea' },
  ],
  TRAVEL: [
    { key: 'destination', label: '국가·지역', kind: 'text' },
    { key: 'bestSeason', label: '추천 시기', kind: 'text' },
    { key: 'transportation', label: '교통', kind: 'text' },
    { key: 'budgetNote', label: '비용 메모', kind: 'textarea' },
  ],
  PHOTO: [
    { key: 'camera', label: '기기', kind: 'text' },
    { key: 'lens', label: '렌즈', kind: 'text' },
    { key: 'shootingSettings', label: '촬영 설정', kind: 'text' },
    { key: 'location', label: '촬영 장소', kind: 'text' },
  ],
  STUDY: [
    { key: 'subject', label: '분야', kind: 'text' },
    { key: 'learningGoal', label: '학습 목표', kind: 'text' },
    { key: 'resource', label: '참고 자료', kind: 'text' },
    { key: 'reviewCycle', label: '복습 주기', kind: 'text' },
  ],
  PRODUCT: [
    { key: 'brand', label: '브랜드', kind: 'text' },
    { key: 'modelName', label: '모델명', kind: 'text' },
    { key: 'price', label: '가격', kind: 'number', min: 0 },
    { key: 'purchasePlace', label: '구매처', kind: 'text' },
  ],
  PHONE_COMPUTER: [
    { key: 'deviceOrOs', label: '기기·OS', kind: 'text' },
    { key: 'appOrProgram', label: '앱·프로그램', kind: 'text' },
    { key: 'problem', label: '문제', kind: 'textarea' },
    { key: 'solution', label: '해결 방법', kind: 'textarea' },
  ],
  TIP: [
    { key: 'situation', label: '적용 상황', kind: 'text' },
    { key: 'keyPoint', label: '핵심 포인트', kind: 'text' },
    { key: 'precautions', label: '주의사항', kind: 'textarea' },
  ],
  OTHER: [
    { key: 'customLabel', label: '주제', kind: 'text' },
    { key: 'additionalInfo', label: '추가 정보', kind: 'textarea' },
  ],
}
