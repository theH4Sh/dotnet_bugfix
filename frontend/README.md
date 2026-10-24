# Booking frontend

The React app provides customer registration and sign-in, booking creation,
payment, cancellation and details, plus performer booking lists and acceptance.
State is managed with Redux Toolkit and pages use React Router.

Start the API at `http://localhost:5000` (for example, from `BookingApi`, run
`dotnet run --urls http://localhost:5000`), then start the frontend:

```sh
npm install
npm run dev
```

The frontend reads `VITE_API_URL` from `frontend/.env`. Set it to the API
origin (for example, `http://localhost:5000`). When it is unset,
the Vite development server proxies `/auth` and `/bookings` to
`http://localhost:5000`.
