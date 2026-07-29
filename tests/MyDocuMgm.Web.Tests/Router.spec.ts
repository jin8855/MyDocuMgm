import { routes } from '../../src/MyDocuMgm.Web/src/app/router'

describe('router', () => {
  it('exposes all Phase 1A screens', () => {
    expect(routes.map((route) => route.path)).toEqual([
      '/',
      '/contents',
      '/categories',
      '/mobile',
      '/workflow/:id/:step',
      '/contents/:id',
      '/error',
    ])
  })
})
