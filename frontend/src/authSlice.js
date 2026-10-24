import { createAsyncThunk, createSlice } from '@reduxjs/toolkit'
import { apiRequest, getRoleFromToken } from './api'

function loadSession() {
  try {
    const session = JSON.parse(localStorage.getItem('booking-session'))
    if (session?.token && session?.user?.role) return session
  } catch {
    localStorage.removeItem('booking-session')
  }
  return null
}

const savedSession = loadSession()

export const register = createAsyncThunk('auth/register', async (details, { rejectWithValue }) => {
  try {
    return await apiRequest('/auth/register', {
      method: 'POST',
      body: JSON.stringify({
        email: details.email,
        password: details.password,
        name: details.name,
        role: Number(details.role) || 1,
      }),
    })
  } catch (error) {
    return rejectWithValue(error.message)
  }
})

export const login = createAsyncThunk('auth/login', async (credentials, { rejectWithValue }) => {
  try {
    const data = await apiRequest('/auth/login', {
      method: 'POST',
      body: JSON.stringify({
        email: credentials.email,
        password: credentials.password,
      }),
    })
    const role = getRoleFromToken(data.token)
    return {
      token: data.token,
      user: { email: credentials.email, role },
    }
  } catch (error) {
    return rejectWithValue(error.message)
  }
})

const authSlice = createSlice({
  name: 'auth',
  initialState: { session: savedSession, loading: false, error: null },
  reducers: {
    logout(state) {
      state.session = null
      state.error = null
    },
    clearAuthError(state) {
      state.error = null
    },
  },
  extraReducers(builder) {
    builder
      .addCase(register.pending, (state) => {
        state.loading = true
        state.error = null
      })
      .addCase(register.fulfilled, (state) => {
        state.loading = false
        state.error = null
      })
      .addCase(register.rejected, (state, action) => {
        state.loading = false
        state.error = action.payload
      })
      .addCase(login.pending, (state) => {
        state.loading = true
        state.error = null
      })
      .addCase(login.fulfilled, (state, action) => {
        state.loading = false
        state.session = action.payload
        state.error = null
      })
      .addCase(login.rejected, (state, action) => {
        state.loading = false
        state.error = action.payload
      })
  },
})

export const { clearAuthError, logout } = authSlice.actions
export default authSlice.reducer
