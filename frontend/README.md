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

The Vite development server proxies `/auth` and `/bookings` requests to the API.
Set `VITE_API_URL` if the API is hosted at a different origin.
