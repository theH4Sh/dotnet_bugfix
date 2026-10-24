const API_BASE_URL = import.meta.env.VITE_API_URL ?? ''

let onUnauthorizedCallback = null
export function setOnUnauthorized(callback) {
  onUnauthorizedCallback = callback
}

export async function apiRequest(path, { token, ...options } = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  })

  if (!response.ok) {
    if (response.status === 401 && token && onUnauthorizedCallback) {
      onUnauthorizedCallback()
    }

    let message = ''
    try {
      const contentType = response.headers.get('content-type') ?? ''
      const body = contentType.includes('application/json')
        ? await response.json()
        : await response.text()

      if (typeof body === 'string') {
        message = body
      } else if (body && typeof body === 'object') {
        if (body.error) message = body.error
        else if (body.message) message = body.message
        else if (body.title) message = body.title
        else if (body.errors && typeof body.errors === 'object') {
          message = Object.values(body.errors).flat().join(', ')
        }
      }
    } catch {
      // Ignored
    }

    if (!message) {
      if (response.status === 401) message = 'Invalid email or password / Session expired.'
      else if (response.status === 403) message = 'You do not have permission to perform this action.'
      else if (response.status === 404) message = 'Item not found.'
      else if (response.status === 409) message = 'Email is already registered.'
      else message = `Request failed (${response.status})`
    }

    throw new Error(message)
  }

  if (response.status === 204) return null
  return response.json()
}

export function getRoleFromToken(token) {
  try {
    if (!token || typeof token !== 'string') throw new Error('Token is missing')
    const parts = token.split('.')
    if (parts.length < 2) throw new Error('Token is malformed')
    let base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    while (base64.length % 4) {
      base64 += '='
    }
    const payload = JSON.parse(atob(base64))
    const roleClaim = Object.entries(payload).find(([key]) =>
      key.toLowerCase() === 'role' ||
      key.toLowerCase() === 'roles' ||
      key.toLowerCase().endsWith('/claims/role'),
    )?.[1]
    const role = Array.isArray(roleClaim) ? roleClaim[0] : roleClaim
    if (role === 'Customer' || role === 1 || role === '1') return 'Customer'
    if (role === 'Performer' || role === 2 || role === '2') return 'Performer'
  } catch {
    // The API token should always be a JWT; the caller reports an invalid token.
  }
  throw new Error('The API returned an invalid login token.')
}
