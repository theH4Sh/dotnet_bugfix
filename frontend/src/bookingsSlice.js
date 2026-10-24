import { createAsyncThunk, createSlice } from '@reduxjs/toolkit'
import { apiRequest } from './api'

const request = (path, token, options) => apiRequest(path, { token, ...options })

export const loadMine = createAsyncThunk('bookings/loadMine', (_, { getState, rejectWithValue }) => {
  const token = getState().auth.session?.token
  if (!token) return rejectWithValue('Not authenticated')
  return request('/bookings/mine?size=100', token).catch((error) => rejectWithValue(error.message))
})

export const loadAvailable = createAsyncThunk(
  'bookings/loadAvailable',
  (_, { getState, rejectWithValue }) => {
    const token = getState().auth.session?.token
    if (!token) return rejectWithValue('Not authenticated')
    return request('/bookings/available?size=100', token).catch((error) => rejectWithValue(error.message))
  },
)

export const loadBooking = createAsyncThunk(
  'bookings/loadBooking',
  (id, { getState, rejectWithValue }) => {
    const token = getState().auth.session?.token
    if (!token) return rejectWithValue('Not authenticated')
    return request(`/bookings/${id}`, token).catch((error) => rejectWithValue(error.message))
  },
)

export const createBooking = createAsyncThunk(
  'bookings/create',
  (pilgrimName, { getState, rejectWithValue }) => {
    const token = getState().auth.session?.token
    if (!token) return rejectWithValue('Not authenticated')
    return request('/bookings/', token, {
      method: 'POST',
      body: JSON.stringify({ pilgrimName }),
    }).catch((error) => rejectWithValue(error.message))
  },
)

export const payBooking = createAsyncThunk(
  'bookings/pay',
  (id, { getState, rejectWithValue }) => {
    const token = getState().auth.session?.token
    if (!token) return rejectWithValue('Not authenticated')
    return request(`/bookings/${id}/pay`, token, { method: 'POST' }).catch(
      (error) => rejectWithValue(error.message),
    )
  },
)

export const cancelBooking = createAsyncThunk(
  'bookings/cancel',
  (id, { getState, rejectWithValue }) => {
    const token = getState().auth.session?.token
    if (!token) return rejectWithValue('Not authenticated')
    return request(`/bookings/${id}/cancel`, token, { method: 'POST' }).catch(
      (error) => rejectWithValue(error.message),
    )
  },
)

export const acceptBooking = createAsyncThunk(
  'bookings/accept',
  (id, { getState, rejectWithValue }) => {
    const token = getState().auth.session?.token
    if (!token) return rejectWithValue('Not authenticated')
    return request(`/bookings/${id}/accept`, token, { method: 'POST' }).catch(
      (error) => rejectWithValue(error.message),
    )
  },
)

const bookingsSlice = createSlice({
  name: 'bookings',
  initialState: {
    mine: [],
    available: [],
    selected: null,
    loading: false,
    actionId: null,
    actionType: null,
    recentlyCreated: null,
    error: null,
    successMessage: null,
  },
  reducers: {
    clearBookingError(state) {
      state.error = null
    },
    clearSuccessMessage(state) {
      state.successMessage = null
    },
    clearRecentlyCreated(state) {
      state.recentlyCreated = null
    },
  },
  extraReducers(builder) {
    builder
      .addCase(loadMine.pending, (state) => {
        state.loading = true
        state.error = null
      })
      .addCase(loadMine.fulfilled, (state, action) => {
        state.loading = false
        state.mine = Array.isArray(action.payload) ? action.payload : []
      })
      .addCase(loadAvailable.pending, (state) => {
        state.loading = true
        state.error = null
      })
      .addCase(loadAvailable.fulfilled, (state, action) => {
        state.loading = false
        state.available = Array.isArray(action.payload) ? action.payload : []
      })
      .addCase(loadBooking.pending, (state) => {
        state.loading = true
        state.error = null
        state.selected = null
      })
      .addCase(loadBooking.fulfilled, (state, action) => {
        state.loading = false
        state.selected = action.payload
      })
      // Action states
      .addCase(createBooking.pending, (state) => {
        state.actionType = 'create'
        state.error = null
        state.successMessage = null
      })
      .addCase(createBooking.fulfilled, (state, action) => {
        state.actionType = null
        if (action.payload && action.payload.id) {
          // Immediately prepend to mine so it is guaranteed visible at index 0
          state.mine = [action.payload, ...state.mine.filter((b) => b.id !== action.payload.id)]
          state.recentlyCreated = action.payload
        }
        state.successMessage = `Booking #${action.payload?.id} created for "${action.payload?.pilgrimName}"! Status: Pending Payment (500 SAR).`
      })
      .addCase(payBooking.pending, (state, action) => {
        state.actionType = 'pay'
        state.actionId = action.meta.arg
        state.error = null
        state.successMessage = null
      })
      .addCase(payBooking.fulfilled, (state, action) => {
        state.actionType = null
        state.actionId = null
        const bookingId = action.meta.arg
        // Immediately update status to Paid (2) in memory
        const item = state.mine.find((b) => b.id === bookingId)
        if (item) {
          item.status = 2 // Paid
          item.paidAt = new Date().toISOString()
        }
        if (state.selected && state.selected.id === bookingId) {
          state.selected.status = 2
          state.selected.paidAt = new Date().toISOString()
        }
        if (state.recentlyCreated && state.recentlyCreated.id === bookingId) {
          state.recentlyCreated.status = 2
        }
        const ref = action.payload?.gatewayRef ? `(Gateway Ref: ${action.payload.gatewayRef})` : ''
        state.successMessage = `Payment of 500 SAR confirmed for Booking #${bookingId}! ${ref} Booking is now Paid and ready for performers.`
      })
      .addCase(cancelBooking.pending, (state, action) => {
        state.actionType = 'cancel'
        state.actionId = action.meta.arg
        state.error = null
        state.successMessage = null
      })
      .addCase(cancelBooking.fulfilled, (state, action) => {
        state.actionType = null
        state.actionId = null
        const bookingId = action.meta.arg
        const item = state.mine.find((b) => b.id === bookingId)
        if (item) {
          item.status = 5 // Cancelled
        }
        if (state.selected && state.selected.id === bookingId) {
          state.selected.status = 5
        }
        if (state.recentlyCreated && state.recentlyCreated.id === bookingId) {
          state.recentlyCreated.status = 5
        }
        state.successMessage = `Booking #${bookingId} has been cancelled.`
      })
      .addCase(acceptBooking.pending, (state, action) => {
        state.actionType = 'accept'
        state.actionId = action.meta.arg
        state.error = null
        state.successMessage = null
      })
      .addCase(acceptBooking.fulfilled, (state, action) => {
        state.actionType = null
        state.actionId = null
        const bookingId = action.meta.arg
        // Remove from available immediately
        state.available = state.available.filter((b) => b.id !== bookingId)
        if (action.payload && action.payload.id) {
          state.mine = [action.payload, ...state.mine.filter((b) => b.id !== action.payload.id)]
        }
        state.successMessage = `Booking #${bookingId} accepted! It is now assigned to you.`
      })
      // Global reject matcher
      .addMatcher(
        (action) =>
          [loadMine, loadAvailable, loadBooking, createBooking, payBooking, cancelBooking, acceptBooking].some(
            (thunk) => thunk.rejected.match(action),
          ),
        (state, action) => {
          state.loading = false
          state.actionType = null
          state.actionId = null
          state.error = action.payload || 'An error occurred while communicating with the server.'
        },
      )
  },
})

export const { clearBookingError, clearSuccessMessage, clearRecentlyCreated } = bookingsSlice.actions
export default bookingsSlice.reducer
