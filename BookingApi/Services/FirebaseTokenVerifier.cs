using FirebaseAdmin;
using FirebaseAdmin.Auth;

namespace BookingApi.Services;

public record FirebaseTokenResult(string Uid, string Email, bool EmailVerified, string? Name);

public interface IFirebaseTokenVerifier
{
    Task<FirebaseTokenResult> VerifyTokenAsync(string idToken, CancellationToken ct = default);
}

public class FirebaseTokenVerifier : IFirebaseTokenVerifier
{
    public async Task<FirebaseTokenResult> VerifyTokenAsync(string idToken, CancellationToken ct = default)
    {
        var auth = FirebaseAuth.DefaultInstance
            ?? throw new InvalidOperationException("FirebaseApp has not been initialized.");

        var decoded = await auth.VerifyIdTokenAsync(idToken, ct);

        var email = decoded.Claims.TryGetValue("email", out var e) ? e?.ToString() ?? "" : "";
        var emailVerified = decoded.Claims.TryGetValue("email_verified", out var ev) && ev is true or "true";
        var name = decoded.Claims.TryGetValue("name", out var n) ? n?.ToString() : null;

        return new FirebaseTokenResult(decoded.Uid, email, emailVerified, name);
    }
}

