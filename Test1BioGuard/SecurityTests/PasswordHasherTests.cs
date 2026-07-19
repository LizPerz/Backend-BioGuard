using FluentAssertions;
using BioGuard.Api.Services;

namespace Test1BioGuard.SecurityTests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ContrasenaValida_RetornaHash()
    {
        var hash = PasswordHasher.Hash("Password123!");

        hash.Should().NotBeNullOrEmpty();
        hash.Should().Contain(".");
        var parts = hash.Split('.');
        parts.Length.Should().Be(3);
    }

    [Fact]
    public void Hash_MismaContrasena_RetornaHashesDiferentes()
    {
        var hash1 = PasswordHasher.Hash("Password123!");
        var hash2 = PasswordHasher.Hash("Password123!");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Hash_ContrasenaVacia_RetornaHash()
    {
        var hash = PasswordHasher.Hash("");

        hash.Should().NotBeNullOrEmpty();
        var parts = hash.Split('.');
        parts.Length.Should().Be(3);
    }

    [Fact]
    public void Verify_ContrasenaCorrecta_RetornaTrue()
    {
        var hash = PasswordHasher.Hash("Password123!");

        var result = PasswordHasher.Verify("Password123!", hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ContrasenaIncorrecta_RetornaFalse()
    {
        var hash = PasswordHasher.Hash("Password123!");

        var result = PasswordHasher.Verify("WrongPassword!", hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_HashCorrupto_RetornaFalse()
    {
        var result = PasswordHasher.Verify("Password123!", "corrupt.hash.value");

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_HashVacio_RetornaFalse()
    {
        var result = PasswordHasher.Verify("Password123!", "");

        result.Should().BeFalse();
    }

    [Fact]
    public void Hash_FormatoContieneIteraciones()
    {
        var hash = PasswordHasher.Hash("test");

        hash.Should().StartWith("100000.");
    }

    [Fact]
    public void Hash_ContrasenaLarga_RetornaHash()
    {
        var longPassword = new string('A', 1000);
        var hash = PasswordHasher.Hash(longPassword);

        hash.Should().NotBeNullOrEmpty();
        PasswordHasher.Verify(longPassword, hash).Should().BeTrue();
    }

    [Fact]
    public void Hash_CaracteresEspeciales_RetornaHash()
    {
        var specialPassword = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
        var hash = PasswordHasher.Hash(specialPassword);

        hash.Should().NotBeNullOrEmpty();
        PasswordHasher.Verify(specialPassword, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_TimingSafe_ComparaCorrectamente()
    {
        var hash1 = PasswordHasher.Hash("Password123!");
        var hash2 = PasswordHasher.Hash("Password123!");

        PasswordHasher.Verify("Password123!", hash1).Should().BeTrue();
        PasswordHasher.Verify("Password123!", hash2).Should().BeTrue();
        PasswordHasher.Verify("WrongPassword!", hash1).Should().BeFalse();
    }

    [Fact]
    public void Hash_UsaPBKDF2_ConFormatoCorrecto()
    {
        var hash = PasswordHasher.Hash("test");
        var parts = hash.Split('.');

        int.Parse(parts[0]).Should().Be(100000);
        Convert.FromBase64String(parts[1]).Length.Should().Be(16);
        Convert.FromBase64String(parts[2]).Length.Should().Be(32);
    }
}
