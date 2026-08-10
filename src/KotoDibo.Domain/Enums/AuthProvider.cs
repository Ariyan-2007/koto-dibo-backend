namespace KotoDibo.Domain.Enums;

// Extensible: Google/Apple/Phone become new members when those auth methods are implemented,
// each backed by its own UserCredential document — no changes to User required.
public enum AuthProvider
{
    Password,
}
