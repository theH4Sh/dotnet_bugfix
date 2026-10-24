import { useEffect, useState } from 'react'
import { useDispatch, useSelector } from 'react-redux'
import { Link, Navigate, Route, Routes, useLocation, useNavigate, useParams } from 'react-router-dom'
import { clearAuthError, login, logout, register } from './authSlice'
import {
  acceptBooking,
  cancelBooking,
  clearBookingError,
  clearRecentlyCreated,
  clearSuccessMessage,
  createBooking,
  loadAvailable,
  loadBooking,
  loadMine,
  payBooking,
} from './bookingsSlice'

const roleHome = (role) => (role === 'Performer' ? '/available' : '/bookings')

function statusLabel(status) {
  const labels = {
    1: 'Pending Payment',
    2: 'Paid (Ready to Accept)',
    3: 'Accepted (Assigned)',
    4: 'Completed',
    5: 'Cancelled',
    6: 'Expired',
  }
  return labels[status] ?? `Status #${status}`
}

function ProtectedRoute({ children, roles }) {
  const session = useSelector((state) => state.auth.session)
  const location = useLocation()

  if (!session) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (roles && !roles.includes(session.user?.role)) {
    return <Navigate to={roleHome(session.user?.role)} replace />
  }
  return children
}

function SiteHeader() {
  const dispatch = useDispatch()
  const navigate = useNavigate()
  const session = useSelector((state) => state.auth.session)

  return (
    <header className="site-header">
      <div className="header-left">
        <Link to={session ? roleHome(session.user?.role) : '/'} className="logo">
          🕋 Labaik Badal – Booking Service
        </Link>
      </div>

      <nav className="header-nav">
        {session ? (
          <>
            <Link to="/bookings">My Bookings</Link>
            {session.user?.role === 'Performer' && (
              <Link to="/available">Available Bookings</Link>
            )}
            <span className="user-badge">
              {session.user?.email} <strong>({session.user?.role})</strong>
            </span>
            <button
              className="btn btn-secondary"
              onClick={() => {
                dispatch(logout())
                navigate('/login')
              }}
            >
              Sign out
            </button>
          </>
        ) : (
          <>
            <Link to="/login">Sign in</Link>
            <Link to="/register">Register</Link>
          </>
        )}
      </nav>
    </header>
  )
}

function AuthPage({ mode }) {
  const dispatch = useDispatch()
  const navigate = useNavigate()
  const location = useLocation()
  const { session, loading, error } = useSelector((state) => state.auth)
  const [form, setForm] = useState({ name: '', email: '', password: '', role: 1 })

  useEffect(() => {
    dispatch(clearAuthError())
  }, [dispatch, mode])

  if (session) return <Navigate to={roleHome(session.user?.role)} replace />

  async function submit(event) {
    event.preventDefault()
    try {
      if (mode === 'register') {
        await dispatch(register(form)).unwrap()
        navigate('/login', {
          state: { message: `Account created for ${form.email}. Sign in to continue.` },
        })
      } else {
        const loggedSession = await dispatch(
          login({ email: form.email, password: form.password }),
        ).unwrap()
        navigate(location.state?.from || roleHome(loggedSession.user?.role), { replace: true })
      }
    } catch {
      // The rejected request is displayed from the Redux error state.
    }
  }

  return (
    <main className="container auth-container">
      <h1>{mode === 'register' ? 'Create an Account' : 'Sign In'}</h1>
      {location.state?.message && <div className="alert alert-success">{location.state.message}</div>}
      {error && <div className="alert alert-danger">{error}</div>}

      <form onSubmit={submit} className="box">
        {mode === 'register' && (
          <>
            <label>
              Full Name
              <input
                autoComplete="name"
                required
                placeholder="e.g. Ahmad Khan"
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </label>

            <fieldset className="role-selector">
              <legend>I want to register as:</legend>
              <label className="radio-label">
                <input
                  type="radio"
                  name="role"
                  value="1"
                  checked={Number(form.role) === 1}
                  onChange={() => setForm({ ...form, role: 1 })}
                />
                <strong>Customer</strong> (Request / Book Umrah for a pilgrim)
              </label>
              <label className="radio-label">
                <input
                  type="radio"
                  name="role"
                  value="2"
                  checked={Number(form.role) === 2}
                  onChange={() => setForm({ ...form, role: 2 })}
                />
                <strong>Performer</strong> (Perform Umrah on behalf of others)
              </label>
            </fieldset>
          </>
        )}

        <label>
          Email Address
          <input
            autoComplete="email"
            type="email"
            required
            placeholder="you@example.com"
            value={form.email}
            onChange={(event) => setForm({ ...form, email: event.target.value })}
          />
        </label>

        <label>
          Password
          <input
            autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
            type="password"
            required
            placeholder="••••••••"
            value={form.password}
            onChange={(event) => setForm({ ...form, password: event.target.value })}
          />
        </label>

        <button type="submit" className="btn btn-primary" disabled={loading}>
          {loading ? 'Please wait…' : mode === 'register' ? 'Register Account' : 'Sign In'}
        </button>
      </form>

      <p className="auth-footer">
        {mode === 'register' ? 'Already have an account? ' : "Don't have an account? "}
        <Link to={mode === 'register' ? '/login' : '/register'}>
          {mode === 'register' ? 'Sign in here' : 'Register here'}
        </Link>
      </p>
    </main>
  )
}

function Alerts() {
  const dispatch = useDispatch()
  const { error, successMessage } = useSelector((state) => state.bookings)

  if (!error && !successMessage) return null

  return (
    <div className="alerts-container">
      {error && (
        <div className="alert alert-danger">
          <span>{error}</span>
          <button className="close-btn" onClick={() => dispatch(clearBookingError())}>×</button>
        </div>
      )}
      {successMessage && (
        <div className="alert alert-success">
          <span>{successMessage}</span>
          <button className="close-btn" onClick={() => dispatch(clearSuccessMessage())}>×</button>
        </div>
      )}
    </div>
  )
}

function PaymentCheckoutModal({ booking, onClose }) {
  const dispatch = useDispatch()
  const { actionId, actionType } = useSelector((state) => state.bookings)
  const isPaying = actionType === 'pay' && actionId === booking.id

  async function handleConfirmPay(e) {
    e.preventDefault()
    try {
      await dispatch(payBooking(booking.id)).unwrap()
      dispatch(loadMine())
      onClose()
    } catch {
      // Handled in Redux error
    }
  }

  return (
    <div className="modal-overlay">
      <div className="modal-content box">
        <div className="section-header">
          <h2>💳 Payment Gateway Checkout</h2>
          <button className="close-btn" onClick={onClose} disabled={isPaying}>×</button>
        </div>

        <p className="info-text">
          Performers can only see and accept bookings after they are paid. Complete payment of <strong>500 SAR</strong> below.
        </p>

        <div className="payment-summary">
          <div><strong>Booking ID:</strong> #{booking.id}</div>
          <div><strong>Pilgrim Name:</strong> {booking.pilgrimName}</div>
          <div><strong>Amount Due:</strong> <span className="highlight-amount">500.00 SAR</span> (Fixed)</div>
        </div>

        <form onSubmit={handleConfirmPay} className="payment-form">
          <label>
            Cardholder Name
            <input defaultValue="Fatima Ahmad" required disabled={isPaying} />
          </label>
          <label>
            Card Number
            <input defaultValue="4242 •••• •••• 4242" required disabled={isPaying} />
          </label>
          <div className="payment-row">
            <label>
              Expiry
              <input defaultValue="12/28" required disabled={isPaying} />
            </label>
            <label>
              CVC
              <input defaultValue="123" required disabled={isPaying} />
            </label>
          </div>

          <div className="modal-actions">
            <button type="submit" className="btn btn-success" disabled={isPaying}>
              {isPaying ? 'Charging 500 SAR…' : '💳 Confirm & Pay 500 SAR'}
            </button>
            <button type="button" className="btn btn-secondary" onClick={onClose} disabled={isPaying}>
              Cancel / Pay Later
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function CustomerDashboard() {
  const dispatch = useDispatch()
  const { mine, loading, actionId, actionType } = useSelector((state) => state.bookings)
  const [pilgrimName, setPilgrimName] = useState('')
  const [payingBooking, setPayingBooking] = useState(null)

  useEffect(() => {
    dispatch(loadMine())
  }, [dispatch])

  async function handleCreate(e) {
    e.preventDefault()
    if (!pilgrimName.trim()) return
    try {
      const created = await dispatch(createBooking(pilgrimName.trim())).unwrap()
      setPilgrimName('')
      dispatch(loadMine())
      if (created && created.id) {
        setPayingBooking(created)
      }
    } catch {
      // Handled in Redux error state
    }
  }

  async function handleCancel(id) {
    if (!window.confirm(`Are you sure you want to cancel booking #${id}?`)) return
    try {
      await dispatch(cancelBooking(id)).unwrap()
      dispatch(loadMine())
    } catch {
      // Handled in Redux error state
    }
  }

  const pendingBookings = mine.filter((b) => b.status === 1)

  return (
    <main className="container">
      <Alerts />

      {payingBooking && (
        <PaymentCheckoutModal
          booking={payingBooking}
          onClose={() => {
            setPayingBooking(null)
            dispatch(clearRecentlyCreated())
          }}
        />
      )}

      {/* Immediate Payment Reminder if any bookings are pending payment */}
      {pendingBookings.length > 0 && !payingBooking && (
        <div className="pending-payment-banner">
          <div>
            <strong>⚠️ {pendingBookings.length} booking(s) pending payment!</strong>
            <p style={{ margin: '4px 0 0 0', fontSize: '0.9rem' }}>
              Performers will only see and accept your bookings after they are paid.
            </p>
          </div>
          <button
            className="btn btn-success btn-sm"
            onClick={() => setPayingBooking(pendingBookings[0])}
          >
            💳 Pay Booking #{pendingBookings[0].id} (500 SAR)
          </button>
        </div>
      )}

      <section className="box">
        <h2>Create a New Umrah Booking</h2>
        <form onSubmit={handleCreate} className="create-booking-form">
          <label>
            Pilgrim Name
            <input
              required
              placeholder="Full name of pilgrim (e.g. Fatima Bano)"
              value={pilgrimName}
              onChange={(e) => setPilgrimName(e.target.value)}
            />
          </label>
          <div className="price-tag">
            <span>Price:</span> <strong>500 SAR</strong> (Fixed price per booking)
          </div>
          <button
            type="submit"
            className="btn btn-primary"
            disabled={actionType === 'create'}
          >
            {actionType === 'create' ? 'Creating…' : 'Create Booking'}
          </button>
        </form>
      </section>

      <section className="box">
        <div className="section-header">
          <h2>My Bookings ({mine.length})</h2>
          <button className="btn btn-secondary" onClick={() => dispatch(loadMine())} disabled={loading}>
            {loading ? 'Refreshing…' : '↻ Refresh'}
          </button>
        </div>

        {loading && mine.length === 0 ? (
          <p>Loading your bookings…</p>
        ) : mine.length === 0 ? (
          <p className="empty-text">No bookings created yet. Create one above to get started!</p>
        ) : (
          <div className="table-responsive">
            <table className="data-table">
              <thead>
                <tr>
                  <th>#ID</th>
                  <th>Pilgrim Name</th>
                  <th>Amount</th>
                  <th>Status</th>
                  <th>Assigned Performer</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {mine.map((b) => {
                  const isPaying = actionType === 'pay' && actionId === b.id
                  const isCancelling = actionType === 'cancel' && actionId === b.id

                  return (
                    <tr key={b.id}>
                      <td>
                        <Link to={`/bookings/${b.id}`}><strong>#{b.id}</strong></Link>
                      </td>
                      <td>{b.pilgrimName}</td>
                      <td>{b.amount} SAR</td>
                      <td>
                        <span className={`status-badge status-${b.status}`}>
                          {statusLabel(b.status)}
                        </span>
                      </td>
                      <td>
                        {b.performer ? (
                          <span>{b.performer.name || b.performer.email}</span>
                        ) : (
                          <span className="text-muted">Unassigned</span>
                        )}
                      </td>
                      <td className="actions-cell">
                        {b.status === 1 && (
                          <button
                            className="btn btn-success btn-sm"
                            disabled={isPaying || isCancelling}
                            onClick={() => setPayingBooking(b)}
                          >
                            💳 Pay (500 SAR)
                          </button>
                        )}
                        {[1, 2].includes(b.status) && (
                          <button
                            className="btn btn-danger btn-sm"
                            disabled={isPaying || isCancelling}
                            onClick={() => handleCancel(b.id)}
                          >
                            {isCancelling ? 'Cancelling…' : 'Cancel'}
                          </button>
                        )}
                        <Link to={`/bookings/${b.id}`} className="btn btn-secondary btn-sm">
                          Details
                        </Link>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  )
}

function PerformerDashboard({ view = 'available' }) {
  const dispatch = useDispatch()
  const { mine, available, loading, actionId, actionType } = useSelector((state) => state.bookings)

  useEffect(() => {
    dispatch(loadMine())
    dispatch(loadAvailable())
  }, [dispatch])

  async function handleAccept(id) {
    try {
      await dispatch(acceptBooking(id)).unwrap()
      dispatch(loadAvailable())
      dispatch(loadMine())
    } catch {
      // Handled in Redux error state
    }
  }

  return (
    <main className="container">
      <Alerts />

      <div className="tab-buttons">
        <Link
          to="/available"
          className={`tab-btn ${view === 'available' ? 'active' : ''}`}
        >
          Available Paid Bookings ({available.length})
        </Link>
        <Link
          to="/bookings"
          className={`tab-btn ${view === 'mine' ? 'active' : ''}`}
        >
          My Accepted Bookings ({mine.length})
        </Link>
      </div>

      {view === 'available' ? (
        <section className="box">
          <div className="section-header">
            <h2>Paid Bookings Available to Accept</h2>
            <button
              className="btn btn-secondary"
              onClick={() => dispatch(loadAvailable())}
              disabled={loading}
            >
              {loading ? 'Refreshing…' : '↻ Refresh Available'}
            </button>
          </div>

          <p className="info-text">
            These bookings have been fully paid (500 SAR) by customers. Click <strong>Accept Booking</strong> to claim one and perform the Umrah.
          </p>

          {loading && available.length === 0 ? (
            <p>Loading available bookings…</p>
          ) : available.length === 0 ? (
            <p className="empty-text">No paid bookings available right now. Check back soon!</p>
          ) : (
            <div className="table-responsive">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>#ID</th>
                    <th>Pilgrim Name</th>
                    <th>Compensation</th>
                    <th>Status</th>
                    <th>Paid At</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {available.map((b) => {
                    const isAccepting = actionType === 'accept' && actionId === b.id

                    return (
                      <tr key={b.id}>
                        <td>
                          <Link to={`/bookings/${b.id}`}><strong>#{b.id}</strong></Link>
                        </td>
                        <td>{b.pilgrimName}</td>
                        <td>{b.amount} SAR</td>
                        <td>
                          <span className="status-badge status-2">{statusLabel(b.status)}</span>
                        </td>
                        <td>{b.paidAt ? new Date(b.paidAt).toLocaleString() : 'Just now'}</td>
                        <td className="actions-cell">
                          <button
                            className="btn btn-primary btn-sm"
                            disabled={isAccepting}
                            onClick={() => handleAccept(b.id)}
                          >
                            {isAccepting ? 'Accepting…' : '✓ Accept Booking (500 SAR)'}
                          </button>
                          <Link to={`/bookings/${b.id}`} className="btn btn-secondary btn-sm">
                            Details
                          </Link>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>
      ) : (
        <section className="box">
          <div className="section-header">
            <h2>Bookings Assigned to Me ({mine.length})</h2>
            <button
              className="btn btn-secondary"
              onClick={() => dispatch(loadMine())}
              disabled={loading}
            >
              {loading ? 'Refreshing…' : '↻ Refresh Mine'}
            </button>
          </div>

          {loading && mine.length === 0 ? (
            <p>Loading your assigned bookings…</p>
          ) : mine.length === 0 ? (
            <p className="empty-text">
              You haven't accepted any bookings yet. Head over to{' '}
              <Link to="/available">Available Bookings</Link> to accept one.
            </p>
          ) : (
            <div className="table-responsive">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>#ID</th>
                    <th>Pilgrim Name</th>
                    <th>Amount</th>
                    <th>Status</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {mine.map((b) => (
                    <tr key={b.id}>
                      <td>
                        <Link to={`/bookings/${b.id}`}><strong>#{b.id}</strong></Link>
                      </td>
                      <td>{b.pilgrimName}</td>
                      <td>{b.amount} SAR</td>
                      <td>
                        <span className={`status-badge status-${b.status}`}>
                          {statusLabel(b.status)}
                        </span>
                      </td>
                      <td className="actions-cell">
                        <Link to={`/bookings/${b.id}`} className="btn btn-secondary btn-sm">
                          View Details
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}
    </main>
  )
}

function BookingDetails() {
  const { id } = useParams()
  const dispatch = useDispatch()
  const role = useSelector((state) => state.auth.session?.user?.role)
  const { selected: booking, loading, actionId, actionType } = useSelector((state) => state.bookings)
  const [showCheckout, setShowCheckout] = useState(false)

  useEffect(() => {
    dispatch(loadBooking(id))
  }, [dispatch, id])

  async function handleCancel() {
    if (!window.confirm(`Are you sure you want to cancel booking #${id}?`)) return
    try {
      await dispatch(cancelBooking(id)).unwrap()
      dispatch(loadBooking(id))
      dispatch(loadMine())
    } catch {
      // Handled in Redux error state
    }
  }

  async function handleAccept() {
    try {
      await dispatch(acceptBooking(id)).unwrap()
      dispatch(loadBooking(id))
      dispatch(loadMine())
      dispatch(loadAvailable())
    } catch {
      // Handled in Redux error state
    }
  }

  const isCurrentAction = actionId === Number(id) || actionId === id

  return (
    <main className="container">
      <p>
        <Link to={roleHome(role)} className="back-link">
          ← Back to {role === 'Performer' ? 'Dashboard' : 'My Bookings'}
        </Link>
      </p>

      <Alerts />

      {showCheckout && booking && (
        <PaymentCheckoutModal
          booking={booking}
          onClose={() => {
            setShowCheckout(false)
            dispatch(loadBooking(id))
          }}
        />
      )}

      {loading ? (
        <p>Loading booking #{id} details…</p>
      ) : booking ? (
        <div className="box details-card">
          <div className="section-header">
            <h1>Booking #{booking.id}</h1>
            <span className={`status-badge status-${booking.status}`}>
              {statusLabel(booking.status)}
            </span>
          </div>

          <div className="details-grid">
            <div>
              <strong>Pilgrim Name:</strong>
              <p>{booking.pilgrimName}</p>
            </div>
            <div>
              <strong>Fixed Amount:</strong>
              <p>{booking.amount} SAR</p>
            </div>
            <div>
              <strong>Customer:</strong>
              <p>{booking.customer?.name ? `${booking.customer.name} (${booking.customer.email})` : `Customer #${booking.customerId}`}</p>
            </div>
            <div>
              <strong>Assigned Performer:</strong>
              <p>
                {booking.performer
                  ? `${booking.performer.name || 'Performer'} (${booking.performer.email})`
                  : booking.performerId
                  ? `Performer #${booking.performerId}`
                  : 'None (Unassigned)'}
              </p>
            </div>
            <div>
              <strong>Created At:</strong>
              <p>{booking.createdAt ? new Date(booking.createdAt).toLocaleString() : 'N/A'}</p>
            </div>
            <div>
              <strong>Paid At:</strong>
              <p>{booking.paidAt ? new Date(booking.paidAt).toLocaleString() : 'Not paid yet'}</p>
            </div>
          </div>

          <div className="details-actions">
            {role === 'Customer' && booking.status === 1 && (
              <button
                className="btn btn-success"
                onClick={() => setShowCheckout(true)}
              >
                💳 Pay 500 SAR Now (Payment Gateway)
              </button>
            )}

            {role === 'Customer' && [1, 2].includes(booking.status) && (
              <button
                className="btn btn-danger"
                disabled={isCurrentAction && actionType === 'cancel'}
                onClick={handleCancel}
              >
                {isCurrentAction && actionType === 'cancel' ? 'Cancelling…' : 'Cancel Booking'}
              </button>
            )}

            {role === 'Performer' && booking.status === 2 && !booking.performerId && (
              <button
                className="btn btn-primary"
                disabled={isCurrentAction && actionType === 'accept'}
                onClick={handleAccept}
              >
                {isCurrentAction && actionType === 'accept' ? 'Accepting…' : '✓ Accept Booking'}
              </button>
            )}
          </div>
        </div>
      ) : (
        <div className="box">
          <p className="empty-text">Booking not found or you do not have permission to view it.</p>
        </div>
      )}
    </main>
  )
}

export default function App() {
  const session = useSelector((state) => state.auth.session)

  return (
    <>
      <SiteHeader />
      <Routes>
        <Route
          path="/"
          element={<Navigate to={session ? roleHome(session.user?.role) : '/login'} replace />}
        />
        <Route path="/login" element={<AuthPage mode="login" />} />
        <Route path="/register" element={<AuthPage mode="register" />} />

        {/* Customer Route */}
        <Route
          path="/bookings"
          element={
            <ProtectedRoute roles={['Customer', 'Performer']}>
              {session?.user?.role === 'Performer' ? (
                <PerformerDashboard view="mine" />
              ) : (
                <CustomerDashboard />
              )}
            </ProtectedRoute>
          }
        />

        {/* Performer Route */}
        <Route
          path="/available"
          element={
            <ProtectedRoute roles={['Performer']}>
              <PerformerDashboard view="available" />
            </ProtectedRoute>
          }
        />

        {/* Booking Details Route */}
        <Route
          path="/bookings/:id"
          element={
            <ProtectedRoute roles={['Customer', 'Performer']}>
              <BookingDetails />
            </ProtectedRoute>
          }
        />

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  )
}
