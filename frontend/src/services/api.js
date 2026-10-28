// Every call to the PennyWise Web API goes through this file, so no component
// ever builds an endpoint URL itself.
//
// The base path is relative ("/api"). Locally, Vite's dev server proxies it to
// the ASP.NET Core API; on AWS, Nginx on EC2 #1 proxies it to EC2 #2's private
// IP. VITE_API_BASE_URL exists only as an override and must never hold secrets,
// because Vite copies VITE_* values into the JavaScript the browser downloads.
//
// Signing in sets an HttpOnly cookie that the browser sends with every request
// to the same site; this code never sees or stores the session itself.
const BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/$/, '')

// The API refuses data changes without this header (CSRF protection; see
// ClientHeaderMiddleware.cs). Other websites cannot add it to their requests.
const CLIENT_HEADER = { 'X-PennyWise-Client': 'web' }

// Called when the session has ended (signed out elsewhere, expired cookie), so
// the app can show the login page. Set by the AuthProvider.
let onSessionEnded = () => {}
export function setSessionEndedHandler(handler) {
  onSessionEnded = handler
}

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
      headers: body ? { ...CLIENT_HEADER, 'Content-Type': 'application/json' } : CLIENT_HEADER,
      credentials: 'same-origin',
      body: body ? JSON.stringify(body) : undefined,
    })
  } catch {
    throw new ApiError('Cannot reach the PennyWise server. Check your connection and try again.', 0)
  }

  if (response.status === 204) return null

  const isJson = response.headers.get('content-type')?.includes('json')
  const data = isJson ? await response.json().catch(() => null) : null

  if (response.status === 401 && !path.startsWith('/auth/')) onSessionEnded()

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
  me: () => request('/auth/me'),
  register: (form) => request('/auth/register', { method: 'POST', body: form }),
  login: (credentials) => request('/auth/login', { method: 'POST', body: credentials }),
  logout: () => request('/auth/logout', { method: 'POST' }),

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
