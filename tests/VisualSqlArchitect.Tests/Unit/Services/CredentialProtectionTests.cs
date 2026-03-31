using VisualSqlArchitect.Core;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

/// <summary>
/// Tests for credential protection in ConnectionProfile.
/// Regression tests for bug where passwords were stored in plaintext in connections.json.
/// </summary>
public class CredentialProtectionTests
{
    private static void AssertProtectedFormat(string protectedValue)
    {
        Assert.True(
            protectedValue.StartsWith("enc:", StringComparison.Ordinal) ||
            protectedValue.StartsWith("dpapi:", StringComparison.Ordinal),
            "Expected protected value to start with 'enc:' or 'dpapi:' prefix.");
    }

    [Fact]
    public void WithProtectedPassword_EncryptsPassword()
    {
        // Arrange
        var profile = new ConnectionProfile
        {
            Name = "Test DB",
            Password = "MySecretPassword"
        };

        // Act
        var protected_ = profile.WithProtectedPassword();

        // Assert
        // Protected password should start with a known protection prefix
        AssertProtectedFormat(protected_.Password);
        // Should not be same as original plaintext
        Assert.NotEqual("MySecretPassword", protected_.Password);
    }

    [Fact]
    public void WithUnprotectedPassword_DecryptsEncryptedPassword()
    {
        // Arrange
        var plaintext = "MySecretPassword";
        var profile = new ConnectionProfile
        {
            Name = "Test DB",
            Password = plaintext
        };

        // Act
        var protected_ = profile.WithProtectedPassword();
        var unprotected = protected_.WithUnprotectedPassword();

        // Assert
        Assert.Equal(plaintext, unprotected.Password);
    }

    [Fact]
    public void WithUnprotectedPassword_HandlesLegacyPlaintextPassword()
    {
        // Arrange
        // Simulate a profile loaded from legacy file (plaintext password, no "enc:" prefix)
        var profile = new ConnectionProfile
        {
            Name = "Legacy DB",
            Password = "OldPlaintextPassword"  // No "enc:" prefix
        };

        // Act
        var unprotected = profile.WithUnprotectedPassword();

        // Assert
        // Legacy plaintext passwords should be returned unchanged (backward compatible)
        Assert.Equal("OldPlaintextPassword", unprotected.Password);
    }

    [Fact]
    public void WithProtectedPassword_EmptyPasswordRemainsEmpty()
    {
        // Arrange
        var profile = new ConnectionProfile
        {
            Name = "Test DB",
            Password = ""
        };

        // Act
        var protected_ = profile.WithProtectedPassword();

        // Assert
        Assert.Empty(protected_.Password);
    }

    [Fact]
    public void WithProtectedPassword_NullPasswordRemainNull()
    {
        // Arrange
        var profile = new ConnectionProfile
        {
            Name = "Test DB",
            Password = null!
        };

        // Act
        var protected_ = profile.WithProtectedPassword();

        // Assert
        Assert.Null(protected_.Password);
    }

    [Fact]
    public void WithProtectedPassword_PreservesOtherFields()
    {
        // Arrange
        var profile = new ConnectionProfile
        {
            Id = "test-id-123",
            Name = "My Database",
            Provider = DatabaseProvider.Postgres,
            Host = "db.example.com",
            Port = 5432,
            Database = "mydb",
            Username = "admin",
            Password = "password123",
            UseIntegratedSecurity = false,
            TimeoutSeconds = 30
        };

        // Act
        var protected_ = profile.WithProtectedPassword();

        // Assert
        Assert.Equal("test-id-123", protected_.Id);
        Assert.Equal("My Database", protected_.Name);
        Assert.Equal(DatabaseProvider.Postgres, protected_.Provider);
        Assert.Equal("db.example.com", protected_.Host);
        Assert.Equal(5432, protected_.Port);
        Assert.Equal("mydb", protected_.Database);
        Assert.Equal("admin", protected_.Username);
        Assert.False(protected_.UseIntegratedSecurity);
        Assert.Equal(30, protected_.TimeoutSeconds);
        // Only password should be protected
        AssertProtectedFormat(protected_.Password);
    }

    [Fact]
    public void RegressionTest_PasswordNotPlaintextInFile()
    {
        // This test verifies that passwords are never stored in plaintext.
        // In the bug, passwords were serialized directly without encryption.

        // Arrange
        var profile = new ConnectionProfile
        {
            Name = "Production DB",
            Password = "SuperSecretDatabasePassword"
        };

        // Act
        var protected_ = profile.WithProtectedPassword();

        // Simulate what would be written to file
        var jsonPassword = protected_.Password;

        // Assert
        // Password in the "file" should NEVER be plaintext
        Assert.DoesNotContain("SuperSecretDatabasePassword", jsonPassword);
        AssertProtectedFormat(jsonPassword);
    }

    [Fact]
    public void RegressionTest_CredentialProtector_CanRoundTrip()
    {
        // Regression test: Ensures CredentialProtector is working and being used

        // Arrange
        var secret = "MyPasswordSecret123!@#";

        // Act
        var encrypted = CredentialProtector.Protect(secret);
        var decrypted = CredentialProtector.Unprotect(encrypted);

        // Assert
        Assert.Equal(secret, decrypted);
        Assert.NotEqual(secret, encrypted);
        AssertProtectedFormat(encrypted);
    }

    [Fact]
    public void RoundTrip_EncryptAndDecrypt()
    {
        // Arrange
        var originalPassword = "ComplexP@ssw0rd!";
        var profile = new ConnectionProfile
        {
            Name = "Test",
            Provider = DatabaseProvider.MySql,
            Password = originalPassword
        };

        // Act
        var protected_ = profile.WithProtectedPassword();
        var restored = protected_.WithUnprotectedPassword();

        // Assert
        Assert.Equal(originalPassword, restored.Password);
        // Passwords on disk should be encrypted
        AssertProtectedFormat(protected_.Password);
    }

    [Fact]
    public void EncryptedPassword_DifferentEachTime()
    {
        // Due to random nonce in AES-GCM, same plaintext produces different ciphertext

        // Arrange
        var password = "TestPassword";
        var profile1 = new ConnectionProfile { Password = password };
        var profile2 = new ConnectionProfile { Password = password };

        // Act
        var encrypted1 = profile1.WithProtectedPassword().Password;
        var encrypted2 = profile2.WithProtectedPassword().Password;

        // Assert
        // Different encryptions due to random nonce
        Assert.NotEqual(encrypted1, encrypted2);
        // But both decrypt to the same password
        var decrypted1 = CredentialProtector.Unprotect(encrypted1);
        var decrypted2 = CredentialProtector.Unprotect(encrypted2);
        Assert.Equal(password, decrypted1);
        Assert.Equal(password, decrypted2);
    }
}
