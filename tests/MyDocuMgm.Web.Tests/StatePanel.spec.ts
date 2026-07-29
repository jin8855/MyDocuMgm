import { createApp } from 'vue'
import StatePanel from '../../src/MyDocuMgm.Web/src/components/StatePanel.vue'

describe('StatePanel', () => {
  it('renders an accessible error state', () => {
    const host = document.createElement('div')
    document.body.append(host)
    const app = createApp(StatePanel, {
      kind: 'error',
      title: '불러오기 실패',
      detail: '다시 시도해 주세요.',
    })
    app.mount(host)

    const panel = host.firstElementChild
    expect(panel?.getAttribute('role')).toBe('status')
    expect(panel?.classList.contains('error')).toBe(true)
    expect(panel?.textContent).toContain('다시 시도해 주세요.')

    app.unmount()
    host.remove()
  })
})
