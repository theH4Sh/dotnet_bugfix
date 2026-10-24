import { configureStore } from '@reduxjs/toolkit'
import authReducer, { logout } from './authSlice'
import bookingsReducer from './bookingsSlice'
import { setOnUnauthorized } from './api'

export const store = configureStore({
  reducer: {
    auth: authReducer,
    bookings: bookingsReducer,
  },
})

// Disconnect on expired token or 401
setOnUnauthorized(() => {
  store.dispatch(logout())
})

let currentSession = store.getState().auth.session
store.subscribe(() => {
  const nextSession = store.getState().auth.session
  if (nextSession === currentSession) return
  currentSession = nextSession
  if (nextSession) localStorage.setItem('booking-session', JSON.stringify(nextSession))
  else localStorage.removeItem('booking-session')
})
