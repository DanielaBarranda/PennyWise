// Every call to the PennyWise Web API goes through this file, so no component
// ever builds an endpoint URL itself.
//
// The base path is relative ("/api"). Locally, Vite's dev server proxies it to
// the ASP.NET Core API; on AWS, Nginx on EC2 #1 proxies it to EC2 #2's private
// IP. VITE_API_BASE_URL exists only as an override and must never hold secrets,
// because Vite copies VITE_* values into the JavaScript the browser downloads.
const BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/$/, '')

export class ApiError extends Error {
  constructor(message, status, fieldErrors = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    // ASP.NET Core validation errors, keyed by field name: { amount: ['...'] }
    this.fieldErrors = fieldErrors
  }
}

async function request(path, { method = 'GET', body, query } = {}) {
  const url = new URL(BASE_URL + path, window.location.origin)
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null && value !== '') {
      url.searchParams.set(key, value)
    }
  }

  let response
  try {
    response = await fetch(url, {
      method,
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
    })
  } catch {
    throw new ApiError('Cannot reach the PennyWise server. Check your connection and try again.', 0)
  }

  if (response.status === 204) return null

  const isJson = response.headers.get('content-type')?.includes('json')
  const data = isJson ? await response.json().catch(() => null) : null

  if (!response.ok) {
    // A non-JSON error usually comes from the proxy (Vite or Nginx), not the API,
    // which means the API itself is down or unreachable.
    if (!isJson && response.status >= 500) {
      throw new ApiError('The PennyWise server is not responding right now. Please try again shortly.', response.status)
    }
    throw new ApiError(
      data?.detail ?? data?.title ?? `The server returned an error (${response.status}).`,
      response.status,
      normalizeFieldErrors(data?.errors),
    )
  }

  return data
}

// ASP.NET Core sends keys like "Amount" or "$.amount"; the forms use "amount".
function normalizeFieldErrors(errors) {
  const result = {}
  for (const [key, messages] of Object.entries(errors ?? {})) {
    const field = key.replace(/^\$\./, '')
    result[field.charAt(0).toLowerCase() + field.slice(1)] = messages.join(' ')
  }
  return result
}

export const api = {
  getDashboardSummary: (month) => request('/dashboard/summary', { query: { month } }),

  getBudgets: (month) => request('/budgets', { query: { month } }),
  getBudget: (id) => request(`/budgets/${id}`),
  createBudget: (budget) => request('/budgets', { method: 'POST', body: budget }),
  updateBudget: (id, budget) => request(`/budgets/${id}`, { method: 'PUT', body: budget }),

  getExpenses: ({ month, category } = {}) => request('/expenses', { query: { month, category } }),
  getExpense: (id) => request(`/expenses/${id}`),
  createExpense: (expense) => request('/expenses', { method: 'POST', body: expense }),
  updateExpense: (id, expense) => request(`/expenses/${id}`, { method: 'PUT', body: expense }),
  deleteExpense: (id) => request(`/expenses/${id}`, { method: 'DELETE' }),
}
