# Labaik Badal – .NET Bug-Fix Assignment

**Time:** 3 hours max · **AI tools allowed** (Copilot, ChatGPT, Claude, Cursor…) · **Stack:** .NET 10, EF Core, SQLite, JWT

## The situation
This is a small booking API for an Umrah service. A previous developer wrote it quickly. It "works on my machine",
but it has **many bugs**: security holes, money bugs, race conditions, crashes and bad practices.

How it should work:
- **Customers** register, create a booking for a pilgrim, pay for it, and can cancel it before a performer accepts it.
- The **price is fixed by the business: 500 SAR**.
- **Performers** see paid bookings and accept one. **Only one performer** can get a booking.
- A background job **expires bookings that stay unpaid** for 30 minutes.
- Users must only see and change **their own** data.

## Your task
1. Get the project running: `cd BookingApi && dotnet run`. It may not even start; that is bug #1.
2. **Find and fix as many bugs as you can.** Prioritise: security > money > data correctness > code quality.
3. Add a **test project** with at least **5 meaningful tests**, including:
   - a customer cannot read or cancel another customer's booking
   - paying twice does not charge twice
   - two performers accepting at the same time: only one wins
4. Write **`FIXES.md`** listing every bug you found:
   *what it was → why it's dangerous → how you fixed it*. Include bugs you found but didn't have time to fix.
5. In `FIXES.md`, add a short section: **"How I used AI"**, covering what you asked, and what you accepted, changed or rejected.

## Submit
A GitHub repo link (one commit per fix is a plus). We'll then do a **30-minute call** where you explain your fixes.

## We judge
- Number and severity of real bugs found
- Quality of fixes (root cause, not hiding errors)
- Tests that fail before the fix and pass after it
- Can you explain every line you submit, including the lines the AI wrote?
