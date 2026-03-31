using System.Reflection;
using VisualSqlArchitect.UI.Services;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

public class CredentialProtectorKeyDerivationTests
{
    [Fact]
    public void InstallationSecret_LoadOrCreate_IsStableAndExpectedSize()
    {
        MethodInfo method = typeof(CredentialProtector)
            .GetMethod("LoadOrCreateInstallationSecret", BindingFlags.NonPublic | BindingFlags.Static)!;

        byte[] secret1 = (byte[])method.Invoke(null, null)!;
        byte[] secret2 = (byte[])method.Invoke(null, null)!;

        Assert.Equal(32, secret1.Length);
        Assert.Equal(secret1, secret2);
    }
}
