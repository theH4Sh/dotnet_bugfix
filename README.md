Bugs Found:

Auth Bugs:
    - Trusting user input, the app lets anyone register as either performer or customer whereas they should only be able to register as customers.

    - tf is role 3? and why does it responds with 200 Ok, 1 = customer, 2 = performer... why we letting in 3, oh wait we shouldn't even trust the user input in the first place

    - Also lets you register with the same email...  ----- X FIXED

    - AH HELL NAH... DAWG...PASSWORDS ARE STORED IN PLAINTEXT --- X FIXED

    - ALSO WHY WE RETURNING THE PASSWORD IN RESPONSE? --- X FIXEDs

    - No password validation found... like there's no criteria for the password i.e 8 words, uppercase, lowercase none of that sh-

    - aight now I'm logged in and LOCKED IN... we get our beloved JWT, but there's a catch... WHY IS IT EXPIRING AFTER 365 DAYS?????? SHOULD BE MAX 3 DAYS OR LESS... EVEN REFRESH TOKENS AIN'T SUPPOSED TO LAST THAT LONG

BOOKING ROUTES:
    - the api accepts expired tokens to get user bookings...
    - shouldn't let the user choose the amount... X FIXEDs
    - the user is able to accept their own booking which shouldn't be allowed... -- FIXED
    - the user is able to cancel another user's booking -- FIXED
    - user is able to cancel accepted booking -- FIXED
    - user is able to cancel paid bookings -- FIXED
    - Performers should only be able to accept paid bookings -- X FIXED